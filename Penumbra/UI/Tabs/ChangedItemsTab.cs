using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.Tabs;

public class ChangedItemsTab : ITab
{
    private readonly CollectionManager _collectionManager;
    private readonly PenumbraApi           _api;

    public ChangedItemsTab(CollectionManager collectionManager, PenumbraApi api)
    {
        _collectionManager = collectionManager;
        _api               = api;
    }

    public ReadOnlySpan<byte> Label
        => "Changed Items"u8;

    private LowerString _changedItemFilter    = LowerString.Empty;
    private LowerString _changedItemModFilter = LowerString.Empty;

    public void DrawContent()
    {
        var       varWidth = DrawFilters();
        using var child    = ImRaii.Child("##changedItemsChild", -Vector2.One);
        if (!child)
            return;

        var       height = ImGui.GetTextLineHeightWithSpacing() + 2 * ImGui.GetStyle().CellPadding.Y;
        var       skips  = ImGuiClip.GetNecessarySkips(height);
        using var list   = ImRaii.Table("##changedItems", 3, ImGuiTableFlags.RowBg, -Vector2.One);
        if (!list)
            return;

        const ImGuiTableColumnFlags flags = ImGuiTableColumnFlags.NoResize | ImGuiTableColumnFlags.WidthFixed;
        ImGui.TableSetupColumn("items", flags, 400 * UiHelpers.Scale);
        ImGui.TableSetupColumn("mods",  flags, varWidth - 120 * UiHelpers.Scale);
        ImGui.TableSetupColumn("id",    flags, 120 * UiHelpers.Scale);

        var items = _collectionManager.Current.ChangedItems;
        var rest = _changedItemFilter.IsEmpty && _changedItemModFilter.IsEmpty
            ? ImGuiClip.ClippedDraw(items, skips, DrawChangedItemColumn, items.Count)
            : ImGuiClip.FilteredClippedDraw(items, skips, FilterChangedItem, DrawChangedItemColumn);
        ImGuiClip.DrawEndDummy(rest, height);
    }

    /// <summary> Draw a pair of filters and return the variable width of the flexible column. </summary>
    private float DrawFilters()
    {
        var varWidth = ImGui.GetContentRegionAvail().X
          - 400 * UiHelpers.Scale
          - ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetNextItemWidth(400 * UiHelpers.Scale);
        LowerString.InputWithHint("##changedItemsFilter", "Filter Item...", ref _changedItemFilter, 128);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(varWidth);
        LowerString.InputWithHint("##changedItemsModFilter", "Filter Mods...", ref _changedItemModFilter, 128);
        return varWidth;
    }

    /// <summary> Apply the current filters. </summary>
    private bool FilterChangedItem(KeyValuePair<string, (SingleArray<IMod>, object?)> item)
        => (_changedItemFilter.IsEmpty
             || UiHelpers.ChangedItemName(item.Key, item.Value.Item2)
                    .Contains(_changedItemFilter.Lower, StringComparison.OrdinalIgnoreCase))
         && (_changedItemModFilter.IsEmpty || item.Value.Item1.Any(m => m.Name.Contains(_changedItemModFilter)));

    /// <summary> Draw a full column for a changed item. </summary>
    private void DrawChangedItemColumn(KeyValuePair<string, (SingleArray<IMod>, object?)> item)
    {
        ImGui.TableNextColumn();
        UiHelpers.DrawChangedItem(_api, item.Key, item.Value.Item2, false);
        ImGui.TableNextColumn();
        if (item.Value.Item1.Count > 0)
        {
            ImGui.TextUnformatted(item.Value.Item1[0].Name);
            if (item.Value.Item1.Count > 1 && ImGui.IsItemHovered())
                ImGui.SetTooltip(string.Join("\n", item.Value.Item1.Skip(1).Select(m => m.Name)));
        }

        ImGui.TableNextColumn();
        if (!UiHelpers.GetChangedItemObject(item.Value.Item2, out var text))
            return;

        using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.ItemId.Value(Penumbra.Config));
        ImGuiUtil.RightAlign(text);
    }
}
