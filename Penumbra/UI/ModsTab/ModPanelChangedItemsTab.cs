using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Mods;
using Penumbra.String;

namespace Penumbra.UI.ModsTab;

public class ModPanelChangedItemsTab(
    ModFileSystemSelector selector,
    ChangedItemDrawer drawer,
    ImGuiCacheService cacheService,
    Configuration config)
    : ITab, IUiService
{
    private readonly ImGuiCacheService.CacheId _cacheId = cacheService.GetNewId();

    private class ChangedItemsCache
    {
        private         Mod?                _lastSelected;
        private         ushort              _lastUpdate;
        private         ChangedItemIconFlag _filter = ChangedItemFlagExtensions.DefaultFlags;
        private         ChangedItemMode     _lastMode;
        private         bool                _reset;
        public readonly List<Container>     Data = [];
        public          bool                AnyExpandable { get; private set; }

        public record struct Container
        {
            public IIdentifiedObjectData Data;
            public ByteString            Text;
            public ByteString            ModelData;
            public uint                  Id;
            public int                   Children;
            public ChangedItemIconFlag   Icon;
            public bool                  Expandable;
            public bool                  Expanded;
            public bool                  Child;

            public static Container Single(string text, IIdentifiedObjectData data)
                => new()
                {
                    Child      = false,
                    Text       = ByteString.FromStringUnsafe(data.ToName(text),   false),
                    ModelData  = ByteString.FromStringUnsafe(data.AdditionalData, false),
                    Icon       = data.GetIcon().ToFlag(),
                    Expandable = false,
                    Expanded   = false,
                    Data       = data,
                    Id         = 0,
                    Children   = 0,
                };

            public static Container Parent(string text, IIdentifiedObjectData data, uint id, int children, bool expanded)
                => new()
                {
                    Child      = false,
                    Text       = ByteString.FromStringUnsafe(data.ToName(text),   false),
                    ModelData  = ByteString.FromStringUnsafe(data.AdditionalData, false),
                    Icon       = data.GetIcon().ToFlag(),
                    Expandable = true,
                    Expanded   = expanded,
                    Data       = data,
                    Id         = id,
                    Children   = children,
                };

            public static Container Indent(string text, IIdentifiedObjectData data)
                => new()
                {
                    Child      = true,
                    Text       = ByteString.FromStringUnsafe(data.ToName(text),           false),
                    ModelData  = ByteString.FromStringUnsafe(data.AdditionalData, false),
                    Icon       = data.GetIcon().ToFlag(),
                    Expandable = false,
                    Expanded   = false,
                    Data       = data,
                    Id         = 0,
                    Children   = 0,
                };
        }

        public void Reset()
            => _reset = true;

        public void Update(Mod? mod, ChangedItemDrawer drawer, ChangedItemIconFlag filter, ChangedItemMode mode)
        {
            if (mod == _lastSelected && _lastSelected!.LastChangedItemsUpdate == _lastUpdate && _filter == filter && !_reset && _lastMode == mode)
                return;

            _reset = false;
            Data.Clear();
            AnyExpandable = false;
            _lastSelected = mod;
            _filter       = filter;
            _lastMode     = mode;
            if (_lastSelected == null)
                return;

            _lastUpdate = _lastSelected.LastChangedItemsUpdate;

            if (mode is ChangedItemMode.Alphabetical)
            {
                foreach (var (s, i) in _lastSelected.ChangedItems)
                {
                    if (drawer.FilterChangedItem(s, i, LowerString.Empty))
                        Data.Add(Container.Single(s, i));
                }

                return;
            }

            var tmp              = new Dictionary<(PrimaryId, FullEquipType), List<IdentifiedItem>>();
            var defaultExpansion = _lastMode is ChangedItemMode.GroupedExpanded;
            foreach (var (s, i) in _lastSelected.ChangedItems)
            {
                if (i is not IdentifiedItem item)
                    continue;

                if (!drawer.FilterChangedItem(s, item, LowerString.Empty))
                    continue;

                if (tmp.TryGetValue((item.Item.PrimaryId, item.Item.Type), out var p))
                    p.Add(item);
                else
                    tmp[(item.Item.PrimaryId, item.Item.Type)] = [item];
            }

            foreach (var list in tmp.Values)
            {
                list.Sort((i1, i2) =>
                {
                    // reversed
                    var count = i2.Count.CompareTo(i1.Count);
                    if (count != 0)
                        return count;

                    return string.Compare(i1.Item.Name, i2.Item.Name, StringComparison.Ordinal);
                });
            }

            var sortedTmp = tmp.Values.OrderBy(s => s[0].Item.Name).ToArray();

            var sortedTmpIdx = 0;
            foreach (var (s, i) in _lastSelected.ChangedItems)
            {
                if (i is IdentifiedItem)
                    continue;

                if (!drawer.FilterChangedItem(s, i, LowerString.Empty))
                    continue;

                while (sortedTmpIdx < sortedTmp.Length
                    && string.Compare(sortedTmp[sortedTmpIdx][0].Item.Name, s, StringComparison.Ordinal) <= 0)
                    AddList(sortedTmp[sortedTmpIdx++]);

                Data.Add(Container.Single(s, i));
            }

            for (; sortedTmpIdx < sortedTmp.Length; ++sortedTmpIdx)
                AddList(sortedTmp[sortedTmpIdx]);
            return;

            void AddList(List<IdentifiedItem> list)
            {
                var mainItem = list[0];
                if (list.Count == 1)
                {
                    Data.Add(Container.Single(mainItem.Item.Name, mainItem));
                }
                else
                {
                    var id       = ImUtf8.GetId($"{mainItem.Item.PrimaryId}{(int)mainItem.Item.Type}");
                    var expanded = ImGui.GetStateStorage().GetBool(id, defaultExpansion);
                    Data.Add(Container.Parent(mainItem.Item.Name, mainItem, id, list.Count - 1, expanded));
                    AnyExpandable = true;
                    if (!expanded)
                        return;

                    foreach (var item in list.Skip(1))
                        Data.Add(Container.Indent(item.Item.Name, item));
                }
            }
        }
    }

    public ReadOnlySpan<byte> Label
        => "Changed Items"u8;

    public bool IsVisible
        => selector.Selected!.ChangedItems.Count > 0;

    private ImGuiStoragePtr _stateStorage;

    private Vector2 _buttonSize;

    public void DrawContent()
    {
        if (cacheService.Cache(_cacheId, () => (new ChangedItemsCache(), "ModPanelChangedItemsCache")) is not { } cache)
            return;

        drawer.DrawTypeFilter();

        _stateStorage = ImGui.GetStateStorage();
        cache.Update(selector.Selected, drawer, config.Ephemeral.ChangedItemFilter, config.ChangedItemDisplay);
        ImGui.Separator();
        _buttonSize = new Vector2(ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeight());
        using var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, Vector2.Zero)
            .Push(ImGuiStyleVar.ItemSpacing,         Vector2.Zero)
            .Push(ImGuiStyleVar.FramePadding,        Vector2.Zero)
            .Push(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.01f, 0.5f));

        using var table = ImUtf8.Table("##changedItems"u8, cache.AnyExpandable ? 2 : 1, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(ImGui.GetContentRegionAvail().X, -1));
        if (!table)
            return;

        if (cache.AnyExpandable)
        {
            ImUtf8.TableSetupColumn("##exp"u8,  ImGuiTableColumnFlags.WidthFixed, _buttonSize.Y);
            ImUtf8.TableSetupColumn("##text"u8, ImGuiTableColumnFlags.WidthStretch);
            ImGuiClip.ClippedDraw(cache.Data, DrawContainerExpandable, _buttonSize.Y);
        }
        else
        {
            ImGuiClip.ClippedDraw(cache.Data, DrawContainer, ImGui.GetFrameHeightWithSpacing());
        }
    }

    private void DrawContainerExpandable(ChangedItemsCache.Container obj, int idx)
    {
        using var id = ImUtf8.PushId(idx);
        ImGui.TableNextColumn();
        if (obj.Expandable)
        {
            using var color = ImRaii.PushColor(ImGuiCol.Button, 0);
            if (ImUtf8.IconButton(obj.Expanded ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight,
                    obj.Expanded     ? "Hide the other items using the same model." :
                    obj.Children > 1 ? $"Show {obj.Children} other items using the same model." :
                                       "Show one other item using the same model.",
                    _buttonSize))
            {
                _stateStorage.SetBool(obj.Id, !obj.Expanded);
                if (cacheService.TryGetCache<ChangedItemsCache>(_cacheId, out var cache))
                    cache.Reset();
            }
        }
        else
        {
            ImGui.Dummy(_buttonSize);
        }

        DrawBaseContainer(obj, idx);
    }

    private void DrawContainer(ChangedItemsCache.Container obj, int idx)
    {
        using var id = ImUtf8.PushId(idx);
        DrawBaseContainer(obj, idx);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawBaseContainer(in ChangedItemsCache.Container obj, int idx)
    {
        ImGui.TableNextColumn();
        using var indent = ImRaii.PushIndent(1, obj.Child);
        drawer.DrawCategoryIcon(obj.Icon, _buttonSize.Y);
        ImGui.SameLine(0, 0);
        var clicked = ImUtf8.Selectable(obj.Text.Span, false, ImGuiSelectableFlags.None, _buttonSize with { X = 0 });
        drawer.ChangedItemHandling(obj.Data, clicked);
        ChangedItemDrawer.DrawModelData(obj.ModelData.Span, _buttonSize.Y);
    }
}
