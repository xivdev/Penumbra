using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ManagementTab;

public sealed class DuplicateModsTab(ModConfigUpdater configUpdater, ModManager mods, CollectionStorage collections, Configuration config)
    : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "Duplicate Mods"u8;

    public ManagementTabType Identifier
        => ManagementTabType.DuplicateMods;

    public void PostTabButton()
    {
        Im.Tooltip.OnHover(
            "This tab shows mods with identical names and some additional data to discern which to keep if they are indeed identical."u8);
    }

    public void DrawContent()
    {
        using var child = Im.Child.Begin("c"u8);
        if (!child)
            return;

        using var table = Im.Table.Begin("duplicates"u8, 7, TableFlags.RowBackground | TableFlags.ScrollY);
        if (!table)
            return;

        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(configUpdater, mods, collections));

        table.SetupScrollFreeze(0, 1);
        table.SetupColumn(""u8, TableColumnFlags.WidthFixed, Im.Style.FrameHeight * 2 + Im.Style.ItemInnerSpacing.X);
        table.SetupColumn("Mod Name"u8, TableColumnFlags.WidthStretch, 0.25f);
        table.SetupColumn("Mod Directory"u8, TableColumnFlags.WidthStretch, 0.25f);
        table.SetupColumn("Active"u8, TableColumnFlags.WidthFixed, cache.Active.CalculateSize().X);
        table.SetupColumn("Import Date"u8, TableColumnFlags.WidthFixed, cache.Date.CalculateSize().X);
        table.SetupColumn("Path"u8, TableColumnFlags.WidthStretch, 0.5f);
        table.SetupColumn("Notes"u8, TableColumnFlags.WidthFixed, cache.Notes.CalculateSize().X + Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X);
        table.HeaderRow();

        var       lastDrawnName = StringU8.Empty;
        var       disabled      = !config.DeleteModModifier.IsActive();
        using var clipper       = new Im.ListClipper(cache.Items.Count, 0);
        foreach (var (index, item) in cache.Items.Index())
        {
            using var id = Im.Id.Push(index);
            table.NextColumn();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "Delete this mod. This is NOT revertible."u8, disabled))
            {
                mods.DeleteMod(item.Mod);
                cache.Dirty |= IManagedCache.DirtyFlags.Custom;
            }

            if (disabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {config.DeleteModModifier} to delete this mod.");
            Im.Line.SameInner();
            if (ImEx.Icon.Button(LunaStyle.FolderIcon, "Open this mod in the file explorer of your choice."u8))
                Process.Start(new ProcessStartInfo(item.Mod.ModPath.FullName) { UseShellExecute = true });

            if (lastDrawnName == item.Name)
            {
                table.NextColumn();
            }
            else
            {
                table.DrawFrameColumnWithTooltip(item.Name);
                lastDrawnName = item.Name;
            }

            table.DrawFrameColumnWithTooltip(item.Directory);
            table.NextColumn();
            Im.Cursor.FrameAlign();
            var count = item.Collections.Length + item.MarkedActive;
            ImEx.TextRightAligned($"{count}");
            if (count > 0 && Im.Item.Hovered())
            {
                using var tt = Im.Tooltip.Begin();
                foreach (var collection in item.Collections)
                {
                    Im.Text("Active in Collection "u8);
                    Im.Line.NoSpacing();
                    Im.Text(collection.Identity.Name);
                }
            
                if (item.MarkedActive > 0)
                    Im.Text($"{item.MarkedActive} other Plugins mark this mod as active.");
            }
            
            table.DrawFrameColumn(item.CreationDate);
            table.DrawFrameColumnWithTooltip(item.Path);
            table.NextColumn();
            UnusedModsTab.DrawNotes(item.Notes);
        }
    }

    private sealed class Cache(ModConfigUpdater configUpdater, ModManager mods, CollectionStorage collections) : BasicCache
    {
        public readonly record struct CacheItem(
            StringU8 Name,
            Mod Mod,
            StringU8 Directory,
            StringU8 CreationDate,
            StringU8 Path,
            ModCollection[] Collections,
            (StringPair, StringPair)[] Notes,
            int MarkedActive);

        public List<CacheItem> Items = [];

        public readonly StringU8 Active = new("Active"u8);
        public readonly StringU8 Date   = new("00/00/0000 00:00"u8);
        public readonly StringU8 Notes  = new("Notes"u8);

        public override void Update()
        {
            if (!Dirty.HasFlag(IManagedCache.DirtyFlags.Custom))
                return;

            Items.Clear();
            var data = mods.GroupBy(m => m.Name).Select(g => (g.Key, g.ToList())).Where(p => p.Item2.Count > 1).ToList();
            var dict = new Dictionary<Assembly, (bool, string)>();
            foreach (var (name, list) in data)
            {
                var nameU8 = new StringU8(name);
                foreach (var mod in list)
                {
                    configUpdater.QueryUsage(mod, dict);
                    var creation          = new StringU8($"{DateTimeOffset.FromUnixTimeMilliseconds(mod.ImportDate):g}");
                    var activeCollections = collections.Where(c => c.GetActualSettings(mod.Index).Settings?.Enabled is true).ToArray();
                    var notes = dict.Select(kvp => (new StringPair(kvp.Key.GetName().Name ?? "Unknown"), new StringPair(kvp.Value.Item2)))
                        .ToArray();
                    var markedActive = dict.Values.Count(v => v.Item1);
                    Items.Add(new CacheItem(nameU8, mod, new StringU8(mod.Identifier), creation, new StringU8(mod.Path.CurrentPath),
                        activeCollections, notes, markedActive));
                }
            }

            Items = Items.OrderBy(i => i.Name).ThenByDescending(i => i.Collections.Length + i.MarkedActive)
                .ThenByDescending(i => i.Notes.Length).ThenBy(i => i.Mod.ImportDate)
                .ThenBy(i => i.Directory).ToList();
            Dirty = IManagedCache.DirtyFlags.Clean;
        }
    }
}
