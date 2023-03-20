using System;
using System.Numerics;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelChangedItemsTab : ITab
{
    private readonly ModFileSystemSelector _selector;
    private readonly PenumbraApi           _api;

    public ReadOnlySpan<byte> Label
        => "Changed Items"u8;

    public ModPanelChangedItemsTab(PenumbraApi api, ModFileSystemSelector selector)
    {
        _api      = api;
        _selector = selector;
    }

    public bool IsVisible
        => _selector.Selected!.ChangedItems.Count > 0;

    public void DrawContent()
    {
        using var list = ImRaii.ListBox("##changedItems", -Vector2.One);
        if (!list)
            return;

        var zipList = ZipList.FromSortedList(_selector.Selected!.ChangedItems);
        var height  = ImGui.GetTextLineHeight();
        ImGuiClip.ClippedDraw(zipList, kvp => UiHelpers.DrawChangedItem(_api, kvp.Item1, kvp.Item2, true), height);
    }
}
