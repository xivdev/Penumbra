using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private LowerString _changedItemFilter = LowerString.Empty;

    public void DrawChangedItemTab()
    {
        using var tab = ImRaii.TabItem( "Changed Items" );
        if( !tab )
        {
            return;
        }

        ImGui.SetNextItemWidth( -1 );
        LowerString.InputWithHint( "##changedItemsFilter", "Filter...", ref _changedItemFilter, 64 );

        using var child = ImRaii.Child( "##changedItemsChild", -Vector2.One );
        if( !child )
        {
            return;
        }

        var       height = ImGui.GetTextLineHeightWithSpacing() + 2 * ImGui.GetStyle().CellPadding.Y;
        var       skips  = ImGuiClip.GetNecessarySkips( height );
        using var list   = ImRaii.Table( "##changedItems", 1, ImGuiTableFlags.RowBg, -Vector2.One );
        if( !list )
        {
            return;
        }

        var items  = Penumbra.CollectionManager.Default.ChangedItems;
        var rest = _changedItemFilter.IsEmpty
            ? ImGuiClip.ClippedDraw( items, skips, DrawChangedItem, items.Count )
            : ImGuiClip.FilteredClippedDraw( items, skips, FilterChangedItem, DrawChangedItem );
        ImGuiClip.DrawEndDummy( rest, height );
    }

    private bool FilterChangedItem( KeyValuePair< string, object? > item )
        => item.Key.Contains( _changedItemFilter.Lower, StringComparison.InvariantCultureIgnoreCase );

    private void DrawChangedItem( KeyValuePair< string, object? > item )
    {
        ImGui.TableNextColumn();
        DrawChangedItem( item.Key, item.Value, ImGui.GetStyle().ScrollbarSize );
    }
}