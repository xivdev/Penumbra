using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.STD;
using ImGuiNET;
using Penumbra.GameData.ByteString;
using Penumbra.Interop.Loader;
using Penumbra.UI.Custom;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private static string GetNodeLabel( uint label, uint type, ulong count )
    {
        var byte1 = type >> 24;
        var byte2 = ( type >> 16 ) & 0xFF;
        var byte3 = ( type >> 8 )  & 0xFF;
        var byte4 = type           & 0xFF;
        return byte1 == 0
            ? $"({type:X8}) {( char )byte2}{( char )byte3}{( char )byte4} - {count}###{label}{type}Debug"
            : $"({type:X8}) {( char )byte1}{( char )byte2}{( char )byte3}{( char )byte4} - {count}###{label}{type}Debug";
    }

    private unsafe void DrawResourceMap( ResourceCategory category, uint ext, StdMap< uint, Pointer< ResourceHandle > >* map )
    {
        if( map == null )
        {
            return;
        }

        var label = GetNodeLabel( ( uint )category, ext, map->Count );
        if( !ImGui.TreeNodeEx( label ) )
        {
            return;
        }

        using var raii = ImGuiRaii.DeferredEnd( ImGui.TreePop );

        if( map->Count == 0 || !ImGui.BeginTable( $"##{label}_table", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg ) )
        {
            return;
        }

        raii.Push( ImGui.EndTable );

        ImGui.TableSetupColumn( "Hash", ImGuiTableColumnFlags.WidthFixed, 100 * ImGuiHelpers.GlobalScale );
        ImGui.TableSetupColumn( "Ptr", ImGuiTableColumnFlags.WidthFixed, 100  * ImGuiHelpers.GlobalScale );
        ImGui.TableSetupColumn( "Path", ImGuiTableColumnFlags.WidthFixed,
            ImGui.GetWindowContentRegionWidth() - 300 * ImGuiHelpers.GlobalScale );
        ImGui.TableSetupColumn( "Refs", ImGuiTableColumnFlags.WidthFixed, 30 * ImGuiHelpers.GlobalScale );
        ImGui.TableHeadersRow();

        ResourceLoader.IterateResourceMap( map, ( hash, r ) =>
        {
            if( _filter.Length != 0 && !r->FileName.ToString().Contains( _filter, StringComparison.InvariantCultureIgnoreCase ) )
            {
                return;
            }

            ImGui.TableNextColumn();
            ImGui.Text( $"0x{hash:X8}" );
            ImGui.TableNextColumn();
            var address = $"0x{( ulong )r:X}";
            ImGui.Text( address );
            if( ImGui.IsItemClicked() )
            {
                ImGui.SetClipboardText( address );
            }

            ref var name = ref r->FileName;
            ImGui.TableNextColumn();
            if( name.Capacity > 15 )
            {
                ImGuiNative.igTextUnformatted( name.BufferPtr, name.BufferPtr + name.Length );
            }
            else
            {
                fixed( byte* ptr = name.Buffer )
                {
                    ImGuiNative.igTextUnformatted( ptr, ptr + name.Length );
                }
            }

            if( ImGui.IsItemClicked() )
            {
                var data = ( ( Interop.Structs.ResourceHandle* )r )->GetData();
                ImGui.SetClipboardText( string.Join( " ",
                    new ReadOnlySpan< byte >( ( byte* )data.Data, data.Length ).ToArray().Select( b => b.ToString( "X2" ) ) ) );
                //ImGuiNative.igSetClipboardText( ( byte* )Structs.ResourceHandle.GetData( ( IntPtr )r ) );
            }

            ImGui.TableNextColumn();
            ImGui.Text( r->RefCount.ToString() );
        } );
    }

    private unsafe void DrawCategoryContainer( ResourceCategory category,
        StdMap< uint, Pointer< StdMap< uint, Pointer< ResourceHandle > > > >* map )
    {
        if( map == null || !ImGui.TreeNodeEx( $"({( uint )category:D2}) {category} - {map->Count}###{( uint )category}Debug" ) )
        {
            return;
        }

        using var raii = ImGuiRaii.DeferredEnd( ImGui.TreePop );
        ResourceLoader.IterateExtMap( map, ( ext, map ) => DrawResourceMap( category, ext, map ) );
    }


    private static unsafe void DrawResourceProblems()
    {
        if( !ImGui.CollapsingHeader( "Resource Problems##ResourceManager" )
        || !ImGui.BeginTable( "##ProblemsTable", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit ) )
        {
            return;
        }

        using var end = ImGuiRaii.DeferredEnd( ImGui.EndTable );

        ResourceLoader.IterateResources( ( _, r ) =>
        {
            if( r->RefCount < 10000 )
            {
                return;
            }

            ImGui.TableNextColumn();
            ImGui.Text( r->Category.ToString() );
            ImGui.TableNextColumn();
            ImGui.Text( r->FileType.ToString( "X" ) );
            ImGui.TableNextColumn();
            ImGui.Text( r->Id.ToString( "X" ) );
            ImGui.TableNextColumn();
            ImGui.Text( ( ( ulong )r ).ToString( "X" ) );
            ImGui.TableNextColumn();
            ImGui.Text( r->RefCount.ToString() );
            ImGui.TableNextColumn();
            ref var name = ref r->FileName;
            if( name.Capacity > 15 )
            {
                ImGuiNative.igTextUnformatted( name.BufferPtr, name.BufferPtr + name.Length );
            }
            else
            {
                fixed( byte* ptr = name.Buffer )
                {
                    ImGuiNative.igTextUnformatted( ptr, ptr + name.Length );
                }
            }
        } );
    }

    private string _filter = string.Empty;

    private unsafe void DrawResourceManagerTab()
    {
        if( !ImGui.BeginTabItem( "Resource Manager Tab" ) )
        {
            return;
        }

        using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

        var resourceHandler = *ResourceLoader.ResourceManager;

        if( resourceHandler == null )
        {
            return;
        }

        ImGui.InputTextWithHint( "##resourceFilter", "Filter...", ref _filter, Utf8GamePath.MaxGamePathLength );

        raii.Push( ImGui.EndChild );
        if( !ImGui.BeginChild( "##ResourceManagerChild", -Vector2.One, true ) )
        {
            return;
        }

        ResourceLoader.IterateGraphs( DrawCategoryContainer );
    }
}