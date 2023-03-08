using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    // Draw a simple clipped table containing all changed items.
    public class ChangedItemsTab : ITab
    {
        private readonly ConfigWindow _config;

        public ChangedItemsTab( ConfigWindow config )
            => _config = config;

        public ReadOnlySpan<byte> Label
            => "Changed Items"u8;

        private LowerString _changedItemFilter    = LowerString.Empty;
        private LowerString _changedItemModFilter = LowerString.Empty;

        public void DrawContent()
        {
            // Draw filters.
            var varWidth = ImGui.GetContentRegionAvail().X
              - 400 * ImGuiHelpers.GlobalScale
              - ImGui.GetStyle().ItemSpacing.X;
            ImGui.SetNextItemWidth( 400 * ImGuiHelpers.GlobalScale );
            LowerString.InputWithHint( "##changedItemsFilter", "Filter Item...", ref _changedItemFilter, 128 );
            ImGui.SameLine();
            ImGui.SetNextItemWidth( varWidth );
            LowerString.InputWithHint( "##changedItemsModFilter", "Filter Mods...", ref _changedItemModFilter, 128 );

            using var child = ImRaii.Child( "##changedItemsChild", -Vector2.One );
            if( !child )
            {
                return;
            }

            // Draw table of changed items.
            var       height = ImGui.GetTextLineHeightWithSpacing() + 2 * ImGui.GetStyle().CellPadding.Y;
            var       skips  = ImGuiClip.GetNecessarySkips( height );
            using var list   = ImRaii.Table( "##changedItems", 3, ImGuiTableFlags.RowBg, -Vector2.One );
            if( !list )
            {
                return;
            }

            const ImGuiTableColumnFlags flags = ImGuiTableColumnFlags.NoResize | ImGuiTableColumnFlags.WidthFixed;
            ImGui.TableSetupColumn( "items", flags, 400 * ImGuiHelpers.GlobalScale );
            ImGui.TableSetupColumn( "mods", flags, varWidth - 120 * ImGuiHelpers.GlobalScale );
            ImGui.TableSetupColumn( "id", flags, 120 * ImGuiHelpers.GlobalScale );

            var items = Penumbra.CollectionManager.Current.ChangedItems;
            var rest = _changedItemFilter.IsEmpty && _changedItemModFilter.IsEmpty
                ? ImGuiClip.ClippedDraw( items, skips, DrawChangedItemColumn, items.Count )
                : ImGuiClip.FilteredClippedDraw( items, skips, FilterChangedItem, DrawChangedItemColumn );
            ImGuiClip.DrawEndDummy( rest, height );
        }


        // Functions in here for less pollution.
        private bool FilterChangedItem( KeyValuePair< string, (SingleArray< IMod >, object?) > item )
            => ( _changedItemFilter.IsEmpty
                 || ChangedItemName( item.Key, item.Value.Item2 )
                       .Contains( _changedItemFilter.Lower, StringComparison.OrdinalIgnoreCase ) )
             && ( _changedItemModFilter.IsEmpty || item.Value.Item1.Any( m => m.Name.Contains( _changedItemModFilter ) ) );

        private void DrawChangedItemColumn( KeyValuePair< string, (SingleArray< IMod >, object?) > item )
        {
            ImGui.TableNextColumn();
            _config.DrawChangedItem( item.Key, item.Value.Item2, false );
            ImGui.TableNextColumn();
            if( item.Value.Item1.Count > 0 )
            {
                ImGui.TextUnformatted( item.Value.Item1[ 0 ].Name );
                if( item.Value.Item1.Count > 1 )
                {
                    ImGuiUtil.HoverTooltip( string.Join( "\n", item.Value.Item1.Skip( 1 ).Select( m => m.Name ) ) );
                }
            }

            ImGui.TableNextColumn();
            if( DrawChangedItemObject( item.Value.Item2, out var text ) )
            {
                using var color = ImRaii.PushColor( ImGuiCol.Text, ColorId.ItemId.Value() );
                ImGuiUtil.RightAlign( text );
            }
        }
    }
}