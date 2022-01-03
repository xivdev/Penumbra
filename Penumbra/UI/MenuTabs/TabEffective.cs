using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface;
using ImGuiNET;
using Penumbra.GameData.Util;
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

            private string _gamePathFilter      = string.Empty;
            private string _gamePathFilterLower = string.Empty;
            private string _filePathFilter      = string.Empty;
            private string _filePathFilterLower = string.Empty;

            private readonly float _leftTextLength =
                ImGui.CalcTextSize( "chara/human/c0000/obj/body/b0000/material/v0000/mt_c0000b0000_b.mtrl" ).X / ImGuiHelpers.GlobalScale + 40;

            private float _arrowLength = 0;

            public TabEffective()
                => _modManager = Service< ModManager >.Get();


            private static void DrawLine( string path, string name )
            {
                ImGui.TableNextColumn();
                ImGuiCustom.CopyOnClickSelectable( path );

                ImGui.TableNextColumn();
                ImGuiCustom.PrintIcon( FontAwesomeIcon.LongArrowAltLeft );
                ImGui.SameLine();
                ImGuiCustom.CopyOnClickSelectable( name );
            }

            private void DrawFilters()
            {
                if( _arrowLength == 0 )
                {
                    using var font = ImGuiRaii.PushFont( UiBuilder.IconFont );
                    _arrowLength = ImGui.CalcTextSize( FontAwesomeIcon.LongArrowAltLeft.ToIconString() ).X / ImGuiHelpers.GlobalScale;
                }

                ImGui.SetNextItemWidth( _leftTextLength * ImGuiHelpers.GlobalScale );
                if( ImGui.InputTextWithHint( "##effective_changes_gfilter", "Filter game path...", ref _gamePathFilter, 256 ) )
                {
                    _gamePathFilterLower = _gamePathFilter.ToLowerInvariant();
                }

                ImGui.SameLine( ( _leftTextLength + _arrowLength ) * ImGuiHelpers.GlobalScale + 3 * ImGui.GetStyle().ItemSpacing.X );
                ImGui.SetNextItemWidth( -1 );
                if( ImGui.InputTextWithHint( "##effective_changes_ffilter", "Filter file path...", ref _filePathFilter, 256 ) )
                {
                    _filePathFilterLower = _filePathFilter.ToLowerInvariant();
                }
            }

            private bool CheckFilters( KeyValuePair< GamePath, FullPath > kvp )
            {
                if( _gamePathFilter.Any() && !kvp.Key.ToString().Contains( _gamePathFilterLower ) )
                {
                    return false;
                }

                return !_filePathFilter.Any() || kvp.Value.FullName.ToLowerInvariant().Contains( _filePathFilterLower );
            }

            private bool CheckFilters( KeyValuePair< GamePath, GamePath > kvp )
            {
                if( _gamePathFilter.Any() && !kvp.Key.ToString().Contains( _gamePathFilterLower ) )
                {
                    return false;
                }

                return !_filePathFilter.Any() || kvp.Value.ToString().Contains( _filePathFilterLower );
            }

            private bool CheckFilters( (string, string, string) kvp )
            {
                if( _gamePathFilter.Any() && !kvp.Item1.ToLowerInvariant().Contains( _gamePathFilterLower ) )
                {
                    return false;
                }

                return !_filePathFilter.Any() || kvp.Item3.Contains( _filePathFilterLower );
            }

            private void DrawFilteredRows( ModCollectionCache? active, ModCollectionCache? forced )
            {
                void DrawFileLines( ModCollectionCache cache )
                {
                    foreach( var (gp, fp) in cache.ResolvedFiles.Where( CheckFilters ) )
                    {
                        DrawLine( gp, fp.FullName );
                    }

                    foreach( var (gp, fp) in cache.SwappedFiles.Where( CheckFilters ) )
                    {
                        DrawLine( gp, fp );
                    }

                    foreach( var (mp, mod, _) in cache.MetaManipulations.Manipulations
                       .Select( p => ( p.Item1.IdentifierString(), p.Item2.Data.Meta.Name, p.Item2.Data.Meta.LowerName ) )
                       .Where( CheckFilters ) )
                    {
                        DrawLine( mp, mod );
                    }
                }

                if( active != null )
                {
                    DrawFileLines( active );
                }

                if( forced != null )
                {
                    DrawFileLines( forced );
                }
            }

            public void Draw()
            {
                if( !ImGui.BeginTabItem( LabelTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                DrawFilters();

                const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollX;

                var activeCollection = _modManager.Collections.ActiveCollection.Cache;
                var forcedCollection = _modManager.Collections.ForcedCollection.Cache;

                var (activeResolved, activeSwap, activeMeta) = activeCollection != null
                    ? ( activeCollection.ResolvedFiles.Count, activeCollection.SwappedFiles.Count, activeCollection.MetaManipulations.Count )
                    : ( 0, 0, 0 );
                var (forcedResolved, forcedSwap, forcedMeta) = forcedCollection != null
                    ? ( forcedCollection.ResolvedFiles.Count, forcedCollection.SwappedFiles.Count, forcedCollection.MetaManipulations.Count )
                    : ( 0, 0, 0 );
                var totalLines = activeResolved + forcedResolved + activeSwap + forcedSwap + activeMeta + forcedMeta;
                if( totalLines == 0 )
                {
                    return;
                }

                if( ImGui.BeginTable( "##effective_changes", 2, flags, AutoFillSize ) )
                {
                    raii.Push( ImGui.EndTable );
                    ImGui.TableSetupColumn( "##tableGamePathCol", ImGuiTableColumnFlags.None, _leftTextLength * ImGuiHelpers.GlobalScale );

                    if( _filePathFilter.Any() || _gamePathFilter.Any() )
                    {
                        DrawFilteredRows( activeCollection, forcedCollection );
                    }
                    else
                    {
                        ImGuiListClipperPtr clipper;
                        unsafe
                        {
                            clipper = new ImGuiListClipperPtr( ImGuiNative.ImGuiListClipper_ImGuiListClipper() );
                        }

                        clipper.Begin( totalLines );


                        while( clipper.Step() )
                        {
                            for( var actualRow = clipper.DisplayStart; actualRow < clipper.DisplayEnd; actualRow++ )
                            {
                                var row = actualRow;
                                ImGui.TableNextRow();
                                if( row < activeResolved )
                                {
                                    var (gamePath, file) = activeCollection!.ResolvedFiles.ElementAt( row );
                                    DrawLine( gamePath, file.FullName );
                                }
                                else if( ( row -= activeResolved ) < activeSwap )
                                {
                                    var (gamePath, swap) = activeCollection!.SwappedFiles.ElementAt( row );
                                    DrawLine( gamePath, swap );
                                }
                                else if( ( row -= activeSwap ) < activeMeta )
                                {
                                    var (manip, mod) = activeCollection!.MetaManipulations.Manipulations.ElementAt( row );
                                    DrawLine( manip.IdentifierString(), mod.Data.Meta.Name );
                                }
                                else if( ( row -= activeMeta ) < forcedResolved )
                                {
                                    var (gamePath, file) = forcedCollection!.ResolvedFiles.ElementAt( row );
                                    DrawLine( gamePath, file.FullName );
                                }
                                else if( ( row -= forcedResolved ) < forcedSwap )
                                {
                                    var (gamePath, swap) = forcedCollection!.SwappedFiles.ElementAt( row );
                                    DrawLine( gamePath, swap );
                                }
                                else
                                {
                                    row              -= forcedSwap;
                                    var (manip, mod) =  forcedCollection!.MetaManipulations.Manipulations.ElementAt( row );
                                    DrawLine( manip.IdentifierString(), mod.Data.Meta.Name );
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

