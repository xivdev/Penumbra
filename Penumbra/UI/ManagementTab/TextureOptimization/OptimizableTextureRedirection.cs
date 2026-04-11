using ImSharp;
using OtterTex;
using Penumbra.Mods;

namespace Penumbra.UI.ManagementTab;

public sealed class OptimizableTexture : BaseScannedFile
{
    public readonly long           Size;
    public readonly bool           Invalid;
    public readonly bool           OptimizedTexture;
    public readonly int            Width;
    public readonly int            Height;
    public readonly int            MipMaps;
    public readonly DXGIFormat     Format;
    public readonly ColorParameter SolidColor;

    /// <summary> Invalid.  </summary>
    public OptimizableTexture(string filePath, Mod mod)
        : base(filePath, mod)
    {
        OptimizedTexture = false;
        Invalid          = true;
        Size             = -1;
        MipMaps          = 0;
    }

    /// <summary> Small. </summary>
    public OptimizableTexture(string filePath, Mod mod, bool small)
        : base(filePath, mod)
    {
        Invalid          = false;
        OptimizedTexture = small;
        MipMaps          = 0;
    }

    public OptimizableTexture(string filePath, Mod mod, long size,
        DXGIFormat format, int width, int height, int mips)
        : base(filePath, mod)
    {
        Invalid          = false;
        OptimizedTexture = false;
        Size             = size;
        Format           = format;
        Width            = width;
        Height           = height;
        MipMaps          = mips;
    }

    public OptimizableTexture(string filePath, Mod mod, long size,
        DXGIFormat format, Rgba32 solidColor, int width, int height, int mips)
        : base(filePath, mod)
    {
        Invalid          = false;
        OptimizedTexture = false;
        Size             = size;
        Format           = format;
        SolidColor       = solidColor;
        Width            = width;
        Height           = height;
        MipMaps          = 0;
    }

    public override bool DataPredicate()
        => !Invalid && !OptimizedTexture;
}
