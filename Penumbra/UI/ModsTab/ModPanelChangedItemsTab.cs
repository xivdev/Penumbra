using System;
using System.Collections.Generic;
using System.Numerics;
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
        using var list = ImRaii.ListBox("##changedItems", -Vector2.One);
        if (!list)
            return;

        var zipList = ZipList.FromSortedList((SortedList<string, object?>)_selector.Selected!.ChangedItems);
        var height  = ImGui.GetFrameHeight();
        ImGuiClip.ClippedDraw(zipList, kvp => _drawer.DrawChangedItem(kvp.Item1, kvp.Item2, true), height);
    }
}
