using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private LowerString _changedItemFilter = LowerString.Empty;

    // Draw a simple clipped table containing all changed items.
    private void DrawChangedItemTab()
    {
        // Functions in here for less pollution.
        bool FilterChangedItem( KeyValuePair< string, object? > item )
            => item.Key.Contains( _changedItemFilter.Lower, StringComparison.InvariantCultureIgnoreCase );

        void DrawChangedItemColumn( KeyValuePair< string, object? > item )
        {
            ImGui.TableNextColumn();
            DrawChangedItem( item.Key, item.Value, ImGui.GetStyle().ScrollbarSize );
        }

        using var tab = ImRaii.TabItem( "Changed Items" );
        if( !tab )
        {
            return;
        }

        // Draw filters.
        ImGui.SetNextItemWidth( -1 );
        LowerString.InputWithHint( "##changedItemsFilter", "Filter...", ref _changedItemFilter, 64 );

        using var child = ImRaii.Child( "##changedItemsChild", -Vector2.One );
        if( !child )
        {
            return;
        }

        // Draw table of changed items.
        var       height = ImGui.GetTextLineHeightWithSpacing() + 2 * ImGui.GetStyle().CellPadding.Y;
        var       skips  = ImGuiClip.GetNecessarySkips( height );
        using var list   = ImRaii.Table( "##changedItems", 1, ImGuiTableFlags.RowBg, -Vector2.One );
        if( !list )
        {
            return;
        }

        var items = Penumbra.CollectionManager.Default.ChangedItems;
        var rest = _changedItemFilter.IsEmpty
            ? ImGuiClip.ClippedDraw( items, skips, DrawChangedItemColumn, items.Count )
            : ImGuiClip.FilteredClippedDraw( items, skips, FilterChangedItem, DrawChangedItemColumn );
        ImGuiClip.DrawEndDummy( rest, height );
    }
}