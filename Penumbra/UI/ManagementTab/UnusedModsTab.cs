using ImSharp;
using ImSharp.Table;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class UnusedModsTab(
    ModConfigUpdater modConfigUpdater,
    ModManager manager,
    Configuration config,
    ModExportManager exports,
    CommunicatorService communicator) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "Unused Mods"u8;

    public ManagementTabType Identifier
        => ManagementTabType.UnusedMods;

    private readonly Table _table       = new(modConfigUpdater, manager, config, exports, communicator);
    private          int   _defaultDays = 30;

    public void PostTabButton()
    {
        if (!Im.Item.Hovered())
            return;

        using var tt = Im.Tooltip.Begin();
        ImEx.TextMultiColored("Here you can list mods that are not currently enabled or have temporary settings in "u8)
            .Then("any "u8, ColorId.NewMod.Value()).Then(" collection."u8).End();
        Im.Text(
            "Other Plugins subscribing to Penumbras API can mark mods as 'in use' so that they do not appear, or add custom notes to them while still displaying them."u8);
    }

    public void DrawContent()
    {
        using var child = Im.Child.Begin("c"u8, Im.ContentRegion.Available);
        if (!child)
            return;

        if (Im.Checkbox("Hide All Mods with Notes"u8, _table.HideNodes))
            _table.HideNodes ^= true;
        Im.Line.Same();
        if (Im.Button("Show All Inactive Mods"u8))
            _table.UnusedCap = TimeSpan.Zero;

        Im.Line.Same();
        if (Im.Button("Show Inactive Mods Not Configured in"u8))
            _table.UnusedCap = TimeSpan.FromDays(_defaultDays);
        Im.Line.SameInner();
        Im.Item.SetNextWidthScaled(40);
        Im.Drag("Days"u8, ref _defaultDays, 0, null, 0.1f, SliderFlags.AlwaysClamp);

        _table.Draw();
    }

    private sealed class Table(
        ModConfigUpdater modConfigUpdater,
        ModManager manager,
        Configuration config,
        ModExportManager exports,
        CommunicatorService communicator) : TableBase<CacheItem, Table.Cache>(new StringU8("unused"u8),
        new ButtonColumn(manager, config, exports),
        new NameColumn(communicator), new LastEditColumn(), new ModSizeColumn(), new PathColumn(), new NotesColumn())
    {
        public bool HideNodes
        {
            get;
            set
            {
                if (field == value)
                    return;

                field        = value;
                _filterDirty = true;
            }
        }

        public TimeSpan UnusedCap
        {
            get;
            set
            {
                if (field == value)
                    return;

                field      = value;
                _spanDirty = true;
            }
        } = TimeSpan.MaxValue;


        private bool _filterDirty;
        private bool _spanDirty;

        public override IEnumerable<CacheItem> GetItems()
        {
            var now = DateTime.UtcNow;
            return modConfigUpdater.ListUnusedMods(UnusedCap).Select(m => new CacheItem(m.Item1, m.Item2, now));
        }

        public override Vector2 GetSize()
        {
            var size = Im.ContentRegion.Available;
            size.Y -= Im.Style.TextHeightWithSpacing;
            return size;
        }

        protected override void PreDraw(in Cache cache)
        {
            var buttons = (ButtonColumn)Columns[0];
            buttons.DeleteList.Clear();
            var disabled = !config.DeleteModModifier.IsActive();
            Im.Line.Same();
            if (ImEx.Button("Update View"u8,
                    "The table does not automatically update, so click this to update the visible mods without changing the time limit."u8))
                cache.Dirty |= IManagedCache.DirtyFlags.Custom;

            Im.Line.Same();
            if (ImEx.Button("Delete All Visible Mods"u8, default, "Delete all mods that are currently visible. This is NOT reversible."u8,
                    disabled))
                foreach (var (mod, globalIndex) in cache.GetItemsWithIndices().ToList())
                {
                    manager.DeleteMod(mod.Mod);
                    cache.DeleteSingleItem(globalIndex);
                }

            if (disabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\nHold {config.DeleteModModifier} to delete the mods.");
        }

        protected override void PostDraw(in Cache cache)
        {
            base.PostDraw(in cache);
            var buttons = (ButtonColumn)Columns[0];
            foreach (var item in buttons.DeleteList)
                cache.DeleteSingleItem(item);
            buttons.DeleteList.Clear();

            if (cache.Loading)
                return;

            Im.Text($"{cache.Count}/{cache.AllItems.Count} visible of {manager.Count} total Mods.");
            if (buttons.Exporting is { } mod)
            {
                Im.Line.Same();
                Im.Text($"Exporting and deleting mod: {mod.Name}");
                Im.Line.Same();
                ImEx.Spinner("s"u8, Im.Style.TextHeight / 3, 2, Im.Color.Get(ImGuiColor.Text));
            }
        }

        protected override Cache CreateCache()
            => new(this);

        public sealed class Cache : TableCache<CacheItem>
        {
            private readonly Table _parent;

            public Cache(Table parent)
                : base(parent)
            {
                KeepAliveDuration = TimeSpan.FromMinutes(5);
                _parent           = parent;
            }

            protected override bool WouldBeVisible(in CacheItem value, int globalIndex)
                => (!_parent.HideNodes || value.Notes.Length == 0) && base.WouldBeVisible(value, globalIndex);

            private CancellationTokenSource? _cancel;

            public override void Update()
            {
                if (_parent._filterDirty)
                {
                    FilterDirty          = true;
                    _parent._filterDirty = false;
                }

                if (_parent._spanDirty)
                {
                    Dirty              |= IManagedCache.DirtyFlags.Custom;
                    _parent._spanDirty =  false;
                }

                base.Update();
            }

            protected override void UpdateSort()
            {
                if (Loading)
                    return;

                base.UpdateSort();
            }

            protected override void UpdateFilter()
            {
                if (Loading)
                    return;

                base.UpdateFilter();
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                _cancel?.Cancel();
            }

            protected override void OnDataUpdate()
            {
                _cancel?.Cancel();
                base.OnDataUpdate();

                if (UnfilteredItems.Count < 50)
                    return;

                Loading = true;
                _cancel = new CancellationTokenSource();
                var token = _cancel.Token;
                Task.Run(() =>
                {
                    foreach (var mod in UnfilteredItems)
                    {
                        if (token.IsCancellationRequested)
                        {
                            Loading = false;
                            return;
                        }

                        _ = mod.ModSizeString;
                    }

                    Loading = false;
                }, token);
            }
        }
    }

    private sealed class ButtonColumn : BasicColumn<CacheItem>
    {
        public readonly HashSet<int> DeleteList = [];

        private readonly ModManager       _manager;
        private readonly Configuration    _config;
        private readonly ModExportManager _exports;

        public Mod? Exporting { get; private set; }

        public ButtonColumn(ModManager manager, Configuration config, ModExportManager exports)
        {
            _manager =  manager;
            _config  =  config;
            _exports =  exports;
            Label    =  StringU8.Empty;
            Flags    |= TableColumnFlags.NoSort;
        }

        public override void DrawColumn(in CacheItem item, int globalIndex)
        {
            var inactive      = !_config.DeleteModModifier.IsActive();
            var exportingThis = Exporting == item.Mod;
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "Delete this mod from Penumbra and your drive. This is NOT reversible."u8,
                    inactive || exportingThis))
            {
                _manager.DeleteMod(item.Mod);
                DeleteList.Add(globalIndex);
            }

            if (inactive)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\nHold {_config.DeleteModModifier} to delete.");
            if (exportingThis)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "\nCurrently exporting and deleting this mod, please wait."u8);

            Im.Line.SameInner();

            var exporting = Exporting is not null;
            if (ImEx.Icon.Button(LunaStyle.BackupDeleteIcon,
                    "Export this mod to your export directory, compressing it, and then delete it from Penumbra."u8, inactive || exporting))
            {
                Exporting = item.Mod;
                _exports.CreateAsync(Exporting).ContinueWith(_ =>
                {
                    _manager.DeleteMod(Exporting);
                    DeleteList.Add(globalIndex);
                    Exporting = null;
                });
            }

            if (inactive)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\nHold {_config.DeleteModModifier} to delete.");
            if (exporting)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Already exporting and deleting a mod, please wait."u8);

            Im.Line.SameInner();
            if (ImEx.Icon.Button(LunaStyle.FolderIcon, "Open the mod directory in the file explorer of your choice."u8))
                Process.Start(new ProcessStartInfo(item.Mod.ModPath.FullName) { UseShellExecute = true });
        }

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => Im.Style.FrameHeight * 3 + 2 * Im.Style.ItemInnerSpacing.X;
    }

    private sealed class NameColumn : TextColumn<CacheItem>
    {
        private readonly CommunicatorService _communicator;

        public NameColumn(CommunicatorService communicator)
        {
            _communicator =  communicator;
            Label         =  new StringU8("Mod Name"u8);
            Flags         |= TableColumnFlags.WidthStretch;
        }

        protected override string ComparisonText(in CacheItem item, int globalIndex)
            => item.Mod.Name;

        protected override StringU8 DisplayText(in CacheItem item, int globalIndex)
            => item.ModName;

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => 0.3f;

        public override void DrawColumn(in CacheItem item, int globalIndex)
        {
            var content = Im.ContentRegion.Available.X;
            Im.Cursor.FrameAlign();
            var clicked = Im.Selectable(item.ModName);
            Im.Tooltip.OnHover(item.DirectoryName);
            if (Im.Font.CalculateSize(item.ModName).X >= content)
                Im.Tooltip.OnHover(item.ModName);
            Im.Tooltip.OnHover("\nClick to move to mod."u8);
            if (clicked)
                _communicator.SelectTab.Invoke(new SelectTab.Arguments(TabType.Mods, item.Mod));
        }
    }

    private sealed class PathColumn : TextColumn<CacheItem>
    {
        public PathColumn()
        {
            Label =  new StringU8("Mod Path"u8);
            Flags |= TableColumnFlags.WidthStretch;
        }

        protected override string ComparisonText(in CacheItem item, int globalIndex)
            => item.Mod.Path.CurrentPath;

        protected override StringU8 DisplayText(in CacheItem item, int globalIndex)
            => item.ModPath;

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => 0.7f;

        public override void DrawColumn(in CacheItem item, int globalIndex)
        {
            var content = Im.ContentRegion.Available.X;
            Im.Cursor.FrameAlign();
            base.DrawColumn(in item, globalIndex);
            if (Im.Item.Size.X >= content)
                Im.Tooltip.OnHover(item.ModPath);
        }
    }


    private sealed class LastEditColumn : NumberColumn<long, CacheItem>
    {
        public LastEditColumn()
        {
            Label = new StringU8("Last Config Edit"u8);
        }

        public override long ToValue(in CacheItem item, int globalIndex)
            => item.Mod.LastConfigEdit;

        protected override StringU8 DisplayNumber(in CacheItem item, int globalIndex)
            => item.Duration;

        protected override string ComparisonText(in CacheItem item, int globalIndex)
            => item.Duration.Utf16;

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => Im.Font.CalculateSize("Last Config Edit"u8).X + ImEx.Table.ArrowWidth + Im.Style.CellPadding.X * 2;

        public override void DrawColumn(in CacheItem item, int globalIndex)
        {
            Im.Cursor.FrameAlign();
            base.DrawColumn(in item, globalIndex);
            Im.Tooltip.OnHover($"Click to copy Timestamp: {item.Mod.LastConfigEdit}");
            if (Im.Item.Clicked())
                Im.Clipboard.Set($"{item.Mod.LastConfigEdit}");
        }
    }

    private sealed class ModSizeColumn : NumberColumn<long, CacheItem>
    {
        public ModSizeColumn()
        {
            Label = new StringU8("Size On Disk"u8);
        }

        public override long ToValue(in CacheItem item, int globalIndex)
            => item.ModSize;

        protected override StringU8 DisplayNumber(in CacheItem item, int globalIndex)
            => item.ModSizeString;

        protected override string ComparisonText(in CacheItem item, int globalIndex)
            => item.ModSizeString.Utf16;

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => Im.Font.CalculateSize("Size On Disk"u8).X + ImEx.Table.ArrowWidth + Im.Style.CellPadding.X * 2;

        public override void DrawColumn(in CacheItem item, int globalIndex)
        {
            Im.Cursor.FrameAlign();
            base.DrawColumn(in item, globalIndex);
        }
    }

    private sealed class NotesColumn : TextColumn<CacheItem>
    {
        private static readonly StringU8 Notes = new("Notes"u8);

        public NotesColumn()
        {
            Label = new StringU8("Notes"u8);
        }

        public override bool WouldBeVisible(in CacheItem item, int globalIndex)
        {
            if (item.Notes.Length is 0)
                return Filter.WouldBeVisible(string.Empty);
            if (Filter.WouldBeVisible("Notes"))
                return true;

            return item.Notes.Any(n => Filter.WouldBeVisible(n.Item1.Utf16) || Filter.WouldBeVisible(n.Item2.Utf16));
        }

        public override int Compare(in CacheItem lhs, int lhsGlobalIndex, in CacheItem rhs, int rhsGlobalIndex)
        {
            var first = lhs.Notes.Length.CompareTo(rhs.Notes.Length);
            if (first is not 0)
                return first;

            foreach (var (lhsNote, rhsNote) in lhs.Notes.Zip(rhs.Notes))
            {
                var second = lhsNote.Item1.Utf16.CompareTo(rhsNote.Item1.Utf16, StringComparison.Ordinal);
                if (second is not 0)
                    return second;
            }

            foreach (var (lhsNote, rhsNote) in lhs.Notes.Zip(rhs.Notes))
            {
                var third = lhsNote.Item2.Utf16.Length.CompareTo(rhsNote.Item2.Utf16.Length);
                if (third is not 0)
                    return third;
            }

            return 0;
        }

        protected override string ComparisonText(in CacheItem item, int globalIndex)
            => item.Notes.Length is 0 ? string.Empty : "Notes";

        protected override StringU8 DisplayText(in CacheItem item, int globalIndex)
            => item.Notes.Length is 0 ? StringU8.Empty : Notes;

        public override void DrawColumn(in CacheItem item, int globalIndex)
            => DrawNotes(item.Notes);

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => Im.Style.FrameHeightWithSpacing + Im.Font.CalculateSize(Notes).X;
    }

    private record CacheItem(
        Mod Mod,
        StringU8 ModName,
        StringU8 ModPath,
        StringU8 DirectoryName,
        long ModSize,
        StringPair ModSizeString,
        StringPair Duration,
        (StringPair, StringPair)[] Notes)
    {
        public long ModSize
        {
            get => field < 0 ? field = WindowsFunctions.GetDirectorySize(Mod.ModPath.FullName) : field;
        } = ModSize;

        private StringPair _modSizeString = ModSizeString;

        public StringPair ModSizeString
        {
            get => _modSizeString.IsEmpty
                ? _modSizeString = new StringPair(FormattingFunctions.HumanReadableSize(ModSize))
                : _modSizeString;
        }

        public CacheItem(Mod mod, (string, string)[] notes, DateTime now)
            : this(mod, new StringU8(mod.Name), new StringU8(mod.Path.CurrentPath), new StringU8($"Directory Name: {mod.Identifier}"),
                -1, StringPair.Empty, new StringPair(FormattingFunctions.DurationString(mod.LastConfigEdit, now)),
                notes.Select(n => (new StringPair(n.Item1), new StringPair(n.Item2))).ToArray())
        { }
    }

    public static void DrawNotes((StringPair, StringPair)[] notes)
    {
        if (notes.Length is 0)
            return;

        ImEx.Icon.DrawAligned(LunaStyle.InfoIcon, LunaStyle.FavoriteColor);
        var hovered = Im.Item.Hovered();
        Im.Line.SameInner();
        Im.Text("Notes"u8);
        if (!hovered && !Im.Item.Hovered())
            return;
        
        using var tt = Im.Tooltip.Begin();
        DrawNote(notes[0]);
        foreach (var note in notes.Skip(1))
        {
            Im.Separator();
            DrawNote(note);
        }
        
        return;
        
        static void DrawNote((StringPair, StringPair) note)
        {
            using (Im.Group())
            {
                Im.Text(note.Item1.Utf8);
                Im.Line.Same();
                using (Im.Group())
                {
                    Im.Text(note.Item2.Utf8);
                }
            }
        }
    }
}
