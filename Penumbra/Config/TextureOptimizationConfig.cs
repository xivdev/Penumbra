using Luna;

namespace Penumbra;

public sealed class TextureOptimizationConfig : IService
{
    public long LowerSizeLimit        = 1 << 20;
    public int  SmallDimensionLimit   = 32;
    public int  LargeDimensionLimit   = 4096;
    public int  TextureDimensionLimit = 4096;
    public bool CreateBackups         = true;
}
