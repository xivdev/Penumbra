using System.IO;
using Dalamud.Interface;
using ImGuiNET;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabEffective
        {
            private const           string     LabelTab      = "Effective Changes";
            private static readonly string     LongArrowLeft = $"{( char )FontAwesomeIcon.LongArrowAltLeft}";
            private readonly        ModManager _modManager;

            public TabEffective()
                => _modManager = Service< ModManager >.Get();


            private static void DrawFileLine( FileInfo file, GamePath path )
            {
                ImGui.TableNextColumn();
                Custom.ImGuiCustom.CopyOnClickSelectable( path );

                ImGui.TableNextColumn();
                ImGui.PushFont( UiBuilder.IconFont );
                ImGui.TextUnformatted( LongArrowLeft );
                ImGui.PopFont();

                ImGui.TableNextColumn();
                Custom.ImGuiCustom.CopyOnClickSelectable( file.FullName );
            }

            private static void DrawManipulationLine( MetaManipulation manip, Mod.Mod mod )
            {
                ImGui.TableNextColumn();
                ImGui.Selectable( manip.IdentifierString() );

                ImGui.TableNextColumn();
                ImGui.PushFont( UiBuilder.IconFont );
                ImGui.TextUnformatted( LongArrowLeft );
                ImGui.PopFont();

                ImGui.TableNextColumn();
                ImGui.Selectable( mod.Data.Meta.Name );
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                {
                    return;
                }

                const ImGuiTableFlags flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollX;

                if( ImGui.BeginTable( "##effective_changes", 3, flags, AutoFillSize ) )
                {
                    var currentCollection = _modManager.CurrentCollection.Cache!;
                    foreach( var file in currentCollection.ResolvedFiles )
                    {
                        DrawFileLine( file.Value, file.Key );
                        ImGui.TableNextRow();
                    }

                    foreach( var (manip, mod) in currentCollection.MetaManipulations.Manipulations )
                    {
                        DrawManipulationLine( manip, mod );
                        ImGui.TableNextRow();
                    }

                    ImGui.EndTable();
                }

                ImGui.EndTabItem();
            }
        }
    }
}