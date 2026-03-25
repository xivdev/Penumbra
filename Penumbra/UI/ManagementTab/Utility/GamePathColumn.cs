using ImSharp;
using ImSharp.Table;
using Luna;

namespace Penumbra.UI.ManagementTab;

public sealed class GamePathColumn<TCacheObject, TRedirection> : TextColumn<TCacheObject>
    where TCacheObject : RedirectionCacheObject<TRedirection>
    where TRedirection : BaseScannedRedirection
{
    public GamePathColumn()
        => WidthDependsOnItems = true;

    protected override string ComparisonText(in TCacheObject item, int globalIndex)
        => item.GamePath;

    protected override StringU8 DisplayText(in TCacheObject item, int globalIndex)
        => item.GamePath;

    public override float ComputeWidth(IEnumerable<TCacheObject> obj)
        => obj.Max(o => o.GamePath.Utf8.CalculateSize().X, UnscaledWidth);
}
