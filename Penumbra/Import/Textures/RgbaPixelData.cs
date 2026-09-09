namespace Penumbra.Import.Textures;

public readonly record struct RgbaPixelData(int Width, int Height, byte[] PixelData)
{
    public static readonly RgbaPixelData Empty = new(0, 0, []);

    public (int Width, int Height) Size
        => (Width, Height);

    public RgbaPixelData((int Width, int Height) size, byte[] pixelData)
        : this(size.Width, size.Height, pixelData)
    { }

    public CustomBitmap ToImage()
        => CustomBitmap.FromPixelData(this);

    public RgbaPixelData Resize((int Width, int Height) size)
        => CustomBitmap.Resize(this, size);

    public static byte[] NewPixelData((int Width, int Height) size)
        => new byte[size.Width * size.Height * 4];

    public static RgbaPixelData FromTexture(Texture texture)
        => new(texture.TextureWrap!.Width, texture.TextureWrap!.Height, texture.RgbaPixels);
}
