using ImSharp;
using OtterTex;
using SixLabors.ImageSharp;
using Rgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Penumbra.Import.Textures;

public readonly struct BaseImage : IDisposable, IEquatable<BaseImage>, IEqualityOperators<BaseImage, BaseImage, bool>
{
    public readonly object? Image;

    public BaseImage(ScratchImage scratch)
        => Image = scratch;

    public BaseImage(Image<Rgba32> image)
        => Image = image;

    public static implicit operator BaseImage(ScratchImage scratch)
        => new(scratch);

    public static implicit operator BaseImage(Image<Rgba32> img)
        => new(img);

    public ScratchImage? AsDds
        => Image as ScratchImage;

    public Image<Rgba32>? AsPng
        => Image as Image<Rgba32>;

    public TextureType Type
        => Image switch
        {
            null          => TextureType.Unknown,
            ScratchImage  => TextureType.Dds,
            Image<Rgba32> => TextureType.Png,
            _             => TextureType.Unknown,
        };

    public void Dispose()
        => (Image as IDisposable)?.Dispose();

    public override int GetHashCode()
        => Image is null ? 0 : Image.GetHashCode();

    public override bool Equals(object? obj)
        => obj is BaseImage other && Equals(other);

    public bool Equals(BaseImage other)
        => ReferenceEquals(Image, other.Image);

    /// <summary> Obtain RGBA pixel data for the given image (not including any mip maps.) </summary>
    public (byte[] Rgba, int Width, int Height) GetPixelData()
    {
        switch (Image)
        {
            case null: return ([], 0, 0);
            case ScratchImage scratch:
            {
                var rgba = scratch.GetRGBA(out var f).ThrowIfError(f);
                return (rgba.Pixels[..(f.Meta.Width * f.Meta.Height * (f.Meta.Format.BitsPerPixel() / 8))].ToArray(), f.Meta.Width,
                    f.Meta.Height);
            }
            case Image<Rgba32> img:
            {
                var ret = new byte[img.Height * img.Width * 4];
                img.CopyPixelDataTo(ret);
                return (ret, img.Width, img.Height);
            }
            default: return ([], 0, 0);
        }
    }

    public ColorParameter IsSolidColor()
    {
        var (rgba, _, _) = GetPixelData();
        return IsSolidColor(rgba);
    }

    public static ColorParameter IsSolidColor(Span<byte> rgba)
    {
        if (rgba.Length < 4 || (rgba.Length & 3) is not 0)
            return ColorParameter.Default;

        if (rgba.Length < 8)
            return Unsafe.As<byte, ImSharp.Rgba32>(ref rgba[0]);

        var startValue = Unsafe.As<byte, uint>(ref rgba[0]);
        if ((rgba.Length & 7) is 0)
        {
            if (startValue != Unsafe.As<byte, uint>(ref rgba[4]))
                return ColorParameter.Default;

            rgba = rgba[8..];
        }
        else
        {
            rgba = rgba[4..];
        }

        var doubleValue = startValue | ((ulong)startValue << 32);
        var span        = MemoryMarshal.Cast<byte, ulong>(rgba);
        foreach (var value in span)
        {
            if (doubleValue != value)
                return ColorParameter.Default;
        }

        return new ImSharp.Rgba32(startValue);
    }

    public unsafe BaseImage AtLevelOfDetail(int lod)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lod);
        if (lod is 0)
            return this;

        switch (Image)
        {
            case null or Image<Rgba32>: throw new ArgumentOutOfRangeException(nameof(lod));
            case ScratchImage scratch:
                ref readonly var meta = ref scratch.Meta;
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(lod, meta.MipLevels);
                if (meta is not { Dimension: TexDimension.Tex2D, IsCubeMap: false, ArraySize: 1 })
                    throw new NotImplementedException();

                ref readonly var image = ref scratch.Images[lod];
                var downscaled = ScratchImage.Initialize2D(meta.Format, image.Width, image.Height, meta.ArraySize, meta.MipLevels - lod);
                fixed (byte* ptr = downscaled.Pixels)
                {
                    var span = new Span<byte>(ptr, downscaled.Pixels.Length);
                    scratch.Pixels[scratch.ImagePixelOffsets[lod]..].CopyTo(span);
                }

                return downscaled;
            default: throw new NotImplementedException();
        }
    }

    public (int Width, int Height) Dimensions
        => Image switch
        {
            null                 => (0, 0),
            ScratchImage scratch => (scratch.Meta.Width, scratch.Meta.Height),
            Image<Rgba32> img    => (img.Width, img.Height),
            _                    => (0, 0),
        };

    public int Width
        => Dimensions.Width;

    public int Height
        => Dimensions.Height;

    public Vector2 ImageSize
    {
        get
        {
            var (width, height) = Dimensions;
            return new Vector2(width, height);
        }
    }

    public DXGIFormat Format
        => Image switch
        {
            null           => DXGIFormat.Unknown,
            ScratchImage s => s.Meta.Format,
            Image<Rgba32>  => DXGIFormat.B8G8R8X8UNorm,
            _              => DXGIFormat.Unknown,
        };

    public int MipMaps
        => Image switch
        {
            null           => 0,
            ScratchImage s => s.Meta.MipLevels,
            _              => 1,
        };

    public static bool operator ==(BaseImage lhs, BaseImage rhs)
        => lhs.Equals(rhs);

    public static bool operator !=(BaseImage lhs, BaseImage rhs)
        => !lhs.Equals(rhs);
}
