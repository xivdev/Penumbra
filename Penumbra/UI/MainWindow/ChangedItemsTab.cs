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
    UiNavigator navigator,
    Configuration config)
    : ITab<TabType>, IDisposable
{
    private const float ItemColumnWidth = 450f;
    private const float DataColumnWidth = 140f;

    public ReadOnlySpan<byte> Label
        => "Changed Items"u8;

    public TabType Identifier
        => TabType.ChangedItems;

    private Vector2 _buttonSize;

    private readonly ChangedItemFilter _filter = new(drawer, config);

    private sealed class ChangedItemFilter : IFilter<Item>, IDisposable
    {
        private readonly ChangedItemDrawer     _drawer;
        private readonly FilterConfig          _filterConfig;
        private readonly ChangedItemItemFilter _itemFilter;
        private readonly ChangedItemModFilter  _modFilter;
        private          bool                  _ownUpdate;

        public ChangedItemFilter(ChangedItemDrawer drawer, Configuration config)
        {
            _drawer                   =  drawer;
            _filterConfig             =  config.Filters;
            _itemFilter               =  new ChangedItemItemFilter(config);
            _modFilter                =  new ChangedItemModFilter(config);
            _modFilter.FilterChanged  += () => FilterChanged?.Invoke();
            _itemFilter.FilterChanged += () => FilterChanged?.Invoke();
            if (!config.Ui.RememberChangedItemFilters)
                ClearOnStart();
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
            using var style     = ImStyleSingle.FrameRounding.Push(0);
            var       leftWidth = new Vector2(ItemColumnWidth * Im.Style.GlobalScale - Im.Style.ItemInnerSpacing.X, Im.Style.FrameHeight);
            var       varWidth  = new Vector2(availableRegion.X - leftWidth.X - Im.Style.ItemInnerSpacing.X,        Im.Style.FrameHeight);
            var       ret       = _itemFilter.DrawFilter("Filter Item..."u8, leftWidth);
            Im.Line.Same();
            ret |= _modFilter.DrawFilter("Filter Mods..."u8, varWidth);
            return ret;
        }

        public bool Clear()
        {
            Im.Id.ClearActive();
            var changes = !IsEmpty;
            if (!changes)
                return false;

            ClearOnStart();
            FilterChanged?.Invoke();
            return true;
        }

        public void ClearOnStart()
        {
            _itemFilter.Clear();
            _modFilter.Clear();
            _ownUpdate                          = true;
            _filterConfig.ChangedItemTypeFilter = ChangedItemFlagExtensions.DefaultFlags;
            _ownUpdate                          = false;
        }

        public bool IsEmpty
            => _filterConfig.ChangedItemModFilter.Length is 0
             && _filterConfig.ChangedItemItemFilter.Length is 0
             && _filterConfig.ChangedItemTypeFilter is ChangedItemFlagExtensions.DefaultFlags;

        public void Dispose()
            => _filterConfig.ChangedItemTypeFilterChanged -= OnChangedItemTypeFilterChanged;

        private sealed class ChangedItemItemFilter : TextFilterBase<string>
        {
            public ChangedItemItemFilter(Configuration config)
            {
                Set(config.Ui.RememberChangedItemFilters ? config.Filters.ChangedItemItemFilter : string.Empty);
                FilterChanged += () => config.Filters.ChangedItemItemFilter = Text;
            }

            protected override string ToFilterString(in string item, int globalIndex)
                => item;
        }

        private sealed class ChangedItemModFilter : TextFilterBase<string>
        {
            public ChangedItemModFilter(Configuration config)
            {
                Set(config.Ui.RememberChangedItemFilters ? config.Filters.ChangedItemModFilter : string.Empty);
                FilterChanged += () => config.Filters.ChangedItemModFilter = Text;
            }

            protected override string ToFilterString(in string item, int globalIndex)
                => item;
        }
    }

    private readonly struct Item(string label, IIdentifiedObjectData data, SingleArray<IMod> mods)
    {
        public readonly StringPair            Name         = new(data.ToName(label));
        public readonly StringU8              Mod          = mods.Count > 0 ? new StringU8(mods[0].Name) : StringU8.Empty;
        public readonly StringU8              ModelData    = new(data.AdditionalData);
        public readonly ChangedItemIconFlag   CategoryIcon = data.GetIcon().ToFlag();
        public readonly IIdentifiedObjectData Data         = data;
        public readonly SingleArray<IMod>     Mods         = mods;

        public readonly Lazy<StringU8> Tooltip = mods.Count > 1
            ? new Lazy<StringU8>(()
                => new StringU8($"Other mods affecting this item:\n{StringU8.Join((byte)'\n', mods.Skip(1).Select(m => m.Name))}"))
            : StringU8.EmptyLazy;
    }

    private sealed class Cache : BasicFilterCache<Item>
    {
        private readonly EphemeralConfig   _config;
        private readonly ActiveCollections _collections;
        private readonly CollectionChange  _collectionChange;

        public Cache(EphemeralConfig config, ActiveCollections collections, CommunicatorService communicator, IFilter<Item> filter)
            : base(filter)
        {
            _config           = config;
            _collections      = collections;
            _collectionChange = communicator.CollectionChange;
            _collectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.ChangedItemsTabCache);
            _config.ChangedItemSortModsChanged += OnChangedItemSortModeChanged;
        }

        private void OnChangedItemSortModeChanged(ChangedItemSortMode arg1, ChangedItemSortMode arg2)
            => Dirty |= IManagedCache.DirtyFlags.Custom;


        private void OnCollectionChange(in CollectionChange.Arguments arguments)
            => FilterDirty = true;

        protected override IEnumerable<Item> GetItems()
        {
            var items = _collections.Current.ChangedItems.Select(kvp => new Item(kvp.Key, kvp.Value.Item2, kvp.Value.Item1));
            if (_config.ChangedItemSortMode is ChangedItemSortMode.Item)
                return items;

            return items.OrderBy(i => i.Mod).ThenBy(i => i.Name);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _collectionChange.Unsubscribe(OnCollectionChange);
            _config.ChangedItemSortModsChanged -= OnChangedItemSortModeChanged;
        }
    }

    public void DrawContent()
    {
        collectionHeader.Draw(true);
        drawer.DrawTypeFilter(true);
        _filter.DrawFilter("##Filter"u8, Im.ContentRegion.Available.AddX(-Im.Style.FrameHeight - Im.Style.ItemInnerSpacing.X));
        Im.Line.SameInner();
        if (Im.Checkbox("##sort"u8, config.Ephemeral.ChangedItemSortMode is ChangedItemSortMode.Mod))
            config.Ephemeral.ChangedItemSortMode = config.Ephemeral.ChangedItemSortMode is ChangedItemSortMode.Mod
                ? ChangedItemSortMode.Item
                : ChangedItemSortMode.Mod;
        Im.Tooltip.OnHover("Sort the changed items by Mod instead of by Item label."u8);
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
          - ItemColumnWidth * Im.Style.GlobalScale
          - Im.Style.ItemSpacing.X;
        const TableColumnFlags flags = TableColumnFlags.NoResize | TableColumnFlags.WidthFixed;
        table.SetupColumn("items"u8, flags, ItemColumnWidth * Im.Style.GlobalScale);
        table.SetupColumn("mods"u8,  flags, varWidth - DataColumnWidth * Im.Style.GlobalScale);
        table.SetupColumn("id"u8,    flags, DataColumnWidth * Im.Style.GlobalScale);

        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current,
            () => new Cache(config.Ephemeral, collectionManager.Active, communicator, _filter));
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
        var clicked = Im.Selectable(item.Name.Utf8, false, SelectableFlags.None, _buttonSize with { X = 0 });
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
            navigator.MoveTo(mod);

        if (!Im.Item.Hovered())
            return;

        using var _ = Im.Tooltip.Begin();
        Im.Text("Hold Control and click to jump to mod.\n"u8);
        if (item.Tooltip != StringU8.EmptyLazy)
            Im.Text(item.Tooltip.Value);
    }

    public void Dispose()
        => _filter.Dispose();
}
