using ImSharp;

namespace Penumbra.Import.Textures;

public partial class CombinedTexture : IDisposable
{
    public enum TextureSaveType
    {
        AsIs,
        Bitmap,
        BC1,
        BC3,
        BC4,
        BC5,
        BC7,
    }

    private enum Mode
    {
        Empty,
        LeftCopy,
        RightCopy,
        Custom,
    }

    private readonly Texture _left;
    private readonly Texture _right;

    private Texture? _current;
    private Mode     _mode = Mode.Empty;

    private readonly Texture _centerStorage = new();

    public Task SaveTask { get; private set; } = Task.CompletedTask;

    public bool IsLoaded
        => _mode is not Mode.Empty;

    public bool IsLeftCopy
        => _mode is Mode.LeftCopy;

    public void Draw(TextureManager textures, Vector2 size)
    {
        if (_mode is Mode.Custom && !_centerStorage.IsLoaded)
        {
            var (width, height)        = CombineImage();
            _centerStorage.TextureWrap = textures.LoadTextureWrap(_centerStorage.RgbaPixels, width, height);
        }

        if (_current != null)
            TextureDrawer.Draw(_current, size);
    }


    public void SaveAs(TextureType? texType, TextureManager textures, string path, TextureSaveType type, bool mipMaps)
    {
        if (!IsLoaded || _current is null)
            return;

        var finalTexType = texType ?? TextureManager.GetTextureTypeForPath(path);

        SaveTask = finalTexType switch
        {
            TextureType.Tex => textures.SaveAs(type, mipMaps, true, _current.BaseImage, path, _current.RgbaPixels, _current.TextureWrap!.Width,
                _current.TextureWrap!.Height),
            TextureType.Dds => textures.SaveAs(type, mipMaps, false, _current.BaseImage, path, _current.RgbaPixels, _current.TextureWrap!.Width,
                _current.TextureWrap!.Height),
            TextureType.Png => textures.SavePng(_current.BaseImage, path, _current.RgbaPixels, _current.TextureWrap!.Width,
                _current.TextureWrap!.Height),
            TextureType.Targa => textures.SaveTga(_current.BaseImage, path, _current.RgbaPixels, _current.TextureWrap!.Width,
                _current.TextureWrap!.Height),
            _ => throw new ArgumentException(
                $"Cannot save texture as TextureType {finalTexType} with extension {Path.GetExtension(path).ToLowerInvariant()}"),
        };
    }

    public Task SaveAs(TextureType texType, TextureManager textures, Stream output, TextureSaveType type, bool mipMaps)
    {
        if (!IsLoaded || _current is null)
            return Task.CompletedTask;

        var task = texType switch
        {
            TextureType.Tex => textures.SaveAs(type, mipMaps, true, _current.BaseImage, output, _current.RgbaPixels,
                _current.TextureWrap!.Width, _current.TextureWrap!.Height),
            TextureType.Dds => textures.SaveAs(type, mipMaps, false, _current.BaseImage, output, _current.RgbaPixels,
                _current.TextureWrap!.Width, _current.TextureWrap!.Height),
            TextureType.Png => textures.SavePng(_current.BaseImage, output, _current.RgbaPixels, _current.TextureWrap!.Width,
                _current.TextureWrap!.Height),
            TextureType.Targa => textures.SaveTga(_current.BaseImage, output, _current.RgbaPixels, _current.TextureWrap!.Width,
                _current.TextureWrap!.Height),
            _ => throw new ArgumentException($"Cannot save texture as TextureType {texType} to stream"),
        };

        SaveTask = task;

        return task;
    }


    public CombinedTexture(Texture left, Texture right)
    {
        _left         =  left;
        _right        =  right;
        _left.Loaded  += OnLoaded;
        _right.Loaded += OnLoaded;
        OnLoaded(false);
    }

    public void Dispose()
    {
        Clean();
        _left.Loaded  -= OnLoaded;
        _right.Loaded -= OnLoaded;
    }

    private void OnLoaded(bool _)
        => Update();

    public void Update()
    {
        Clean();
        switch (GetActualCombineOp())
        {
            case CombineOp.Invalid: break;
            case CombineOp.LeftCopy:
                _mode    = Mode.LeftCopy;
                _current = _left;
                break;
            case CombineOp.RightCopy:
                _mode    = Mode.RightCopy;
                _current = _right;
                break;
            default:
                _mode    = Mode.Custom;
                _current = _centerStorage;
                break;
        }
    }

    private void Clean()
    {
        _centerStorage.Dispose();
        _current = null;
        SaveTask = Task.CompletedTask;
        _mode    = Mode.Empty;
    }

    public bool TryGetRgbaSolidColor(out Rgba32 color, out int width, out int height)
    {
        if (_current is not null)
        {
            width  = _current.TextureWrap?.Width ?? 0;
            height = _current.TextureWrap?.Height ?? 0;
            return _current.TryGetRgbaSolidColor(out color);
        }

        color  = 0u;
        width  = 0;
        height = 0;
        return false;
    }
}
