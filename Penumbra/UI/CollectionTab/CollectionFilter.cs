using ImSharp;
using Luna;

namespace Penumbra.UI.CollectionTab;

public sealed class CollectionFilter : TextFilterBase<CollectionSelector.Entry>, IUiService
{
    public override bool WouldBeVisible(in CollectionSelector.Entry item, int globalIndex)
        => base.WouldBeVisible(in item, globalIndex) || WouldBeVisible(item.AnonymousName.Utf16);

    protected override string ToFilterString(in CollectionSelector.Entry item, int globalIndex)
        => item.Collection.Identity.Name;
}
