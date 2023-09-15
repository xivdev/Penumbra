namespace Penumbra.Import.Textures;

public partial class CombinedTexture : IDisposable
{
    public enum TextureSaveType
    {
        AsIs,
        Bitmap,
        BC3,
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
        => _mode != Mode.Empty;

    public bool IsLeftCopy
        => _mode == Mode.LeftCopy;

    public void Draw(TextureManager textures, Vector2 size)
    {
        if (_mode == Mode.Custom && !_centerStorage.IsLoaded)
        {
            var (width, height)        = CombineImage();
            _centerStorage.TextureWrap = textures.LoadTextureWrap(_centerStorage.RgbaPixels, width, height);
        }

        if (_current != null)
            TextureDrawer.Draw(_current, size);
    }


    public void SaveAsPng(TextureManager textures, string path)
    {
        if (!IsLoaded || _current == null)
            return;

        SaveTask = textures.SavePng(_current.BaseImage, path, _current.RgbaPixels, _current.TextureWrap!.Width, _current.TextureWrap!.Height);
    }

    private void SaveAs(TextureManager textures, string path, TextureSaveType type, bool mipMaps, bool writeTex)
    {
        if (!IsLoaded || _current == null)
            return;

        SaveTask = textures.SaveAs(type, mipMaps, writeTex, _current.BaseImage, path, _current.RgbaPixels, _current.TextureWrap!.Width,
            _current.TextureWrap!.Height);
    }

    public void SaveAs(TextureType? texType, TextureManager textures, string path, TextureSaveType type, bool mipMaps)
    {
        var finalTexType = texType
         ?? Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".tex" => TextureType.Tex,
                ".dds" => TextureType.Dds,
                ".png" => TextureType.Png,
                _      => TextureType.Unknown,
            };

        switch (finalTexType)
        {
            case TextureType.Tex:
                SaveAsTex(textures, path, type, mipMaps);
                break;
            case TextureType.Dds:
                SaveAsDds(textures, path, type, mipMaps);
                break;
            case TextureType.Png:
                SaveAsPng(textures, path);
                break;
            default:
                throw new ArgumentException(
                    $"Cannot save texture as TextureType {finalTexType} with extension {Path.GetExtension(path).ToLowerInvariant()}");
        }
    }

    public void SaveAsTex(TextureManager textures, string path, TextureSaveType type, bool mipMaps)
        => SaveAs(textures, path, type, mipMaps, true);

    public void SaveAsDds(TextureManager textures, string path, TextureSaveType type, bool mipMaps)
        => SaveAs(textures, path, type, mipMaps, false);


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
}
