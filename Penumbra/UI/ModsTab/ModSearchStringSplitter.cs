using OtterGui.Filesystem;
using OtterGui.Filesystem.Selector;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ModsTab;

public enum ModSearchType : byte
{
    Default = 0,
    ChangedItem,
    Tag,
    Name,
    Author,
    Category,
}

public sealed class ModSearchStringSplitter : SearchStringSplitter<ModSearchType, FileSystem<Mod>.Leaf, ModSearchStringSplitter.Entry>
{
    public readonly struct Entry : ISplitterEntry<ModSearchType, Entry>
    {
        public string              Needle         { get; init; }
        public ModSearchType       Type           { get; init; }
        public ChangedItemIconFlag IconFlagFilter { get; init; }

        public bool Contains(Entry other)
        {
            if (Type != other.Type)
                return false;
            if (Type is ModSearchType.Category)
                return IconFlagFilter == other.IconFlagFilter;

            return Needle.Contains(other.Needle);
        }
    }

    protected override bool ConvertToken(char token, out ModSearchType val)
    {
        val = token switch
        {
            'c' or 'C' => ModSearchType.ChangedItem,
            't' or 'T' => ModSearchType.Tag,
            'n' or 'N' => ModSearchType.Name,
            'a' or 'A' => ModSearchType.Author,
            's' or 'S' => ModSearchType.Category,
            _          => ModSearchType.Default,
        };
        return val is not ModSearchType.Default;
    }

    protected override bool AllowsNone(ModSearchType val)
        => val switch
        {
            ModSearchType.Author      => true,
            ModSearchType.ChangedItem => true,
            ModSearchType.Tag         => true,
            ModSearchType.Category    => true,
            _                         => false,
        };

    protected override void PostProcessing()
    {
        base.PostProcessing();
        HandleList(General);
        HandleList(Forced);
        HandleList(Negated);
        return;

        static void HandleList(List<Entry> list)
        {
            for (var i = 0; i < list.Count; ++i)
            {
                var entry = list[i];
                if (entry.Type is not ModSearchType.Category)
                    continue;

                if (ChangedItemDrawer.TryParsePartial(entry.Needle, out var icon))
                    list[i] = entry with
                    {
                        IconFlagFilter = icon,
                        Needle = string.Empty,
                    };
                else
                    list.RemoveAt(i--);
            }
        }
    }

    public bool IsVisible(ModFileSystem.Folder folder)
    {
        switch (State)
        {
            case FilterState.NoFilters: return true;
            case FilterState.NoMatches: return false;
        }

        var fullName = folder.FullName();
        return Forced.All(i => MatchesName(i, folder.Name, fullName, false))
         && !Negated.Any(i => MatchesName(i, folder.Name, fullName, true))
         && (General.Count == 0 || General.Any(i => MatchesName(i, folder.Name, fullName, false)));
    }

    protected override bool Matches(Entry entry, ModFileSystem.Leaf leaf)
        => entry.Type switch
        {
            ModSearchType.Default => leaf.FullName().AsSpan().Contains(entry.Needle, StringComparison.OrdinalIgnoreCase)
             || leaf.Value.Name.Lower.AsSpan().Contains(entry.Needle, StringComparison.Ordinal),
            ModSearchType.ChangedItem => leaf.Value.LowerChangedItemsString.AsSpan().Contains(entry.Needle, StringComparison.Ordinal),
            ModSearchType.Tag         => leaf.Value.AllTagsLower.AsSpan().Contains(entry.Needle, StringComparison.Ordinal),
            ModSearchType.Name        => leaf.Value.Name.Lower.AsSpan().Contains(entry.Needle, StringComparison.Ordinal),
            ModSearchType.Author      => leaf.Value.Author.Lower.AsSpan().Contains(entry.Needle, StringComparison.Ordinal),
            ModSearchType.Category => leaf.Value.ChangedItems.Any(p
                => ((p.Value?.Icon.ToFlag() ?? ChangedItemIconFlag.Unknown) & entry.IconFlagFilter) != 0),
            _ => true,
        };

    protected override bool MatchesNone(ModSearchType type, bool negated, ModFileSystem.Leaf haystack)
        => type switch
        {
            ModSearchType.Author when negated      => !haystack.Value.Author.IsEmpty,
            ModSearchType.Author                   => haystack.Value.Author.IsEmpty,
            ModSearchType.ChangedItem when negated => haystack.Value.LowerChangedItemsString.Length > 0,
            ModSearchType.ChangedItem              => haystack.Value.LowerChangedItemsString.Length == 0,
            ModSearchType.Tag when negated         => haystack.Value.AllTagsLower.Length > 0,
            ModSearchType.Tag                      => haystack.Value.AllTagsLower.Length == 0,
            ModSearchType.Category when negated    => haystack.Value.ChangedItems.Count > 0,
            ModSearchType.Category                 => haystack.Value.ChangedItems.Count == 0,
            _                                      => true,
        };

    private static bool MatchesName(Entry entry, ReadOnlySpan<char> name, ReadOnlySpan<char> fullName, bool defaultValue)
        => entry.Type switch
        {
            ModSearchType.Default => fullName.Contains(entry.Needle, StringComparison.OrdinalIgnoreCase),
            ModSearchType.Name    => name.Contains(entry.Needle, StringComparison.OrdinalIgnoreCase),
            _                     => defaultValue,
        };
}
