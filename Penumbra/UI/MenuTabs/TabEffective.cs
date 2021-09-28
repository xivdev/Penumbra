using System.IO;
using System.Linq;
using Dalamud.Interface;
using ImGuiNET;
using Penumbra.GameData.Util;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.UI.Custom;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabEffective
        {
            private const    string     LabelTab = "Effective Changes";
            private readonly ModManager _modManager;

            private readonly float _leftTextLength =
                ImGui.CalcTextSize( "chara/human/c0000/obj/body/b0000/material/v0000/mt_c0000b0000_b.mtrl" ).X + 40;

            public TabEffective()
                => _modManager = Service< ModManager >.Get();


            private static void DrawFileLine( FileInfo file, GamePath path )
            {
                ImGui.TableNextColumn();
                ImGuiCustom.CopyOnClickSelectable( path );

                ImGui.TableNextColumn();
                ImGuiCustom.PrintIcon( FontAwesomeIcon.LongArrowAltLeft );
                ImGui.SameLine();
                ImGuiCustom.CopyOnClickSelectable( file.FullName );
            }

            private static void DrawManipulationLine( MetaManipulation manip, Mod.Mod mod )
            {
                ImGui.TableNextColumn();
                ImGui.Selectable( manip.IdentifierString() );

                ImGui.TableNextColumn();
                ImGuiCustom.PrintIcon( FontAwesomeIcon.LongArrowAltLeft );
                ImGui.SameLine();
                ImGui.Selectable( mod.Data.Meta.Name );
            }

            public void Draw()
            {
                if( !ImGui.BeginTabItem( LabelTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollX;

                var activeCollection = _modManager.Collections.ActiveCollection.Cache;
                var forcedCollection = _modManager.Collections.ForcedCollection.Cache;

                var (activeResolved, activeMeta) = activeCollection != null
                    ? ( activeCollection.ResolvedFiles.Count, activeCollection.MetaManipulations.Count )
                    : ( 0, 0 );
                var (forcedResolved, forcedMeta) = forcedCollection != null
                    ? ( forcedCollection.ResolvedFiles.Count, forcedCollection.MetaManipulations.Count )
                    : ( 0, 0 );

                var                 lines = activeResolved + forcedResolved + activeMeta + forcedMeta;
                ImGuiListClipperPtr clipper;
                unsafe
                {
                    clipper = new ImGuiListClipperPtr( ImGuiNative.ImGuiListClipper_ImGuiListClipper() );
                }

                clipper.Begin( lines );

                if( ImGui.BeginTable( "##effective_changes", 2, flags, AutoFillSize ) )
                {
                    raii.Push( ImGui.EndTable );
                    ImGui.TableSetupColumn( "##tableGamePathCol", ImGuiTableColumnFlags.None, _leftTextLength );
                    while( clipper.Step() )
                    {
                        for( var actualRow = clipper.DisplayStart; actualRow < clipper.DisplayEnd; actualRow++ )
                        {
                            var row = actualRow;
                            ImGui.TableNextRow();
                            if( row < activeResolved )
                            {
                                var (gamePath, file) = activeCollection!.ResolvedFiles.ElementAt( row );
                                DrawFileLine( file, gamePath );
                            }
                            else if( ( row -= activeResolved ) < forcedResolved )
                            {
                                var (gamePath, file) = forcedCollection!.ResolvedFiles.ElementAt( row );
                                DrawFileLine( file, gamePath );
                            }
                            else if( ( row -= forcedResolved ) < activeMeta )
                            {
                                var (manip, mod) = activeCollection!.MetaManipulations.Manipulations.ElementAt( row );
                                DrawManipulationLine( manip, mod );
                            }
                            else
                            {
                                row              -= activeMeta;
                                var (manip, mod) =  forcedCollection!.MetaManipulations.Manipulations.ElementAt( row );
                                DrawManipulationLine( manip, mod );
                            }
                        }
                    }
                }
            }
        }
    }
}