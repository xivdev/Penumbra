global using CustomBitmap = SkiaSharp.SKBitmap;
using Luna.Targa;
using OtterTex;
using SkiaSharp;
using TargaImage = Luna.Targa.Structs.Image;

namespace Penumbra.Import.Textures;

public static class CustomBitmapExtensions
{
    private static readonly SKPngEncoderOptions PngEncoder     = new(SKPngEncoderFilterFlags.NoFilters, 0);
    private static readonly SKSamplingOptions   ResizeSampling = new(SKCubicResampler.CatmullRom);

    private static SKImageInfo ImageInfo(int width, int height)
        => new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

    extension(CustomBitmap bitmap)
    {
        public static DXGIFormat Dxgi
            => DXGIFormat.R8G8B8A8UNorm;

        public (int Width, int Height) Dimensions
            => (bitmap.Width, bitmap.Height);

        public (byte[] Data, int Width, int Height) GetPixelData()
        {
            var       ret    = RgbaPixelData.NewPixelData(bitmap.Dimensions);
            using var source = bitmap.PeekPixels();
            // ReadPixels also handles row padding and converts externally supplied bitmaps to straight-alpha RGBA.
            unsafe
            {
                fixed (byte* pixels = ret)
                {
                    if (source is null || !source.ReadPixels(ImageInfo(bitmap.Width, bitmap.Height), (nint)pixels, bitmap.Width * 4, 0, 0))
                        throw new InvalidDataException("Could not read bitmap pixels.");
                }
            }

            return (ret, bitmap.Width, bitmap.Height);
        }

        public static CustomBitmap FromPixelData(in RgbaPixelData data)
        {
            if ((long)data.Width * data.Height * 4 != data.PixelData.Length)
                throw new ArgumentException("Expected width * height * 4 RGBA bytes.", nameof(data));

            var image = new CustomBitmap(ImageInfo(data.Width, data.Height));
            data.PixelData.AsSpan().CopyTo(image.GetPixelSpan());
            return image;
        }

        public static RgbaPixelData Resize(in RgbaPixelData data, (int Width, int Height) size)
        {
            if (data.Width == size.Width && data.Height == size.Height)
                return data;

            using var image = data.ToImage();
            using var resized = image.Resize(ImageInfo(size.Width, size.Height), ResizeSampling)
             ?? throw new InvalidOperationException("Could not resize bitmap.");
            var (pixels, width, height) = resized.GetPixelData();
            return new RgbaPixelData(width, height, pixels);
        }

        public static CustomBitmap CreateDummy()
        {
            var image = new CustomBitmap(ImageInfo(1, 1));
            image.Erase(SKColors.White);
            return image;
        }

        public static CustomBitmap FromStream(Stream stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var       bytes   = buffer.ToArray();
            using var encoded = SKData.CreateCopy(bytes);
            using var codec   = SKCodec.Create(encoded);
            if (codec is null)
            {
                var targa = new TargaReader(bytes);
                return CustomBitmap.FromPixelData(new RgbaPixelData(targa.Header.Image.Width, targa.Header.Image.Height,
                    targa.CreateRgba32Data()));
            }

            var image  = new CustomBitmap(ImageInfo(codec.Info.Width, codec.Info.Height));
            var result = codec.GetPixels(image.Info, image.GetPixels());
            if (result is SKCodecResult.Success)
                return image;

            image.Dispose();
            throw new InvalidDataException($"Could not decode image: {result}.");
        }

        /// <summary> Encode this bitmap as a compressed PNG. </summary>
        public byte[] EncodePng()
        {
            using var pixels = bitmap.PeekPixels();
            using var encoded = pixels?.Encode(SKPngEncoderOptions.Default)
             ?? throw new InvalidDataException("Could not encode bitmap as PNG.");
            return encoded.ToArray();
        }

        public async Task SaveFile(string path, TextureType type, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            await using var stream = File.Create(path);
            await bitmap.SaveStream(stream, type, cancel);
        }

        public async Task SaveStream(Stream stream, TextureType type, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            // Encode before writing so cancellation and asynchronous I/O retain the existing stream contract.
            using var buffer = new MemoryStream();
            switch (type)
            {
                case TextureType.Png:
                    using (var pixels = bitmap.PeekPixels())
                    {
                        if (pixels is null || !pixels.Encode(buffer, PngEncoder))
                            throw new InvalidDataException("Could not encode bitmap as PNG.");
                    }

                    break;
                case TextureType.Targa:
                    var (data, width, height) = bitmap.GetPixelData();
                    TargaWriter.WriteColorMapped(buffer, new TargaImage(width, height, data), encodeRunLength: true);
                    break;
                default: throw new NotImplementedException($"No encoder defined for {type}");
            }

            cancel.ThrowIfCancellationRequested();
            buffer.Position = 0;
            await buffer.CopyToAsync(stream, cancel);
        }
    }
}
