using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;

namespace Penumbra.UI.ModsTab;

public class ModPanelChangedItemsTab : ITab
{
    private readonly ModFileSystemSelector _selector;
    private readonly ChangedItemDrawer     _drawer;

    private ChangedItemDrawer.ChangedItemIcon _filter = Enum.GetValues<ChangedItemDrawer.ChangedItemIcon>().Aggregate((a, b) => a | b);

    public ReadOnlySpan<byte> Label
        => "Changed Items"u8;

    public ModPanelChangedItemsTab(ModFileSystemSelector selector, ChangedItemDrawer drawer)
    {
        _selector = selector;
        _drawer   = drawer;
    }

    public bool IsVisible
        => _selector.Selected!.ChangedItems.Count > 0;

    public void DrawContent()
    {
        _drawer.DrawTypeFilter();
        ImGui.Separator();
        using var table = ImRaii.Table("##changedItems", 1, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(ImGui.GetContentRegionAvail().X, -1));
        if (!table)
            return;

        var zipList = ZipList.FromSortedList((SortedList<string, object?>)_selector.Selected!.ChangedItems);
        var height  = ImGui.GetFrameHeightWithSpacing();
        ImGui.TableNextColumn();
        var skips     = ImGuiClip.GetNecessarySkips(height);
        var remainder = ImGuiClip.FilteredClippedDraw(zipList, skips, CheckFilter, DrawChangedItem);
        ImGuiClip.DrawEndDummy(remainder, height);
    }

    private bool CheckFilter((string Name, object? Data) kvp)
        => _drawer.FilterChangedItem(kvp.Name, kvp.Data, LowerString.Empty);

    private void DrawChangedItem((string Name, object? Data) kvp)
    {
        ImGui.TableNextColumn();
        _drawer.DrawCategoryIcon(kvp.Name, kvp.Data);
        ImGui.SameLine();
        _drawer.DrawChangedItem(kvp.Name, kvp.Data);
        _drawer.DrawModelData(kvp.Data);
    }
}
