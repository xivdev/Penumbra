using System.IO;
using System.Linq;
using Dalamud.Interface;
using ImGuiNET;
using Penumbra.GameData.Util;
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

            private readonly float _leftTextLength =
                ImGui.CalcTextSize( "chara/human/c0000/obj/body/b0000/material/v0000/mt_c0000b0000_b.mtrl" ).X + 40;

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
                ImGui.SameLine();
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
                ImGui.SameLine();
                ImGui.Selectable( mod.Data.Meta.Name );
            }

            public void Draw()
            {
                if( !ImGui.BeginTabItem( LabelTab ) )
                {
                    return;
                }

                const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollX;

                var activeCollection    = _modManager.Collections.ActiveCollection.Cache;
                var forcedCollection    = _modManager.Collections.ForcedCollection.Cache;

                var (activeResolved, activeMeta) = activeCollection != null
                    ? ( activeCollection.ResolvedFiles.Count, activeCollection.MetaManipulations.Count )
                    : ( 0, 0 );
                var (forcedResolved, forcedMeta) = forcedCollection != null
                    ? (forcedCollection.ResolvedFiles.Count, forcedCollection.MetaManipulations.Count)
                    : (0, 0);

                var                 lines = activeResolved + forcedResolved + activeMeta + forcedMeta;
                ImGuiListClipperPtr clipper;
                unsafe
                {
                    clipper = new ImGuiListClipperPtr( ImGuiNative.ImGuiListClipper_ImGuiListClipper() );
                }

                clipper.Begin( lines );

                if( ImGui.BeginTable( "##effective_changes", 2, flags, AutoFillSize ) )
                {
                    ImGui.TableSetupColumn( "##tableGamePathCol", ImGuiTableColumnFlags.None, _leftTextLength );
                    while( clipper.Step() )
                    {
                        for( var actualRow = clipper.DisplayStart; actualRow < clipper.DisplayEnd; actualRow++ )
                        {
                            var row = actualRow;
                            ImGui.TableNextRow();
                            if( row < activeResolved )
                            {
                                var file = activeCollection!.ResolvedFiles.ElementAt( row );
                                DrawFileLine( file.Value, file.Key );
                            }
                            else if( ( row -= activeResolved ) < forcedResolved )
                            {
                                var file = forcedCollection!.ResolvedFiles.ElementAt( row );
                                DrawFileLine( file.Value, file.Key );
                            }
                            else if( ( row -= forcedResolved ) < activeMeta )
                            {
                                var manip = activeCollection!.MetaManipulations.Manipulations.ElementAt( row );
                                DrawManipulationLine( manip.Item1, manip.Item2 );
                            }
                            else
                            {
                                row -= activeMeta;
                                var manip = forcedCollection!.MetaManipulations.Manipulations.ElementAt( row );
                                DrawManipulationLine( manip.Item1, manip.Item2 );
                            }
                        }
                    }

                    ImGui.EndTable();
                }

                ImGui.EndTabItem();
            }
        }
    }
}