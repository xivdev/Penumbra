using ImSharp;
using Lumina.Excel.Sheets;
using Luna;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;

namespace Penumbra.UI.Tabs.Debug;

public sealed class EmoteListDrawer(DictEmote emotes) : IUiService
{
    public readonly DictEmote           Emotes     = emotes;
    public readonly IFilter<EmoteEntry> FileFilter = new EmoteFileFilter();
    public readonly IFilter<EmoteEntry> NameFilter = new EmoteNameFilter();

    public sealed class Cache(EmoteListDrawer parent)
        : BasicFilterCache<EmoteEntry>(new PairFilter<EmoteEntry>(parent.FileFilter, parent.NameFilter))
    {
        protected override IEnumerable<EmoteEntry> GetItems()
            => parent.Emotes.Value.Select(kvp => new EmoteEntry(kvp.Key, kvp.Value));
    }

    public sealed class EmoteFileFilter : RegexFilterBase<EmoteEntry>
    {
        protected override string ToFilterString(in EmoteEntry item, int globalIndex)
            => item.File.Utf16;
    }

    public sealed class EmoteNameFilter : RegexFilterBase<EmoteEntry>
    {
        public override bool WouldBeVisible(in EmoteEntry item, int globalIndex)
            => Text.Length is 0 || item.Emotes.Any(e => WouldBeVisible(e.Utf16));

        protected override string ToFilterString(in EmoteEntry item, int globalIndex)
            => string.Empty;
    }

    public readonly struct EmoteEntry
    {
        public readonly StringPair       File;
        public readonly List<StringPair> Emotes;

        public EmoteEntry(string key, IReadOnlyList<Emote> emotes)
        {
            File   = new StringPair(key);
            Emotes = emotes.Select(e => new StringPair(e.Name.ExtractTextExtended())).ToList();
        }

        public void Draw()
        {
            Im.Table.DrawColumn(File.Utf8);
            if (Emotes.Count > 0)
                Im.Table.DrawColumn(Emotes[0].Utf8);

            foreach (var emote in Emotes.Skip(1))
            {
                Im.Line.NoSpacing();
                Im.Text(", "u8);
                Im.Line.NoSpacing();
                Im.Text(emote.Utf16);
            }
        }
    }

    public void Draw()
    {
        FileFilter.DrawFilter("File Name"u8, Im.ContentRegion.Available);
        NameFilter.DrawFilter("Emote Name"u8, Im.ContentRegion.Available);
        using var table = Im.Table.Begin("##table"u8, 2,
            TableFlags.RowBackground | TableFlags.ScrollY | TableFlags.ScrollX | TableFlags.SizingFixedFit,
            Im.ContentRegion.Available with { Y = 12 * Im.Style.TextHeightWithSpacing });
        if (!table)
            return;

        var       cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(this));
        using var clip  = new Im.ListClipper(cache.Count, Im.Style.TextHeightWithSpacing);
        foreach (var emote in clip.Iterate(cache))
            emote.Draw();
    }
}
