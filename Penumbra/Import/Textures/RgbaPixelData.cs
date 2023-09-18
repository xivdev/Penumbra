using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Penumbra.Import.Textures;

public readonly record struct RgbaPixelData(int Width, int Height, byte[] PixelData)
{
    public static readonly RgbaPixelData Empty = new(0, 0, Array.Empty<byte>());

    public (int Width, int Height) Size
        => (Width, Height);

    public RgbaPixelData((int Width, int Height) size, byte[] pixelData)
        : this(size.Width, size.Height, pixelData)
    { }

    public Image<Rgba32> ToImage()
        => Image.LoadPixelData<Rgba32>(PixelData, Width, Height);

    public RgbaPixelData Resize((int Width, int Height) size)
    {
        if (Width == size.Width && Height == size.Height)
            return this;

        var result = new RgbaPixelData(size, NewPixelData(size));
        using (var image = ToImage())
        {
            image.Mutate(ctx => ctx.Resize(size.Width, size.Height, KnownResamplers.Lanczos3));
            image.CopyPixelDataTo(result.PixelData);
        }

        return result;
    }

    public static byte[] NewPixelData((int Width, int Height) size)
        => new byte[size.Width * size.Height * 4];

    public static RgbaPixelData FromTexture(Texture texture)
        => new(texture.TextureWrap!.Width, texture.TextureWrap!.Height, texture.RgbaPixels);
}
