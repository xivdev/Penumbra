using Lumina.Data.Files;
using Lumina.Extensions;
using OtterTex;

namespace Penumbra.Import.Textures;

public static class TexFileParser
{
    public static ScratchImage Parse(Stream data)
    {
        using var r      = new BinaryReader(data);
        var       header = r.ReadStructure<TexFile.TexHeader>();

        var meta = header.ToTexMeta();
        if (meta.Format == DXGIFormat.Unknown)
            throw new Exception($"Could not convert format {header.Format} to DXGI Format.");

        if (meta.Dimension == TexDimension.Unknown)
            throw new Exception($"Could not obtain dimensionality from {header.Type}.");

        meta.MipLevels = CountMipLevels(data, in meta, in header);
        if (meta.MipLevels == 0)
            throw new Exception("Could not load file. Image is corrupted and does not contain enough data for its size.");

        var scratch = ScratchImage.Initialize(meta);

        CopyData(scratch, r);

        return scratch;
    }

    private static unsafe int CountMipLevels(Stream data, in TexMeta meta, in TexFile.TexHeader header)
    {
        var width  = meta.Width;
        var height = meta.Height;
        var bits   = meta.Format.BitsPerPixel();

        var lastOffset = 0L;
        var lastSize   = 80L;
        var minSize    = meta.Format.IsCompressed() ? 4 : 1;
        for (var i = 0; i < 13; ++i)
        {
            var offset = header.OffsetToSurface[i];
            if (offset == 0)
                return i;

            var requiredSize = width * height * bits / 8;
            if (offset + requiredSize > data.Length)
                return i;

            var diff = offset - lastOffset;
            if (diff != lastSize)
                return i;

            width      = Math.Max(width / 2,  minSize);
            height     = Math.Max(height / 2, minSize);
            lastOffset = offset;
            lastSize   = requiredSize;
        }

        return 13;
    }

    private static unsafe void CopyData(ScratchImage image, BinaryReader r)
    {
        fixed (byte* ptr = image.Pixels)
        {
            var span      = new Span<byte>(ptr, image.Pixels.Length);
            var readBytes = r.Read(span);
            if (readBytes < image.Pixels.Length)
                throw new Exception($"Invalid data length {readBytes} < {image.Pixels.Length}.");
        }
    }

    public static void Write(this TexFile.TexHeader header, BinaryWriter w)
    {
        w.Write((uint)header.Type);
        w.Write((uint)header.Format);
        w.Write(header.Width);
        w.Write(header.Height);
        w.Write(header.Depth);
        w.Write(header.MipLevelsCount);
        w.Write((byte)0); // TODO Lumina Update
        unsafe
        {
            w.Write(header.LodOffset[0]);
            w.Write(header.LodOffset[1]);
            w.Write(header.LodOffset[2]);
            for (var i = 0; i < 13; ++i)
                w.Write(header.OffsetToSurface[i]);
        }
    }

    public static TexFile.TexHeader ToTexHeader(this ScratchImage scratch)
    {
        var meta = scratch.Meta;
        var ret = new TexFile.TexHeader()
        {
            Height         = (ushort)meta.Height,
            Width          = (ushort)meta.Width,
            Depth          = (ushort)Math.Max(meta.Depth, 1),
            MipLevelsCount = (byte)Math.Min(meta.MipLevels, 13),
            Format         = meta.Format.ToTexFormat(),
            Type = meta.Dimension switch
            {
                _ when meta.IsCubeMap => TexFile.Attribute.TextureTypeCube,
                TexDimension.Tex1D    => TexFile.Attribute.TextureType1D,
                TexDimension.Tex2D    => TexFile.Attribute.TextureType2D,
                TexDimension.Tex3D    => TexFile.Attribute.TextureType3D,
                _                     => 0,
            },
        };

        ret.FillSurfaceOffsets(scratch);

        return ret;
    }

    private static unsafe void FillSurfaceOffsets(this ref TexFile.TexHeader header, ScratchImage scratch)
    {
        var idx = 0;
        fixed (byte* ptr = scratch.Pixels)
        {
            foreach (var image in scratch.Images)
            {
                var offset = (byte*)image.Pixels - ptr;
                header.OffsetToSurface[idx++] = (uint)(80 + offset);
            }
        }

        for (; idx < 13; ++idx)
            header.OffsetToSurface[idx] = 0;

        header.LodOffset[0] = 0;
        header.LodOffset[1] = 1;
        header.LodOffset[2] = 2;
    }


    public static TexMeta ToTexMeta(this TexFile.TexHeader header)
        => new()
        {
            Height     = header.Height,
            Width      = header.Width,
            Depth      = Math.Max(header.Depth, (ushort)1),
            MipLevels  = header.MipLevelsCount,
            ArraySize  = 1,
            Format     = header.Format.ToDXGI(),
            Dimension  = header.Type.ToDimension(),
            MiscFlags  = header.Type.HasFlag(TexFile.Attribute.TextureTypeCube) ? D3DResourceMiscFlags.TextureCube : 0,
            MiscFlags2 = 0,
        };

    private static TexDimension ToDimension(this TexFile.Attribute attribute)
        => (attribute & TexFile.Attribute.TextureTypeMask) switch
        {
            TexFile.Attribute.TextureType1D => TexDimension.Tex1D,
            TexFile.Attribute.TextureType2D => TexDimension.Tex2D,
            TexFile.Attribute.TextureType3D => TexDimension.Tex3D,
            _                               => TexDimension.Unknown,
        };

    public static TexFile.TextureFormat ToTexFormat(this DXGIFormat format)
        => format switch
        {
            DXGIFormat.R8UNorm              => TexFile.TextureFormat.L8,
            DXGIFormat.A8UNorm              => TexFile.TextureFormat.A8,
            DXGIFormat.B4G4R4A4UNorm        => TexFile.TextureFormat.B4G4R4A4,
            DXGIFormat.B5G5R5A1UNorm        => TexFile.TextureFormat.B5G5R5A1,
            DXGIFormat.B8G8R8A8UNorm        => TexFile.TextureFormat.B8G8R8A8,
            DXGIFormat.B8G8R8X8UNorm        => TexFile.TextureFormat.B8G8R8X8,
            DXGIFormat.R32Float             => TexFile.TextureFormat.R32F,
            DXGIFormat.R16G16Float          => TexFile.TextureFormat.R16G16F,
            DXGIFormat.R32G32Float          => TexFile.TextureFormat.R32G32F,
            DXGIFormat.R16G16B16A16Float    => TexFile.TextureFormat.R16G16B16A16F,
            DXGIFormat.R32G32B32A32Float    => TexFile.TextureFormat.R32G32B32A32F,
            DXGIFormat.BC1UNorm             => TexFile.TextureFormat.BC1,
            DXGIFormat.BC2UNorm             => TexFile.TextureFormat.BC2,
            DXGIFormat.BC3UNorm             => TexFile.TextureFormat.BC3,
            DXGIFormat.BC5UNorm             => TexFile.TextureFormat.BC5,
            DXGIFormat.BC7UNorm             => TexFile.TextureFormat.BC7,
            DXGIFormat.R16G16B16A16Typeless => TexFile.TextureFormat.D16,
            DXGIFormat.R24G8Typeless        => TexFile.TextureFormat.D24S8,
            DXGIFormat.R16Typeless          => TexFile.TextureFormat.Shadow16,
            _                               => TexFile.TextureFormat.Unknown,
        };

    public static DXGIFormat ToDXGI(this TexFile.TextureFormat format)
        => format switch
        {
            TexFile.TextureFormat.L8            => DXGIFormat.R8UNorm,
            TexFile.TextureFormat.A8            => DXGIFormat.A8UNorm,
            TexFile.TextureFormat.B4G4R4A4      => DXGIFormat.B4G4R4A4UNorm,
            TexFile.TextureFormat.B5G5R5A1      => DXGIFormat.B5G5R5A1UNorm,
            TexFile.TextureFormat.B8G8R8A8      => DXGIFormat.B8G8R8A8UNorm,
            TexFile.TextureFormat.B8G8R8X8      => DXGIFormat.B8G8R8X8UNorm,
            TexFile.TextureFormat.R32F          => DXGIFormat.R32Float,
            TexFile.TextureFormat.R16G16F       => DXGIFormat.R16G16Float,
            TexFile.TextureFormat.R32G32F       => DXGIFormat.R32G32Float,
            TexFile.TextureFormat.R16G16B16A16F => DXGIFormat.R16G16B16A16Float,
            TexFile.TextureFormat.R32G32B32A32F => DXGIFormat.R32G32B32A32Float,
            TexFile.TextureFormat.BC1           => DXGIFormat.BC1UNorm,
            TexFile.TextureFormat.BC2           => DXGIFormat.BC2UNorm,
            TexFile.TextureFormat.BC3           => DXGIFormat.BC3UNorm,
            TexFile.TextureFormat.BC5           => DXGIFormat.BC5UNorm,
            TexFile.TextureFormat.BC7           => DXGIFormat.BC7UNorm,
            TexFile.TextureFormat.D16           => DXGIFormat.R16G16B16A16Typeless,
            TexFile.TextureFormat.D24S8         => DXGIFormat.R24G8Typeless,
            TexFile.TextureFormat.Shadow16      => DXGIFormat.R16Typeless,
            TexFile.TextureFormat.Shadow24      => DXGIFormat.R24G8Typeless,
            _                                   => DXGIFormat.Unknown,
        };
}
