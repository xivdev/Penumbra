
using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Interop;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private class OnScreenTab : ITab
    {
        public ReadOnlySpan<byte> Label
            => "On-Screen"u8;

        public void DrawContent()
        {
            _unfolded ??= new();

            if( ImGui.Button( "Refresh Character List" ) )
            {
                try
                {
                    _trees = ResourceTree.FromObjectTable();
                }
                catch( Exception e )
                {
                    Penumbra.Log.Error( $"Could not get character list for On-Screen tab:\n{e}" );
                    _trees = Array.Empty<ResourceTree>();
                }
                _unfolded.Clear();
            }

            try
            {
                _trees ??= ResourceTree.FromObjectTable();
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not get character list for On-Screen tab:\n{e}" );
                _trees ??= Array.Empty<ResourceTree>();
            }

            var textColorNonPlayer = ImGui.GetColorU32( ImGuiCol.Text );
            var textColorPlayer    = ( textColorNonPlayer & 0xFF000000u ) | ( ( textColorNonPlayer & 0x00FEFEFE ) >> 1 ) | 0x8000u; // Half green

            foreach( var (tree, index) in _trees.WithIndex() )
            {
                using( var c = ImRaii.PushColor( ImGuiCol.Text, tree.PlayerRelated ? textColorPlayer : textColorNonPlayer ) )
                {
                    if( !ImGui.CollapsingHeader( $"{tree.Name}##{index}", ( index == 0 ) ? ImGuiTreeNodeFlags.DefaultOpen : 0 ) )
                    {
                        continue;
                    }
                }
                using var id = ImRaii.PushId( index );

                ImGui.Text( $"Collection: {tree.CollectionName}" );

                using var table = ImRaii.Table( "##ResourceTree", 3,
                    ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg );
                if( !table )
                {
                    continue;
                }

                ImGui.TableSetupColumn( string.Empty, ImGuiTableColumnFlags.WidthStretch, 0.2f );
                ImGui.TableSetupColumn( "Game Path", ImGuiTableColumnFlags.WidthStretch, 0.3f );
                ImGui.TableSetupColumn( "Actual Path", ImGuiTableColumnFlags.WidthStretch, 0.5f );
                ImGui.TableHeadersRow();

                DrawNodes( tree.Nodes, 0 );
            }
        }

        private void DrawNodes( IEnumerable<ResourceTree.Node> resourceNodes, int level )
        {
            var debugMode = Penumbra.Config.DebugMode;
            var frameHeight = ImGui.GetFrameHeight();
            foreach( var (resourceNode, index) in resourceNodes.WithIndex() )
            {
                if( resourceNode.Internal && !debugMode )
                {
                    continue;
                }
                using var id = ImRaii.PushId( index );
                ImGui.TableNextColumn();
                var unfolded = _unfolded!.Contains( resourceNode );
                using( var indent = ImRaii.PushIndent( level ) )
                {
                    ImGui.TableHeader( ( ( resourceNode.Children.Count > 0 ) ? ( unfolded ? "[-] " : "[+] " ) : string.Empty ) + resourceNode.Name );
                    if( ImGui.IsItemClicked() && resourceNode.Children.Count > 0 )
                    {
                        if( unfolded )
                        {
                            _unfolded.Remove( resourceNode );
                        }
                        else
                        {
                            _unfolded.Add( resourceNode );
                        }
                        unfolded = !unfolded;
                    }
                    if( debugMode )
                    {
                        ImGuiUtil.HoverTooltip( $"Resource Type: {resourceNode.Type}\nSource Address: 0x{resourceNode.SourceAddress.ToString( "X" + nint.Size * 2 )}" );
                    }
                }
                ImGui.TableNextColumn();
                var hasGamePaths = resourceNode.PossibleGamePaths.Length > 0;
                ImGui.Selectable( resourceNode.PossibleGamePaths.Length switch
                {
                    0 => "(none)",
                    1 => resourceNode.GamePath.ToString(),
                    _ => "(multiple)",
                }, false, hasGamePaths ? 0 : ImGuiSelectableFlags.Disabled, new Vector2( ImGui.GetContentRegionAvail().X, frameHeight ) );
                if( hasGamePaths )
                {
                    var allPaths = string.Join( '\n', resourceNode.PossibleGamePaths );
                    if( ImGui.IsItemClicked() )
                    {
                        ImGui.SetClipboardText( allPaths );
                    }
                    ImGuiUtil.HoverTooltip( $"{allPaths}\n\nClick to copy to clipboard." );
                }
                ImGui.TableNextColumn();
                var hasFullPath = resourceNode.FullPath.FullName.Length > 0;
                if( hasFullPath )
                {
                    ImGui.Selectable( resourceNode.FullPath.ToString(), false, 0, new Vector2( ImGui.GetContentRegionAvail().X, frameHeight ) );
                    if( ImGui.IsItemClicked() )
                    {
                        ImGui.SetClipboardText( resourceNode.FullPath.ToString() );
                    }
                    ImGuiUtil.HoverTooltip( $"{resourceNode.FullPath}\n\nClick to copy to clipboard." );
                }
                else
                {
                    ImGui.Selectable( "(unavailable)", false, ImGuiSelectableFlags.Disabled, new Vector2( ImGui.GetContentRegionAvail().X, frameHeight ) );
                    ImGuiUtil.HoverTooltip( "The actual path to this file is unavailable.\nIt may be managed by another plug-in." );
                }
                if( unfolded )
                {
                    DrawNodes( resourceNode.Children, level + 1 );
                }
            }
        }

        private ResourceTree[]? _trees;
        private HashSet<ResourceTree.Node>? _unfolded;
    }
}
