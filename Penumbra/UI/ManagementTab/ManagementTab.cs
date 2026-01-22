using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ManagementTab;

public enum ManagementTabType
{
    UnusedMods,
    DuplicateMods,
    Cleanup,
}

public sealed class DuplicateModsTab(ModManager mods, CollectionStorage collections) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "Duplicate Mods"u8;

    public ManagementTabType Identifier
        => ManagementTabType.DuplicateMods;

    public void DrawContent()
    {
        using var table = Im.Table.Begin("duplicates"u8, 4, TableFlags.RowBackground);
        if (!table)
            return;

        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(mods, collections));
        foreach (var item in cache.Items)
        {
            table.DrawFrameColumn(item.Name);
            foreach (var (mod, date, path, enabledCollections) in item.Data)
            {
                table.GoToColumn(0);
                table.DrawFrameColumn(path);
                table.DrawFrameColumn(date);
                table.DrawFrameColumn($"{enabledCollections.Length}");
                if (Im.Item.Hovered())
                {
                    using var tt = Im.Tooltip.Begin();
                    foreach (var collection in enabledCollections)
                        Im.Text(collection.Identity.Name);
                }

                table.NextRow();
            }
        }
    }

    private sealed class Cache(ModManager mods, CollectionStorage collections) : BasicCache
    {
        public readonly record struct CacheItem(
            StringU8 Name,
            (Mod Mod, StringU8 CreationDate, StringU8 Path, ModCollection[] Collections)[] Data);

        public List<CacheItem> Items = [];

        public override void Update()
        {
            if (!Dirty.HasFlag(IManagedCache.DirtyFlags.Custom))
                return;

            Items.Clear();
            Items.AddRange(mods.GroupBy(m => m.Name)
                .Select(kvp => new CacheItem(new StringU8(kvp.Key),
                    kvp.Select(m => (m, new StringU8($"{DateTimeOffset.FromUnixTimeMilliseconds(m.ImportDate)}"),
                            new StringU8(m.Path.CurrentPath),
                            collections.Where(c => c.GetActualSettings(m.Index).Settings?.Enabled is true).ToArray()))
                        .OrderByDescending(t => t.Item4.Length).ThenBy(t => t.m.ImportDate).ToArray())).Where(p => p.Data.Length > 1));

            Dirty = IManagedCache.DirtyFlags.Clean;
        }
    }
}

public sealed class UnusedModsTab(ModConfigUpdater modConfigUpdater) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "Unused Mods"u8;

    public ManagementTabType Identifier
        => ManagementTabType.UnusedMods;

    public void DrawContent()
    {
        using var table = Im.Table.Begin("unused"u8, 5, TableFlags.RowBackground | TableFlags.SizingFixedFit);
        if (!table)
            return;

        var       cache   = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(modConfigUpdater));
        using var clipper = new Im.ListClipper(cache.Data.Count, Im.Style.FrameHeightWithSpacing);
        foreach (var item in clipper.Iterate(cache.Data))
        {
            table.DrawFrameColumn(item.ModName);
            table.DrawFrameColumn(item.ModPath);
            table.DrawFrameColumn(item.Duration.Utf8);
            table.DrawFrameColumn(item.ModSizeString.Utf8);
            table.NextColumn();
            if (item.Notes.Length > 0)
            {
                ImEx.Icon.DrawAligned(LunaStyle.InfoIcon, LunaStyle.FavoriteColor);
                var hovered = Im.Item.Hovered();
                Im.Line.SameInner();
                Im.Text("Notes"u8);
                if (hovered || Im.Item.Hovered())
                {
                    using var tt = Im.Tooltip.Begin();
                    using (Im.Group())
                    {
                        foreach (var (plugin, _) in item.Notes)
                            Im.Text(plugin.Utf8);
                    }

                    using (Im.Group())
                    {
                        foreach (var (_, node) in item.Notes)
                            Im.Text(node.Utf8);
                    }
                }
            }
        }
    }


    private sealed class Cache(ModConfigUpdater modConfigUpdater) : BasicCache
    {
        public readonly record struct CacheItem(
            Mod Mod,
            StringU8 ModName,
            StringU8 ModPath,
            long ModSize,
            StringPair ModSizeString,
            StringPair Duration,
            (StringPair, StringPair)[] Notes)
        {
            public CacheItem(Mod mod, (string, string)[] Notes, DateTime now)
                : this(mod, new StringU8(mod.Name), new StringU8(mod.Path.CurrentPath), 0, StringPair.Empty,
                    new StringPair(FormattingFunctions.DurationString(mod.LastConfigEdit, now)),
                    Notes.Select(n => (new StringPair(n.Item1), new StringPair(n.Item2))).ToArray())
            { }
        }

        public List<CacheItem> Data = [];

        public override void Update()
        {
            if (!Dirty.HasFlag(IManagedCache.DirtyFlags.Custom))
                return;

            var now = DateTime.UtcNow;
            Data.Clear();
            foreach (var (mod, notes) in modConfigUpdater.ListUnusedMods(TimeSpan.Zero).OrderBy(mod => mod.Item1.LastConfigEdit))
                Data.Add(new CacheItem(mod, notes, now));
            Dirty = IManagedCache.DirtyFlags.Clean;
        }
    }
}

public sealed class CleanupTab : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "General Cleanup"u8;

    public ManagementTabType Identifier
        => ManagementTabType.Cleanup;

    public void DrawContent()
    { }
}

public sealed class ManagementTab : TabBar<ManagementTabType>, ITab<TabType>
{
    public new ReadOnlySpan<byte> Label
        => base.Label;

    public TabType Identifier
        => TabType.Management;

    public ManagementTab(Logger log,
        EphemeralConfig config,
        UnusedModsTab unusedMods,
        DuplicateModsTab duplicateMods,
        CleanupTab cleanup)
        : base("Management", log, unusedMods, duplicateMods, cleanup)
    {
        NextTab = config.SelectedManagementTab;
        TabSelected.Subscribe((in tab) => config.SelectedManagementTab = tab, 0);
    }

    public void DrawContent()
        => Draw();
}
