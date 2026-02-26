using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.MainWindow;

public sealed class ChangedItemsTab(
    CollectionManager collectionManager,
    CollectionSelectHeader collectionHeader,
    ChangedItemDrawer drawer,
    CommunicatorService communicator,
    Configuration config)
    : ITab<TabType>, IDisposable
{
    public ReadOnlySpan<byte> Label
        => "Changed Items"u8;

    public TabType Identifier
        => TabType.ChangedItems;

    private Vector2 _buttonSize;

    private readonly ChangedItemFilter _filter = new(drawer, config);

    private sealed class ChangedItemFilter : IFilter<Item>, IDisposable
    {
        private readonly ChangedItemDrawer _drawer;
        private readonly FilterConfig      _filterConfig;
        private          bool              _ownUpdate;

        public ChangedItemFilter(ChangedItemDrawer drawer, Configuration config)
        {
            _drawer       = drawer;
            _filterConfig = config.Filters;
            if (!config.RememberChangedItemFilters)
                Clear();
            _filterConfig.ChangedItemTypeFilterChanged += OnChangedItemTypeFilterChanged;
        }

        private void OnChangedItemTypeFilterChanged(ChangedItemIconFlag _1, ChangedItemIconFlag _2)
        {
            if (!_ownUpdate)
                FilterChanged?.Invoke();
        }

        public bool WouldBeVisible(in Item item, int globalIndex)
            => _drawer.FilterChangedItemGlobal(item.Name, item.Data, _filterConfig.ChangedItemItemFilter)
             && (_filterConfig.ChangedItemModFilter.Length is 0
                 || item.Mods.Any(m => m.Name.Contains(_filterConfig.ChangedItemModFilter, StringComparison.OrdinalIgnoreCase)));

        public event Action? FilterChanged;

        public bool DrawFilter(ReadOnlySpan<byte> label, Vector2 availableRegion)
        {
            using var style = ImStyleSingle.FrameRounding.Push(0);
            var varWidth = Im.ContentRegion.Available.X
              - 450 * Im.Style.GlobalScale
              - Im.Style.ItemSpacing.X;
            Im.Item.SetNextWidth(450 * Im.Style.GlobalScale);
            var filter = _filterConfig.ChangedItemItemFilter;
            var ret    = Im.Input.Text("##changedItemsFilter"u8, ref filter, "Filter Item..."u8);
            if (ret)
                _filterConfig.ChangedItemItemFilter = filter;
            Im.Line.Same();
            Im.Item.SetNextWidth(varWidth);
            filter = _filterConfig.ChangedItemModFilter;
            if (Im.Input.Text("##changedItemsModFilter"u8, ref filter, "Filter Mods..."u8))
            {
                ret                                = true;
                _filterConfig.ChangedItemModFilter = filter;
            }

            if (ret)
                FilterChanged?.Invoke();
            return ret;
        }

        public bool Clear()
        {
            Im.Id.ClearActive();
            var changes = !IsEmpty;
            if (!changes)
                return false;

            _filterConfig.ChangedItemModFilter  = string.Empty;
            _filterConfig.ChangedItemItemFilter = string.Empty;
            _ownUpdate                          = true;
            _filterConfig.ChangedItemTypeFilter = ChangedItemFlagExtensions.DefaultFlags;
            _ownUpdate                          = false;
            FilterChanged?.Invoke();
            return true;
        }

        public bool IsEmpty
            => _filterConfig.ChangedItemModFilter.Length is 0
             && _filterConfig.ChangedItemItemFilter.Length is 0
             && _filterConfig.ChangedItemTypeFilter is ChangedItemFlagExtensions.DefaultFlags;

        public void Dispose()
            => _filterConfig.ChangedItemTypeFilterChanged -= OnChangedItemTypeFilterChanged;
    }

    private readonly record struct Item(string Label, IIdentifiedObjectData Data, SingleArray<IMod> Mods)
    {
        public readonly string              Name         = Data.ToName(Label);
        public readonly StringU8            ItemName     = new(Data.ToName(Label));
        public readonly StringU8            Mod          = Mods.Count > 0 ? new StringU8(Mods[0].Name) : StringU8.Empty;
        public readonly StringU8            ModelData    = new(Data.AdditionalData);
        public readonly ChangedItemIconFlag CategoryIcon = Data.GetIcon().ToFlag();

        public readonly StringU8 Tooltip = Mods.Count > 1
            ? new StringU8($"Other mods affecting this item:\n{StringU8.Join((byte)'\n', Mods.Skip(1).Select(m => m.Name))}")
            : StringU8.Empty;
    }

    private sealed class Cache : BasicFilterCache<Item>
    {
        private readonly ActiveCollections _collections;
        private readonly CollectionChange  _collectionChange;

        public Cache(ActiveCollections collections, CommunicatorService communicator, IFilter<Item> filter)
            : base(filter)
        {
            _collections      = collections;
            _collectionChange = communicator.CollectionChange;
            _collectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.ChangedItemsTabCache);
        }


        private void OnCollectionChange(in CollectionChange.Arguments arguments)
            => FilterDirty = true;

        protected override IEnumerable<Item> GetItems()
            => _collections.Current.ChangedItems.Select(kvp => new Item(kvp.Key, kvp.Value.Item2, kvp.Value.Item1));

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _collectionChange.Unsubscribe(OnCollectionChange);
        }
    }

    public void DrawContent()
    {
        collectionHeader.Draw(true);
        drawer.DrawTypeFilter(true);
        _filter.DrawFilter("##Filter"u8, Im.ContentRegion.Available);
        using var child = Im.Child.Begin("##changedItemsChild"u8, Im.ContentRegion.Available);
        if (!child)
            return;

        _buttonSize = new Vector2(Im.Style.ItemSpacing.Y + Im.Style.FrameHeight);
        using var style = ImStyleDouble.CellPadding.Push(Vector2.Zero)
            .Push(ImStyleDouble.ItemSpacing,         Vector2.Zero)
            .Push(ImStyleDouble.FramePadding,        Vector2.Zero)
            .Push(ImStyleDouble.SelectableTextAlign, new Vector2(0.01f, 0.5f));

        using var table = Im.Table.Begin("##changedItems"u8, 3, TableFlags.RowBackground, Im.ContentRegion.Available);
        if (!table)
            return;

        var varWidth = Im.ContentRegion.Available.X
          - 450 * Im.Style.GlobalScale
          - Im.Style.ItemSpacing.X;
        const TableColumnFlags flags = TableColumnFlags.NoResize | TableColumnFlags.WidthFixed;
        table.SetupColumn("items"u8, flags, 450 * Im.Style.GlobalScale);
        table.SetupColumn("mods"u8,  flags, varWidth - 140 * Im.Style.GlobalScale);
        table.SetupColumn("id"u8,    flags, 140 * Im.Style.GlobalScale);

        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current,
            () => new Cache(collectionManager.Active, communicator, _filter));
        using var clipper = new Im.ListClipper(cache.Count, _buttonSize.Y);
        foreach (var (idx, item) in clipper.Iterate(cache).Index())
        {
            using var id = Im.Id.Push(idx);
            DrawChangedItemColumn(table, item);
        }
    }

    /// <summary> Draw a full column for a changed item. </summary>
    private void DrawChangedItemColumn(in Im.TableDisposable table, in Item item)
    {
        table.NextColumn();
        drawer.DrawCategoryIcon(item.CategoryIcon, _buttonSize.Y);
        Im.Line.NoSpacing();
        var clicked = Im.Selectable(item.ItemName, false, SelectableFlags.None, _buttonSize with { X = 0 });
        drawer.ChangedItemHandling(item.Data, clicked);

        table.NextColumn();
        DrawModColumn(item);

        table.NextColumn();
        ChangedItemDrawer.DrawModelData(item.ModelData, _buttonSize.Y);
    }

    private void DrawModColumn(in Item item)
    {
        if (item.Mods.Count <= 0)
            return;

        if (Im.Selectable(item.Mod, false, SelectableFlags.None, _buttonSize with { X = 0 })
         && Im.Io.KeyControl
         && item.Mods[0] is Mod mod)
            communicator.SelectTab.Invoke(new SelectTab.Arguments(TabType.Mods, mod));

        if (!Im.Item.Hovered())
            return;

        using var _ = Im.Tooltip.Begin();
        Im.Text("Hold Control and click to jump to mod.\n"u8);
        if (!item.Tooltip.IsEmpty)
            Im.Text(item.Tooltip);
    }

    public void Dispose()
        => _filter.Dispose();
}
