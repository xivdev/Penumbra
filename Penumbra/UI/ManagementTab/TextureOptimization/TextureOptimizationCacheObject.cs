using ImSharp;
using Luna;

namespace Penumbra.UI.ManagementTab;

public sealed class TextureOptimizationCacheObject(OptimizableTexture obj) : FileCacheObject<OptimizableTexture>(obj)
{
    public readonly StringPair Format     = new($"{obj.Format}");
    public readonly StringPair Width      = new($"{obj.Width}");
    public readonly StringPair Height     = new($"{obj.Height}");
    public readonly StringPair SolidColor = obj.SolidColor.IsDefault ? StringPair.Empty : new StringPair(obj.SolidColor.Color!.ToString()!);
    public readonly StringPair Size       = new(FormattingFunctions.HumanReadableSize(obj.Size));
    public readonly StringPair MipMaps    = new($"{obj.MipMaps}");
    public          Task?      CompressionTask;
    public          Task?      ResizeTask;
}
