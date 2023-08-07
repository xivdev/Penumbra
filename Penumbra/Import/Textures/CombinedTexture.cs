using System;
using System.Numerics;

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

    public Guid SaveGuid { get; private set; } = Guid.Empty;

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

        SaveGuid = textures.SavePng(_current.BaseImage, path, _current.RgbaPixels, _current.TextureWrap!.Width, _current.TextureWrap!.Height);
    }

    private void SaveAs(TextureManager textures, string path, TextureSaveType type, bool mipMaps, bool writeTex)
    {
        if (!IsLoaded || _current == null)
            return;

        SaveGuid = textures.SaveAs(type, mipMaps, writeTex, _current.BaseImage, path, _current.RgbaPixels, _current.TextureWrap!.Width,
            _current.TextureWrap!.Height);
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
        if (_left.IsLoaded)
        {
            if (_right.IsLoaded)
            {
                _current = _centerStorage;
                _mode    = Mode.Custom;
            }
            else if (!_invertLeft && _multiplierLeft.IsIdentity)
            {
                _mode    = Mode.LeftCopy;
                _current = _left;
            }
            else
            {
                _current = _centerStorage;
                _mode    = Mode.Custom;
            }
        }
        else if (_right.IsLoaded)
        {
            if (!_invertRight && _multiplierRight.IsIdentity)
            {
                _current = _right;
                _mode    = Mode.RightCopy;
            }
            else
            {
                _current = _centerStorage;
                _mode    = Mode.Custom;
            }
        }
    }

    private void Clean()
    {
        _centerStorage.Dispose();
        _current = null;
        SaveGuid = Guid.Empty;
        _mode    = Mode.Empty;
    }
}
