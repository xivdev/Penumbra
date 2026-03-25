using System.Collections.Frozen;
using Dalamud.Interface.Textures.TextureWraps;
using ImSharp;
using OtterTex;

namespace Penumbra.Import.Textures;

public enum TextureType
{
    Unknown,
    Dds,
    Tex,
    Png,
    Bitmap,
    Targa,
}

internal static class TextureTypeExtensions
{
    public static TextureType ReduceToBehaviour(this TextureType type)
        => type switch
        {
            TextureType.Dds    => TextureType.Dds,
            TextureType.Tex    => TextureType.Tex,
            TextureType.Png    => TextureType.Png,
            TextureType.Bitmap => TextureType.Png,
            TextureType.Targa  => TextureType.Png,
            _                  => TextureType.Unknown,
        };
}

public sealed class Texture : IDisposable
{
    public static readonly FrozenDictionary<Rgba32, StringU8> SolidTextures = (((Rgba32, StringU8)[])
    [
        (0xFFFFFFFF, new StringU8("chara/common/texture/white.tex"u8)),
        (0xFF000000, new StringU8("chara/common/texture/black.tex"u8)),
        (0x00000000, new StringU8("chara/common/texture/transparent.tex"u8)),
        (0xFF000078, new StringU8("chara/common/texture/id_16.tex"u8)),
        (0xFF0000FF, new StringU8("chara/common/texture/red.tex"u8)),
        (0xFF00FF00, new StringU8("chara/common/texture/green.tex"u8)),
        (0xFFFF0000, new StringU8("chara/common/texture/blue.tex"u8)),
        (0xFFFF7F7E, new StringU8("chara/common/texture/null_normal.tex"u8)),
        (0xFF9A74A5, new StringU8("chara/common/texture/skin_mask.tex"u8)),
    ]).ToFrozenDictionary(p => p.Item1, p => p.Item2);

    // Path to the file we tried to load.
    public string Path = string.Empty;

    // Path for changing paths.
    internal string? TmpPath;

    // If the load failed, an exception is stored.
    public Exception? LoadError;

    // The pixels of the main image in RGBA order.
    // Empty if LoadError != null or Path is empty.
    public byte[] RgbaPixels = [];

    // If the main image is a solid rectangle, its color.
    private ColorParameter _rgbaSolidColor = ColorParameter.Default;
    private bool           _isRgbaSolidColorPopulated;

    // The ImGui wrapper to load the image.
    // null if LoadError != null or Path is empty.
    public IDalamudTextureWrap? TextureWrap;

    // The base image in whatever format it has, with all mips kept intact.
    public BaseImage OriginalBaseImage;

    // Level of detail, aka mip level. 0 is the most detailed, higher is less detailed.
    public int LevelOfDetail;

    // The base image in whatever format it has, with most detailed mips removed.
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
        RgbaPixels = [];
        InvalidateRgbaSolidColor();
        TextureWrap?.Dispose();
        TextureWrap = null;
        if (BaseImage != OriginalBaseImage)
            BaseImage.Dispose();
        BaseImage = new BaseImage();
        OriginalBaseImage.Dispose();
        OriginalBaseImage = new BaseImage();
        Type              = TextureType.Unknown;
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
            (OriginalBaseImage, Type) = textures.Load(path);
            LevelOfDetail             = Math.Max(0, Math.Min(OriginalBaseImage.MipMaps - 1, LevelOfDetail));
            BaseImage                 = OriginalBaseImage.AtLevelOfDetail(LevelOfDetail);
            Update(textures);
        }
        catch (Exception e)
        {
            LoadError = e;
            Clean();
        }
    }

    public void SelectLevelOfDetail(TextureManager textures)
    {
        if (OriginalBaseImage.Image is null)
            return;

        LevelOfDetail = Math.Max(0, Math.Min(OriginalBaseImage.MipMaps - 1, LevelOfDetail));
        if (BaseImage != OriginalBaseImage)
            BaseImage.Dispose();
        BaseImage = OriginalBaseImage.AtLevelOfDetail(LevelOfDetail);
        Update(textures);
    }

    private void Update(TextureManager textures)
    {
        (RgbaPixels, _, _) = BaseImage.GetPixelData();
        TextureWrap        = textures.LoadTextureWrap(BaseImage, RgbaPixels);
        InvalidateRgbaSolidColor();
        Loaded?.Invoke(true);
    }

    public void Reload(TextureManager textures)
    {
        var path = Path;
        Path = string.Empty;
        Load(textures, path);
    }

    public bool TryGetRgbaSolidColor(out Rgba32 color)
    {
        if (!_isRgbaSolidColorPopulated)
        {
            _rgbaSolidColor            = BaseImage.IsSolidColor(RgbaPixels);
            _isRgbaSolidColorPopulated = true;
        }

        color = _rgbaSolidColor.CheckDefault(Rgba32.Transparent);
        return !_rgbaSolidColor.IsDefault;
    }

    public void InvalidateRgbaSolidColor()
    {
        _isRgbaSolidColorPopulated = false;
        _rgbaSolidColor            = ColorParameter.Default;
    }
}
