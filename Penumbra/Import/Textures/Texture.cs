using ImGuiScene;
using OtterTex;

namespace Penumbra.Import.Textures;

public enum TextureType
{
    Unknown,
    Dds,
    Tex,
    Png,
    Bitmap,
}

public sealed class Texture : IDisposable
{
    // Path to the file we tried to load.
    public string Path = string.Empty;

    // Path for changing paths.
    internal string? TmpPath;

    // If the load failed, an exception is stored.
    public Exception? LoadError = null;

    // The pixels of the main image in RGBA order.
    // Empty if LoadError != null or Path is empty.
    public byte[] RgbaPixels = Array.Empty<byte>();

    // The ImGui wrapper to load the image.
    // null if LoadError != null or Path is empty.
    public TextureWrap? TextureWrap = null;

    // The base image in whatever format it has.
    public BaseImage BaseImage;

    // Original File Type.
    public TextureType Type = TextureType.Unknown;

    // Whether the file is successfully loaded and drawable.
    public bool IsLoaded
        => TextureWrap != null;

    public DXGIFormat Format
        => BaseImage.Format;

    public int MipMaps
        => BaseImage.MipMaps;

    private void Clean()
    {
        RgbaPixels = Array.Empty<byte>();
        TextureWrap?.Dispose();
        TextureWrap = null;
        BaseImage.Dispose();
        BaseImage = new BaseImage();
        Type      = TextureType.Unknown;
        Loaded?.Invoke(false);
    }

    public void Dispose()
        => Clean();

    public event Action<bool>? Loaded;

    public void Load(TextureManager textures, string path)
    {
        TmpPath = null;
        if (path == Path)
            return;

        Path = path;
        Clean();
        if (path.Length == 0)
            return;

        try
        {
            (BaseImage, Type)                   = textures.Load(path);
            (RgbaPixels, var width, var height) = BaseImage.GetPixelData();
            TextureWrap                         = textures.LoadTextureWrap(BaseImage, RgbaPixels);
            Loaded?.Invoke(true);
        }
        catch (Exception e)
        {
            LoadError = e;
            Clean();
        }
    }

    public void Reload(TextureManager textures)
    {
        var path = Path;
        Path = string.Empty;
        Load(textures, path);
    }
}
