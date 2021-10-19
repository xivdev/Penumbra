using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using Penumbra.Mods;
using Penumbra.UI.Custom;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabChangedItems
        {
            private const    string            LabelTab = "Changed Items";
            private readonly ModManager        _modManager;
            private readonly SettingsInterface _base;

            private string _filter      = string.Empty;
            private string _filterLower = string.Empty;

            public TabChangedItems( SettingsInterface ui )
            {
                _base       = ui;
                _modManager = Service< ModManager >.Get();
            }

            public void Draw()
            {
                var items  = _modManager.Collections.ActiveCollection.Cache?.ChangedItems ?? new Dictionary< string, object? >();
                var forced = _modManager.Collections.ForcedCollection.Cache?.ChangedItems ?? new Dictionary< string, object? >();
                var count  = items.Count + forced.Count;
                if( count > 0 && !ImGui.BeginTabItem( LabelTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                ImGui.SetNextItemWidth( -1 );
                if( ImGui.InputTextWithHint( "##ChangedItemsFilter", "Filter...", ref _filter, 64 ) )
                {
                    _filterLower = _filter.ToLowerInvariant();
                }

                if( !ImGui.BeginTable( "##ChangedItemsTable", 1, ImGuiTableFlags.RowBg, AutoFillSize ) )
                {
                    return;
                }

                raii.Push( ImGui.EndTable );

                var list = items.AsEnumerable();
                if( forced.Count > 0 )
                {
                    list = list.Concat( forced ).OrderBy( kvp => kvp.Key );
                }

                if( _filter.Any() )
                {
                    list = list.Where( kvp => kvp.Key.ToLowerInvariant().Contains( _filterLower ) );
                }

                foreach( var (name, data) in list )
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    _base.DrawChangedItem( name, data );
                }
            }
        }
    }
}