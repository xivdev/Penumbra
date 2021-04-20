using System.IO;
using Dalamud.Interface;
using ImGuiNET;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabEffective
        {
            private const string LabelTab        = "Effective File List";
            private const float  TextSizePadding = 5f;

            private ModManager _mods
                => Service< ModManager >.Get();

            private static void DrawFileLine( FileInfo file, GamePath path )
            {
                ImGui.TableNextColumn();
                ImGuiCustom.CopyOnClickSelectable( path );

                ImGui.TableNextColumn();
                ImGui.PushFont( UiBuilder.IconFont );
                ImGui.TextUnformatted( $"{( char )FontAwesomeIcon.LongArrowAltLeft}" );
                ImGui.PopFont();

                ImGui.TableNextColumn();
                ImGuiCustom.CopyOnClickSelectable( file.FullName );
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                {
                    return;
                }

                const ImGuiTableFlags flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollX;

                if( ImGui.BeginTable( "##effective_files", 3, flags, AutoFillSize ) )
                {
                    foreach ( var file in _mods.ResolvedFiles )
                    {
                        DrawFileLine( file.Value, file.Key );
                        ImGui.TableNextRow();
                    }

                    ImGui.EndTable();
                }

                ImGui.EndTabItem();
            }
        }
    }
}