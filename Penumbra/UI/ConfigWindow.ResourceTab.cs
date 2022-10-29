using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.STD;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Interop.Loader;
using Penumbra.String.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private class ResourceTab
    {
        private readonly ConfigWindow _window;

        public ResourceTab( ConfigWindow window )
            => _window = window;

        private float  _hashColumnWidth;
        private float  _pathColumnWidth;
        private float  _refsColumnWidth;
        private string _resourceManagerFilter = string.Empty;

        // Draw a tab to iterate over the main resource maps and see what resources are currently loaded.
        public void Draw()
        {
            if( !Penumbra.Config.DebugMode )
            {
                return;
            }

            using var tab = ImRaii.TabItem( "Resource Manager" );
            if( !tab )
            {
                return;
            }

            // Filter for resources containing the input string.
            ImGui.SetNextItemWidth( -1 );
            ImGui.InputTextWithHint( "##resourceFilter", "Filter...", ref _resourceManagerFilter, Utf8GamePath.MaxGamePathLength );

            using var child = ImRaii.Child( "##ResourceManagerTab", -Vector2.One );
            if( !child )
            {
                return;
            }

            unsafe
            {
                ResourceLoader.IterateGraphs( DrawCategoryContainer );
            }
        }

        private unsafe void DrawResourceMap( ResourceCategory category, uint ext, StdMap< uint, Pointer< ResourceHandle > >* map )
        {
            if( map == null )
            {
                return;
            }

            var       label = GetNodeLabel( ( uint )category, ext, map->Count );
            using var tree  = ImRaii.TreeNode( label );
            if( !tree || map->Count == 0 )
            {
                return;
            }

            using var table = ImRaii.Table( "##table", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg );
            if( !table )
            {
                return;
            }

            ImGui.TableSetupColumn( "Hash", ImGuiTableColumnFlags.WidthFixed, _hashColumnWidth );
            ImGui.TableSetupColumn( "Ptr", ImGuiTableColumnFlags.WidthFixed, _hashColumnWidth );
            ImGui.TableSetupColumn( "Path", ImGuiTableColumnFlags.WidthFixed, _pathColumnWidth );
            ImGui.TableSetupColumn( "Refs", ImGuiTableColumnFlags.WidthFixed, _refsColumnWidth );
            ImGui.TableHeadersRow();

            ResourceLoader.IterateResourceMap( map, ( hash, r ) =>
            {
                // Filter unwanted names.
                if( _resourceManagerFilter.Length != 0
                && !r->FileName.ToString().Contains( _resourceManagerFilter, StringComparison.OrdinalIgnoreCase ) )
                {
                    return;
                }

                var address = $"0x{( ulong )r:X}";
                ImGuiUtil.TextNextColumn( $"0x{hash:X8}" );
                ImGui.TableNextColumn();
                ImGuiUtil.CopyOnClickSelectable( address );

                var resource = ( Interop.Structs.ResourceHandle* )r;
                ImGui.TableNextColumn();
                Text( resource );
                if( ImGui.IsItemClicked() )
                {
                    var data = Interop.Structs.ResourceHandle.GetData( resource );
                    if( data != null )
                    {
                        var length = ( int )Interop.Structs.ResourceHandle.GetLength( resource );
                        ImGui.SetClipboardText( string.Join( " ",
                            new ReadOnlySpan< byte >( data, length ).ToArray().Select( b => b.ToString( "X2" ) ) ) );
                    }
                }

                ImGuiUtil.HoverTooltip( "Click to copy byte-wise file data to clipboard, if any." );

                ImGuiUtil.TextNextColumn( r->RefCount.ToString() );
            } );
        }

        // Draw a full category for the resource manager.
        private unsafe void DrawCategoryContainer( ResourceCategory category,
            StdMap< uint, Pointer< StdMap< uint, Pointer< ResourceHandle > > > >* map, int idx )
        {
            if( map == null )
            {
                return;
            }

            using var tree = ImRaii.TreeNode( $"({( uint )category:D2}) {category} (Ex {idx}) - {map->Count}###{( uint )category}_{idx}" );
            if( tree )
            {
                SetTableWidths();
                ResourceLoader.IterateExtMap( map, ( ext, m ) => DrawResourceMap( category, ext, m ) );
            }
        }

        // Obtain a label for an extension node.
        private static string GetNodeLabel( uint label, uint type, ulong count )
        {
            var (lowest, mid1, mid2, highest) = Functions.SplitBytes( type );
            return highest == 0
                ? $"({type:X8}) {( char )mid2}{( char )mid1}{( char )lowest} - {count}###{label}{type}"
                : $"({type:X8}) {( char )highest}{( char )mid2}{( char )mid1}{( char )lowest} - {count}###{label}{type}";
        }

        // Set the widths for a resource table.
        private void SetTableWidths()
        {
            _hashColumnWidth = 100 * ImGuiHelpers.GlobalScale;
            _pathColumnWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - 300 * ImGuiHelpers.GlobalScale;
            _refsColumnWidth = 30 * ImGuiHelpers.GlobalScale;
        }
    }
}