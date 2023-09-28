using Lumina.Data.Files;
using OtterTex;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.Import.Textures;

public readonly struct BaseImage : IDisposable
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

    public TexFile? AsTex
        => Image as TexFile;

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

    /// <summary> Obtain RGBA pixel data for the given image (not including any mip maps.) </summary>
    public (byte[] Rgba, int Width, int Height) GetPixelData()
    {
        switch (Image)
        {
            case null: return (Array.Empty<byte>(), 0, 0);
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
            default: return (Array.Empty<byte>(), 0, 0);
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
            TexFile t      => t.Header.Format.ToDXGI(),
            Image<Rgba32>  => DXGIFormat.B8G8R8X8UNorm,
            _              => DXGIFormat.Unknown,
        };

    public int MipMaps
        => Image switch
        {
            null           => 0,
            ScratchImage s => s.Meta.MipLevels,
            TexFile t      => t.Header.MipLevelsCount,
            _              => 1,
        };
}
