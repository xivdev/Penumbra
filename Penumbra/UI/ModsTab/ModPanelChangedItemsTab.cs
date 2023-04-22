using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api;
using Penumbra.Services;

namespace Penumbra.UI.ModsTab;

public class ModPanelChangedItemsTab : ITab
{
    private readonly ModFileSystemSelector _selector;
    private readonly CommunicatorService   _communicator;

    public ReadOnlySpan<byte> Label
        => "Changed Items"u8;

    public ModPanelChangedItemsTab(ModFileSystemSelector selector, CommunicatorService communicator)
    {
        _selector     = selector;
        _communicator = communicator;
    }

    public bool IsVisible
        => _selector.Selected!.ChangedItems.Count > 0;

    public void DrawContent()
    {
        using var list = ImRaii.ListBox("##changedItems", -Vector2.One);
        if (!list)
            return;

        var zipList = ZipList.FromSortedList((SortedList<string, object?>)_selector.Selected!.ChangedItems);
        var height  = ImGui.GetTextLineHeight();
        ImGuiClip.ClippedDraw(zipList, kvp => UiHelpers.DrawChangedItem(_communicator, kvp.Item1, kvp.Item2, true), height);
    }
}
