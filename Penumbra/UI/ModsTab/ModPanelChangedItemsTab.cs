using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Widgets;

namespace Penumbra.UI.ModsTab;

public class ModPanelChangedItemsTab(ModFileSystemSelector selector, ChangedItemDrawer drawer) : ITab, IUiService
{
    public ReadOnlySpan<byte> Label
        => "Changed Items"u8;

    public bool IsVisible
        => selector.Selected!.ChangedItems.Count > 0;

    public void DrawContent()
    {
        drawer.DrawTypeFilter();
        ImGui.Separator();
        using var table = ImRaii.Table("##changedItems", 1, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(ImGui.GetContentRegionAvail().X, -1));
        if (!table)
            return;

        var zipList = ZipList.FromSortedList((SortedList<string, object?>)selector.Selected!.ChangedItems);
        var height  = ImGui.GetFrameHeightWithSpacing();
        ImGui.TableNextColumn();
        var skips     = ImGuiClip.GetNecessarySkips(height);
        var remainder = ImGuiClip.FilteredClippedDraw(zipList, skips, CheckFilter, DrawChangedItem);
        ImGuiClip.DrawEndDummy(remainder, height);
    }

    private bool CheckFilter((string Name, object? Data) kvp)
        => drawer.FilterChangedItem(kvp.Name, kvp.Data, LowerString.Empty);

    private void DrawChangedItem((string Name, object? Data) kvp)
    {
        ImGui.TableNextColumn();
        drawer.DrawCategoryIcon(kvp.Name, kvp.Data);
        ImGui.SameLine();
        drawer.DrawChangedItem(kvp.Name, kvp.Data);
        drawer.DrawModelData(kvp.Data);
    }
}
