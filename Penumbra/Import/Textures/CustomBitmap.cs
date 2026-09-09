global using CustomBitmap = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using OtterTex;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace Penumbra.Import.Textures;

public static class CustomBitmapExtensions
{
    private static readonly IImageEncoder PngEncoder = new PngEncoder
    {
        CompressionLevel = PngCompressionLevel.NoCompression,
    };

    private static readonly IImageEncoder TgaEncoder = new TgaEncoder
    {
        Compression  = TgaCompression.None,
        BitsPerPixel = TgaBitsPerPixel.Pixel32,
    };

    private static IImageEncoder Encoder(TextureType type)
        => type switch
        {
            TextureType.Png   => PngEncoder,
            TextureType.Targa => TgaEncoder,
            _                 => throw new NotImplementedException($"No encoder defined for {type}"),
        };

    extension(CustomBitmap bitmap)
    {
        public static DXGIFormat Dxgi
            => DXGIFormat.B8G8R8A8UNorm;

        public (int Width, int Height) Dimensions
            => (bitmap.Width, bitmap.Height);

        public (byte[] Data, int Width, int Height) GetPixelData()
        {
            var ret = new byte[bitmap.Height * bitmap.Width * 4];
            bitmap.CopyPixelDataTo(ret);
            return (ret, bitmap.Width, bitmap.Height);
        }

        public static CustomBitmap FromPixelData(in RgbaPixelData data)
            => Image.LoadPixelData<Rgba32>(data.PixelData, data.Width, data.Height);

        public static RgbaPixelData Resize(in RgbaPixelData data, (int Width, int Height) size)
        {
            if (data.Width == size.Width && data.Height == size.Height)
                return data;

            var       result = new RgbaPixelData(size, RgbaPixelData.NewPixelData(size));
            using var image  = data.ToImage();
            image.Mutate(ctx => ctx.Resize(size.Width, size.Height, KnownResamplers.Lanczos3));
            image.CopyPixelDataTo(result.PixelData);

            return result;
        }

        public static CustomBitmap CreateDummy()
        {
            var image = new CustomBitmap(1, 1);
            image[0, 0] = Color.White;
            return image;
        }

        public static CustomBitmap FromStream(Stream stream)
            => Image.Load<Rgba32>(stream);

        public Task SaveFile(string path, TextureType type, CancellationToken cancel)
            => bitmap.SaveAsync(path, Encoder(type), cancel);

        public Task SaveStream(Stream stream, TextureType type, CancellationToken cancel)
            => bitmap.SaveAsync(stream, Encoder(type), cancel);
    }
}
