using Dalamud.Bindings.ImGui;
using ImSharp;
using Luna;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.Tabs;

public sealed class ChangedItemsTab(
    CollectionManager collectionManager,
    CollectionSelectHeader collectionHeader,
    ChangedItemDrawer drawer,
    CommunicatorService communicator)
    : ITab<TabType>
{
    public ReadOnlySpan<byte> Label
        => "Changed Items"u8;

    public TabType Identifier
        => TabType.ChangedItems;

    private         string  _changedItemFilter    = string.Empty;
    private         string  _changedItemModFilter = string.Empty;
    private         Vector2 _buttonSize;

    public void DrawContent()
    {
        collectionHeader.Draw(true);
        drawer.DrawTypeFilter();
        var       varWidth = DrawFilters();
        using var child    = ImUtf8.Child("##changedItemsChild"u8, -Vector2.One);
        if (!child)
            return;

        _buttonSize = new Vector2(Im.Style.ItemSpacing.Y + Im.Style.FrameHeight);
        using var style = ImStyleDouble.CellPadding.Push(Vector2.Zero)
            .Push(ImStyleDouble.ItemSpacing,         Vector2.Zero)
            .Push(ImStyleDouble.FramePadding,        Vector2.Zero)
            .Push(ImStyleDouble.SelectableTextAlign, new Vector2(0.01f, 0.5f));

        var       skips = ImGuiClip.GetNecessarySkips(_buttonSize.Y);
        using var table = Im.Table.Begin("##changedItems"u8, 3, TableFlags.RowBackground, -Vector2.One);
        if (!table)
            return;

        const TableColumnFlags flags = TableColumnFlags.NoResize | TableColumnFlags.WidthFixed;
        table.SetupColumn("items"u8, flags, 450 * Im.Style.GlobalScale);
        table.SetupColumn("mods"u8,  flags, varWidth - 140 * Im.Style.GlobalScale);
        table.SetupColumn("id"u8,    flags, 140 * Im.Style.GlobalScale);

        var items = collectionManager.Active.Current.ChangedItems;
        var rest  = ImGuiClip.FilteredClippedDraw(items, skips, FilterChangedItem, DrawChangedItemColumn);
        ImGuiClip.DrawEndDummy(rest, _buttonSize.Y);
    }

    /// <summary> Draw a pair of filters and return the variable width of the flexible column. </summary>
    private float DrawFilters()
    {
        var varWidth = Im.ContentRegion.Available.X
          - 450 * Im.Style.GlobalScale
          - Im.Style.ItemSpacing.X;
        Im.Item.SetNextWidth(450 * Im.Style.GlobalScale);
        Im.Input.Text("##changedItemsFilter"u8, ref _changedItemFilter, "Filter Item..."u8);
        Im.Line.Same();
        Im.Item.SetNextWidth(varWidth);
        Im.Input.Text("##changedItemsModFilter"u8, ref _changedItemModFilter, "Filter Mods..."u8);
        return varWidth;
    }

    /// <summary> Apply the current filters. </summary>
    private bool FilterChangedItem(KeyValuePair<string, (Luna.SingleArray<IMod>, IIdentifiedObjectData)> item)
        => drawer.FilterChangedItem(item.Key, item.Value.Item2, _changedItemFilter)
         && (_changedItemModFilter.Length is 0
             || item.Value.Item1.Any(m => m.Name.Contains(_changedItemModFilter, StringComparison.OrdinalIgnoreCase)));

    /// <summary> Draw a full column for a changed item. </summary>
    private void DrawChangedItemColumn(KeyValuePair<string, (Luna.SingleArray<IMod>, IIdentifiedObjectData)> item)
    {
        ImGui.TableNextColumn();
        drawer.DrawCategoryIcon(item.Value.Item2, _buttonSize.Y);
        Im.Line.Same(0, 0);
        var name    = item.Value.Item2.ToName(item.Key);
        var clicked = ImUtf8.Selectable(name, false, ImGuiSelectableFlags.None, _buttonSize with { X = 0 });
        drawer.ChangedItemHandling(item.Value.Item2, clicked);

        ImGui.TableNextColumn();
        DrawModColumn(item.Value.Item1);

        ImGui.TableNextColumn();
        ChangedItemDrawer.DrawModelData(item.Value.Item2, _buttonSize.Y);
    }

    private void DrawModColumn(Luna.SingleArray<IMod> mods)
    {
        if (mods.Count <= 0)
            return;

        var first = mods[0];
        if (ImUtf8.Selectable(first.Name, false, ImGuiSelectableFlags.None, _buttonSize with { X = 0 })
         && ImGui.GetIO().KeyCtrl
         && first is Mod mod)
            communicator.SelectTab.Invoke(new SelectTab.Arguments(Api.Enums.TabType.Mods, mod));

        if (!Im.Item.Hovered())
            return;

        using var _ = ImRaii.Tooltip();
        ImUtf8.Text("Hold Control and click to jump to mod.\n"u8);
        if (mods.Count > 1)
            ImUtf8.Text("Other mods affecting this item:\n" + string.Join("\n", mods.Skip(1).Select(m => m.Name)));
    }
}
