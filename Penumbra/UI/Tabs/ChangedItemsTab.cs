using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Data;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.Tabs;

public class ChangedItemsTab(
    CollectionManager collectionManager,
    CollectionSelectHeader collectionHeader,
    ChangedItemDrawer drawer,
    CommunicatorService communicator)
    : ITab, IUiService
{
    public ReadOnlySpan<byte> Label
        => "Changed Items"u8;

    private LowerString _changedItemFilter    = LowerString.Empty;
    private LowerString _changedItemModFilter = LowerString.Empty;
    private Vector2     _buttonSize;

    public void DrawContent()
    {
        collectionHeader.Draw(true);
        drawer.DrawTypeFilter();
        var       varWidth = DrawFilters();
        using var child    = ImUtf8.Child("##changedItemsChild"u8, -Vector2.One);
        if (!child)
            return;

        _buttonSize = new Vector2(ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeight());
        using var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, Vector2.Zero)
            .Push(ImGuiStyleVar.ItemSpacing,         Vector2.Zero)
            .Push(ImGuiStyleVar.FramePadding,        Vector2.Zero)
            .Push(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.01f, 0.5f));

        var       skips = ImGuiClip.GetNecessarySkips(_buttonSize.Y);
        using var list  = ImUtf8.Table("##changedItems"u8, 3, ImGuiTableFlags.RowBg, -Vector2.One);
        if (!list)
            return;

        const ImGuiTableColumnFlags flags = ImGuiTableColumnFlags.NoResize | ImGuiTableColumnFlags.WidthFixed;
        ImUtf8.TableSetupColumn("items"u8, flags, 450 * UiHelpers.Scale);
        ImUtf8.TableSetupColumn("mods"u8,  flags, varWidth - 140 * UiHelpers.Scale);
        ImUtf8.TableSetupColumn("id"u8,    flags, 140 * UiHelpers.Scale);

        var items = collectionManager.Active.Current.ChangedItems;
        var rest  = ImGuiClip.FilteredClippedDraw(items, skips, FilterChangedItem, DrawChangedItemColumn);
        ImGuiClip.DrawEndDummy(rest, _buttonSize.Y);
    }

    /// <summary> Draw a pair of filters and return the variable width of the flexible column. </summary>
    private float DrawFilters()
    {
        var varWidth = ImGui.GetContentRegionAvail().X
          - 450 * UiHelpers.Scale
          - ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetNextItemWidth(450 * UiHelpers.Scale);
        LowerString.InputWithHint("##changedItemsFilter", "Filter Item...", ref _changedItemFilter, 128);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(varWidth);
        LowerString.InputWithHint("##changedItemsModFilter", "Filter Mods...", ref _changedItemModFilter, 128);
        return varWidth;
    }

    /// <summary> Apply the current filters. </summary>
    private bool FilterChangedItem(KeyValuePair<string, (SingleArray<IMod>, IIdentifiedObjectData)> item)
        => drawer.FilterChangedItem(item.Key, item.Value.Item2, _changedItemFilter)
         && (_changedItemModFilter.IsEmpty || item.Value.Item1.Any(m => m.Name.Contains(_changedItemModFilter)));

    /// <summary> Draw a full column for a changed item. </summary>
    private void DrawChangedItemColumn(KeyValuePair<string, (SingleArray<IMod>, IIdentifiedObjectData)> item)
    {
        ImGui.TableNextColumn();
        drawer.DrawCategoryIcon(item.Value.Item2, _buttonSize.Y);
        ImGui.SameLine(0, 0);
        var name    = item.Value.Item2.ToName(item.Key);
        var clicked = ImUtf8.Selectable(name, false, ImGuiSelectableFlags.None, _buttonSize with { X = 0 });
        drawer.ChangedItemHandling(item.Value.Item2, clicked);

        ImGui.TableNextColumn();
        DrawModColumn(item.Value.Item1);

        ImGui.TableNextColumn();
        ChangedItemDrawer.DrawModelData(item.Value.Item2, _buttonSize.Y);
    }

    private void DrawModColumn(SingleArray<IMod> mods)
    {
        if (mods.Count <= 0)
            return;

        var first = mods[0];
        if (ImUtf8.Selectable(first.Name.Text, false, ImGuiSelectableFlags.None, _buttonSize with { X = 0 })
         && ImGui.GetIO().KeyCtrl
         && first is Mod mod)
            communicator.SelectTab.Invoke(TabType.Mods, mod);

        if (!ImGui.IsItemHovered())
            return;

        using var _ = ImRaii.Tooltip();
        ImUtf8.Text("Hold Control and click to jump to mod.\n"u8);
        if (mods.Count > 1)
            ImUtf8.Text("Other mods affecting this item:\n" + string.Join("\n", mods.Skip(1).Select(m => m.Name)));
    }
}
