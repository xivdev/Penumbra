using ImSharp;
using Luna;
using Penumbra.Interop.Services;
using Penumbra.String;

namespace Penumbra.UI.Tabs.Debug;

public sealed class ActionTmbListDrawer(SchedulerResourceManagementService service) : IUiService
{
    public readonly SchedulerResourceManagementService Service   = service;
    public readonly IFilter<TmbEntry>                  KeyFilter = new TmbKeyFilter();

    public sealed class Cache(ActionTmbListDrawer parent) : BasicFilterCache<TmbEntry>(parent.KeyFilter)
    {
        protected override IEnumerable<TmbEntry> GetItems()
            => parent.Service.ActionTmbs.OrderBy(t => t.Value).Select(k => new TmbEntry(k.Key, k.Value));
    }

    public readonly struct TmbEntry(CiByteString key, uint value)
    {
        public readonly StringPair Key   = new(key.ToString());
        public readonly StringU8   Value = new($"{value}");

        public void Draw()
        {
            Im.Table.DrawColumn(Value);
            Im.Table.DrawColumn(Key.Utf8);
        }
    }

    public sealed class TmbKeyFilter : RegexFilterBase<TmbEntry>
    {
        protected override string ToFilterString(in TmbEntry item, int globalIndex)
            => item.Key.Utf16;
    }

    public void Draw()
    {
        KeyFilter.DrawFilter("Key"u8, Im.ContentRegion.Available);
        using var table = Im.Table.Begin("##table"u8, 2,
            TableFlags.RowBackground | TableFlags.ScrollY | TableFlags.ScrollX | TableFlags.SizingFixedFit,
            Im.ContentRegion.Available with { Y = 12 * Im.Style.TextHeightWithSpacing });
        if (!table)
            return;

        var       cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(this));
        using var clip  = new Im.ListClipper(cache.Count, Im.Style.TextHeightWithSpacing);
        foreach (var tmb in clip.Iterate(cache))
            tmb.Draw();
    }
}
