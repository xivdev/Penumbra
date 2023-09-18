using Dalamud.Interface;
using Dalamud.Plugin.Services;
using ImGuiScene;
using Lumina.Data.Files;
using OtterGui.Log;
using OtterGui.Tasks;
using OtterTex;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Penumbra.Import.Textures;

public sealed class TextureManager : SingleTaskQueue, IDisposable
{
    private readonly Logger       _logger;
    private readonly UiBuilder    _uiBuilder;
    private readonly IDataManager _gameData;

    private readonly ConcurrentDictionary<IAction, (Task, CancellationTokenSource)> _tasks    = new();
    private          bool                                                           _disposed = false;

    public TextureManager(UiBuilder uiBuilder, IDataManager gameData, Logger logger)
    {
        _uiBuilder = uiBuilder;
        _gameData  = gameData;
        _logger    = logger;
    }

    public IReadOnlyDictionary<IAction, (Task, CancellationTokenSource)> Tasks
        => _tasks;

    public void Dispose()
    {
        _disposed = true;
        foreach (var (_, cancel) in _tasks.Values.ToArray())
            cancel.Cancel();
        _tasks.Clear();
    }

    public Task SavePng(string input, string output)
        => Enqueue(new SavePngAction(this, input, output));

    public Task SavePng(BaseImage image, string path, byte[]? rgba = null, int width = 0, int height = 0)
        => Enqueue(new SavePngAction(this, image, path, rgba, width, height));

    public Task SaveAs(CombinedTexture.TextureSaveType type, bool mipMaps, bool asTex, string input, string output)
        => Enqueue(new SaveAsAction(this, type, mipMaps, asTex, input, output));

    public Task SaveAs(CombinedTexture.TextureSaveType type, bool mipMaps, bool asTex, BaseImage image, string path, byte[]? rgba = null,
        int width = 0, int height = 0)
        => Enqueue(new SaveAsAction(this, type, mipMaps, asTex, image, path, rgba, width, height));

    private Task Enqueue(IAction action)
    {
        if (_disposed)
            return Task.FromException(new ObjectDisposedException(nameof(TextureManager)));

        Task t;
        lock (_tasks)
        {
            t = _tasks.GetOrAdd(action, a =>
            {
                var token = new CancellationTokenSource();
                var task  = Enqueue(a, token.Token);
                task.ContinueWith(_ => _tasks.TryRemove(a, out var unused), CancellationToken.None);
                return (task, token);
            }).Item1;
        }

        return t;
    }

    private class SavePngAction : IAction
    {
        private readonly TextureManager _textures;
        private readonly string         _outputPath;
        private readonly ImageInputData _input;

        public SavePngAction(TextureManager textures, string input, string output)
        {
            _textures   = textures;
            _input      = new ImageInputData(input);
            _outputPath = output;
        }

        public SavePngAction(TextureManager textures, BaseImage image, string path, byte[]? rgba = null, int width = 0, int height = 0)
        {
            _textures   = textures;
            _input      = new ImageInputData(image, rgba, width, height);
            _outputPath = path;
        }

        public void Execute(CancellationToken cancel)
        {
            _textures._logger.Information($"[{nameof(TextureManager)}] Saving {_input} as .png to {_outputPath}...");
            var (image, rgba, width, height) = _input.GetData(_textures);
            cancel.ThrowIfCancellationRequested();
            Image<Rgba32>? png = null;
            if (image.Type is TextureType.Unknown)
            {
                if (rgba != null && width > 0 && height > 0)
                    png = ConvertToPng(rgba, width, height).AsPng!;
            }
            else
            {
                png = ConvertToPng(image, cancel, rgba).AsPng!;
            }

            cancel.ThrowIfCancellationRequested();
            png?.SaveAsync(_outputPath, new PngEncoder() { CompressionLevel = PngCompressionLevel.NoCompression }, cancel).Wait(cancel);
        }

        public override string ToString()
            => $"{_input} to {_outputPath} PNG";

        public bool Equals(IAction? other)
        {
            if (other is not SavePngAction rhs)
                return false;

            return string.Equals(_outputPath, rhs._outputPath, StringComparison.OrdinalIgnoreCase) && _input.Equals(rhs._input);
        }

        public override int GetHashCode()
            => HashCode.Combine(_outputPath.ToLowerInvariant(), _input);
    }

    private class SaveAsAction : IAction
    {
        private readonly TextureManager                  _textures;
        private readonly string                          _outputPath;
        private readonly ImageInputData                  _input;
        private readonly CombinedTexture.TextureSaveType _type;
        private readonly bool                            _mipMaps;
        private readonly bool                            _asTex;

        public SaveAsAction(TextureManager textures, CombinedTexture.TextureSaveType type, bool mipMaps, bool asTex, string input,
            string output)
        {
            _textures   = textures;
            _input      = new ImageInputData(input);
            _outputPath = output;
            _type       = type;
            _mipMaps    = mipMaps;
            _asTex      = asTex;
        }

        public SaveAsAction(TextureManager textures, CombinedTexture.TextureSaveType type, bool mipMaps, bool asTex, BaseImage image,
            string path, byte[]? rgba = null, int width = 0, int height = 0)
        {
            _textures   = textures;
            _input      = new ImageInputData(image, rgba, width, height);
            _outputPath = path;
            _type       = type;
            _mipMaps    = mipMaps;
            _asTex      = asTex;
        }

        public void Execute(CancellationToken cancel)
        {
            _textures._logger.Information(
                $"[{nameof(TextureManager)}] Saving {_input} as {_type} {(_asTex ? ".tex" : ".dds")} file{(_mipMaps ? " with mip maps" : string.Empty)} to {_outputPath}...");
            var (image, rgba, width, height) = _input.GetData(_textures);
            if (image.Type is TextureType.Unknown)
            {
                if (rgba != null && width > 0 && height > 0)
                    image = ConvertToDds(rgba, width, height);
                else
                    return;
            }

            var dds = _type switch
            {
                CombinedTexture.TextureSaveType.AsIs when image.Type is TextureType.Png => ConvertToRgbaDds(image, _mipMaps, cancel, rgba,
                    width, height),
                CombinedTexture.TextureSaveType.AsIs when image.Type is TextureType.Dds => AddMipMaps(image.AsDds!, _mipMaps),
                CombinedTexture.TextureSaveType.Bitmap => ConvertToRgbaDds(image, _mipMaps, cancel, rgba, width, height),
                CombinedTexture.TextureSaveType.BC3 => ConvertToCompressedDds(image, _mipMaps, false, cancel, rgba, width, height),
                CombinedTexture.TextureSaveType.BC7 => ConvertToCompressedDds(image, _mipMaps, true, cancel, rgba, width, height),
                _ => throw new Exception("Wrong save type."),
            };

            cancel.ThrowIfCancellationRequested();
            if (_asTex)
                SaveTex(_outputPath, dds.AsDds!);
            else
                dds.AsDds!.SaveDDS(_outputPath);
        }

        public override string ToString()
            => $"{_input} to {_outputPath} {_type} {(_asTex ? "TEX" : "DDS")}{(_mipMaps ? " with MipMaps" : string.Empty)}";

        public bool Equals(IAction? other)
        {
            if (other is not SaveAsAction rhs)
                return false;

            return _type == rhs._type
             && _mipMaps == rhs._mipMaps
             && _asTex == rhs._asTex
             && string.Equals(_outputPath, rhs._outputPath, StringComparison.OrdinalIgnoreCase)
             && _input.Equals(rhs._input);
        }

        public override int GetHashCode()
            => HashCode.Combine(_outputPath.ToLowerInvariant(), _type, _mipMaps, _asTex, _input);
    }

    /// <summary> Load a texture wrap for a given image. </summary>
    public TextureWrap LoadTextureWrap(BaseImage image, byte[]? rgba = null, int width = 0, int height = 0)
    {
        (rgba, width, height) = GetData(image, rgba, width, height);
        return LoadTextureWrap(rgba, width, height);
    }

    /// <summary> Load a texture wrap for a given image. </summary>
    public TextureWrap LoadTextureWrap(byte[] rgba, int width, int height)
        => _uiBuilder.LoadImageRaw(rgba, width, height, 4);

    /// <summary> Load any supported file from game data or drive depending on extension and if the path is rooted. </summary>
    public (BaseImage, TextureType) Load(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".dds" => (LoadDds(path), TextureType.Dds),
            ".png" => (LoadPng(path), TextureType.Png),
            ".tex" => (LoadTex(path), TextureType.Tex),
            _      => throw new Exception($"Extension {Path.GetExtension(path)} unknown."),
        };

    /// <summary> Load a .tex file from game data or drive depending on if the path is rooted. </summary>
    public BaseImage LoadTex(string path)
    {
        using var stream = OpenTexStream(path);
        return TexFileParser.Parse(stream);
    }

    /// <summary> Load a .dds file from drive using OtterTex. </summary>
    public BaseImage LoadDds(string path)
        => ScratchImage.LoadDDS(path);

    /// <summary> Load a .png file from drive using ImageSharp. </summary>
    public BaseImage LoadPng(string path)
    {
        using var stream = File.OpenRead(path);
        return Image.Load<Rgba32>(stream);
    }

    /// <summary> Convert an existing image to .png. Does not create a deep copy of an existing .png and just returns the existing one. </summary>
    public static BaseImage ConvertToPng(BaseImage input, CancellationToken cancel, byte[]? rgba = null, int width = 0, int height = 0)
    {
        switch (input.Type)
        {
            case TextureType.Png: return input;
            case TextureType.Dds:
            {
                (rgba, width, height) = GetData(input, rgba, width, height);
                cancel.ThrowIfCancellationRequested();
                return ConvertToPng(rgba, width, height);
            }
            default: return new BaseImage();
        }
    }

    /// <summary> Convert an existing image to a RGBA32 .dds. Does not create a deep copy of an existing RGBA32 dds and just returns the existing one. </summary>
    public static BaseImage ConvertToRgbaDds(BaseImage input, bool mipMaps, CancellationToken cancel, byte[]? rgba = null, int width = 0,
        int height = 0)
    {
        switch (input.Type)
        {
            case TextureType.Png:
            {
                (rgba, width, height) = GetData(input, rgba, width, height);
                cancel.ThrowIfCancellationRequested();
                var dds = ConvertToDds(rgba, width, height).AsDds!;
                cancel.ThrowIfCancellationRequested();
                return AddMipMaps(dds, mipMaps);
            }
            case TextureType.Dds:
            {
                var scratch = input.AsDds!;
                if (rgba == null)
                    return CreateUncompressed(scratch, mipMaps, cancel);

                (rgba, width, height) = GetData(input, rgba, width, height);
                cancel.ThrowIfCancellationRequested();
                var dds = ConvertToDds(rgba, width, height).AsDds!;
                cancel.ThrowIfCancellationRequested();
                return AddMipMaps(dds, mipMaps);
            }
            default: return new BaseImage();
        }
    }

    /// <summary> Convert an existing image to a block compressed .dds. Does not create a deep copy of an existing dds of the correct format and just returns the existing one. </summary>
    public static BaseImage ConvertToCompressedDds(BaseImage input, bool mipMaps, bool bc7, CancellationToken cancel, byte[]? rgba = null,
        int width = 0, int height = 0)
    {
        switch (input.Type)
        {
            case TextureType.Png:
            {
                (rgba, width, height) = GetData(input, rgba, width, height);
                cancel.ThrowIfCancellationRequested();
                var dds = ConvertToDds(rgba, width, height).AsDds!;
                cancel.ThrowIfCancellationRequested();
                return CreateCompressed(dds, mipMaps, bc7, cancel);
            }
            case TextureType.Dds:
            {
                var scratch = input.AsDds!;
                return CreateCompressed(scratch, mipMaps, bc7, cancel);
            }
            default: return new BaseImage();
        }
    }

    public static BaseImage ConvertToPng(byte[] rgba, int width, int height)
        => Image.LoadPixelData<Rgba32>(rgba, width, height);

    public static BaseImage ConvertToDds(byte[] rgba, int width, int height)
    {
        var scratch = ScratchImage.FromRGBA(rgba, width, height, out var i).ThrowIfError(i);
        return scratch.Convert(DXGIFormat.B8G8R8A8UNorm);
    }

    public bool GameFileExists(string path)
        => _gameData.FileExists(path);

    /// <summary> Add up to 13 mip maps to the input if mip maps is true, otherwise return input. </summary>
    public static ScratchImage AddMipMaps(ScratchImage input, bool mipMaps)
    {
        var numMips = mipMaps ? Math.Min(13, 1 + BitOperations.Log2((uint)Math.Max(input.Meta.Width, input.Meta.Height))) : 1;
        if (numMips == input.Meta.MipLevels)
            return input;

        var flags = (Dalamud.Utility.Util.IsLinux() ? FilterFlags.ForceNonWIC : 0) | FilterFlags.SeparateAlpha;
        var ec    = input.GenerateMipMaps(out var ret, numMips, flags);
        if (ec != ErrorCode.Ok)
            throw new Exception(
                $"Could not create the requested {numMips} mip maps (input has {input.Meta.MipLevels}) with flags [{flags}], maybe retry with the top-right checkbox unchecked:\n{ec}");

        return ret;
    }

    /// <summary> Create an uncompressed .dds (optionally with mip maps) from the input. Returns input (+ mipmaps) if it is already uncompressed. </summary>
    public static ScratchImage CreateUncompressed(ScratchImage input, bool mipMaps, CancellationToken cancel)
    {
        if (input.Meta.Format == DXGIFormat.B8G8R8A8UNorm)
            return AddMipMaps(input, mipMaps);

        input = input.Meta.Format.IsCompressed()
            ? input.Decompress(DXGIFormat.B8G8R8A8UNorm)
            : input.Convert(DXGIFormat.B8G8R8A8UNorm);
        cancel.ThrowIfCancellationRequested();
        return AddMipMaps(input, mipMaps);
    }

    /// <summary> Create a BC3 or BC7 block-compressed .dds from the input (optionally with mipmaps). Returns input (+ mipmaps) if it is already the correct format. </summary>
    public static ScratchImage CreateCompressed(ScratchImage input, bool mipMaps, bool bc7, CancellationToken cancel)
    {
        var format = bc7 ? DXGIFormat.BC7UNorm : DXGIFormat.BC3UNorm;
        if (input.Meta.Format == format)
            return input;

        if (input.Meta.Format.IsCompressed())
        {
            input = input.Decompress(DXGIFormat.B8G8R8A8UNorm);
            cancel.ThrowIfCancellationRequested();
        }

        input = AddMipMaps(input, mipMaps);
        cancel.ThrowIfCancellationRequested();
        return input.Compress(format, CompressFlags.BC7Quick | CompressFlags.Parallel);
    }


    /// <summary> Load a tex file either from game data if the path is not rooted, or from drive if it is rooted.</summary>
    private Stream OpenTexStream(string path)
    {
        if (Path.IsPathRooted(path))
            return File.OpenRead(path);

        var file = _gameData.GetFile(path);
        return file != null ? new MemoryStream(file.Data) : throw new Exception($"Unable to obtain \"{path}\" from game files.");
    }

    /// <summary> Obtain the checked rgba data, width and height for an image. </summary>
    private static (byte[], int, int) GetData(BaseImage input, byte[]? rgba, int width, int height)
    {
        if (rgba == null)
            return input.GetPixelData();

        if (width == 0 || height == 0)
            (width, height) = input.Dimensions;
        return width * height * 4 != rgba.Length
            ? input.GetPixelData()
            : (rgba, width, height);
    }

    /// <summary> Save a .dds file as .tex file with appropriately changed header. </summary>
    public static void SaveTex(string path, ScratchImage input)
    {
        var header = input.ToTexHeader();
        if (header.Format == TexFile.TextureFormat.Unknown)
            throw new Exception($"Could not save tex file with format {input.Meta.Format}, not convertible to a valid .tex format.");

        using var stream = File.Open(path, File.Exists(path) ? FileMode.Truncate : FileMode.CreateNew);
        using var w      = new BinaryWriter(stream);
        header.Write(w);
        w.Write(input.Pixels);
        // Necessary due to the GC being allowed to collect after the last invocation of an object,
        // thus invalidating the ReadOnlySpan.
        GC.KeepAlive(input);
    }

    private readonly struct ImageInputData
    {
        private readonly string? _inputPath;

        private readonly BaseImage _image;
        private readonly byte[]?   _rgba;
        private readonly int       _width;
        private readonly int       _height;

        public ImageInputData(string inputPath)
        {
            _inputPath = inputPath;
            _image     = new BaseImage();
            _rgba      = null;
            _width     = 0;
            _height    = 0;
        }

        public ImageInputData(BaseImage image, byte[]? rgba = null, int width = 0, int height = 0)
        {
            _inputPath = null;
            _image     = image.Width == 0 || image.Height == 0 ? new BaseImage() : image;
            _rgba      = rgba?.ToArray();
            _width     = width;
            _height    = height;
        }

        public (BaseImage Image, byte[]? Rgba, int Width, int Height) GetData(TextureManager textures)
        {
            if (_inputPath == null)
                return (_image, _rgba, _width, _height);

            if (!File.Exists(_inputPath))
                throw new FileNotFoundException($"Input texture file {_inputPath} not Found.", _inputPath);

            var (image, _) = textures.Load(_inputPath);
            return (image, null, 0, 0);
        }

        public bool Equals(ImageInputData rhs)
        {
            if (_inputPath != null)
                return string.Equals(_inputPath, rhs._inputPath, StringComparison.OrdinalIgnoreCase);

            if (rhs._inputPath != null)
                return false;

            if (_image.Image != null)
                return ReferenceEquals(_image.Image, rhs._image.Image);

            return _width == rhs._width && _height == rhs._height && _rgba != null && rhs._rgba != null && _rgba.SequenceEqual(rhs._rgba);
        }

        public override string ToString()
            => _inputPath
             ?? _image.Type switch
                {
                    TextureType.Unknown => $"Custom {_width} x {_height} RGBA Image",
                    TextureType.Dds     => $"Custom {_width} x {_height} {_image.Format} Image",
                    TextureType.Tex     => $"Custom {_width} x {_height} {_image.Format} Image",
                    TextureType.Png     => $"Custom {_width} x {_height} .png Image",
                    TextureType.Bitmap  => $"Custom {_width} x {_height} RGBA Image",
                    _                   => "Unknown Image",
                };

        public override int GetHashCode()
            => _inputPath != null ? _inputPath.ToLowerInvariant().GetHashCode() : HashCode.Combine(_width, _height);
    }
}
