using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface;
using ImGuiNET;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Util;
using Penumbra.Mods;
using Penumbra.UI.Custom;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private class TabEffective
    {
        private const    string     LabelTab = "Effective Changes";

        private string _gamePathFilter      = string.Empty;
        private string _gamePathFilterLower = string.Empty;
        private string _filePathFilter      = string.Empty;
        private string _filePathFilterLower = string.Empty;

        private const float LeftTextLength = 600;

        private float _arrowLength = 0;

        private static void DrawLine( Utf8GamePath path, FullPath name )
        {
            ImGui.TableNextColumn();
            ImGuiCustom.CopyOnClickSelectable( path.Path );

            ImGui.TableNextColumn();
            ImGuiCustom.PrintIcon( FontAwesomeIcon.LongArrowAltLeft );
            ImGui.SameLine();
            ImGuiCustom.CopyOnClickSelectable( name.InternalName );
        }

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

            ImGui.SetNextItemWidth( LeftTextLength * ImGuiHelpers.GlobalScale );
            if( ImGui.InputTextWithHint( "##effective_changes_gfilter", "Filter game path...", ref _gamePathFilter, 256 ) )
            {
                _gamePathFilterLower = _gamePathFilter.ToLowerInvariant();
            }

            ImGui.SameLine( ( LeftTextLength + _arrowLength ) * ImGuiHelpers.GlobalScale + 3 * ImGui.GetStyle().ItemSpacing.X );
            ImGui.SetNextItemWidth( -1 );
            if( ImGui.InputTextWithHint( "##effective_changes_ffilter", "Filter file path...", ref _filePathFilter, 256 ) )
            {
                _filePathFilterLower = _filePathFilter.ToLowerInvariant();
            }
        }

        private bool CheckFilters( KeyValuePair< Utf8GamePath, FullPath > kvp )
        {
            if( _gamePathFilter.Any() && !kvp.Key.ToString().Contains( _gamePathFilterLower ) )
            {
                return false;
            }

            return !_filePathFilter.Any() || kvp.Value.FullName.ToLowerInvariant().Contains( _filePathFilterLower );
        }

        private bool CheckFilters( KeyValuePair< Utf8GamePath, Utf8GamePath > kvp )
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
                    DrawLine( gp, fp );
                }

                //foreach( var (mp, mod, _) in cache.MetaManipulations.Manipulations
                //           .Select( p => ( p.Item1.IdentifierString(), p.Item2.Data.Meta.Name, p.Item2.Data.Meta.LowerName ) )
                //           .Where( CheckFilters ) )
                //{
                //    DrawLine( mp, mod );
                //}
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

            var modManager       = Penumbra.ModManager;
            var activeCollection = modManager.Collections.ActiveCollection.Cache;
            var forcedCollection = modManager.Collections.ForcedCollection.Cache;

            var (activeResolved, activeMeta) = activeCollection != null
                ? ( activeCollection.ResolvedFiles.Count, activeCollection.MetaManipulations.Count )
                : ( 0, 0 );
            var (forcedResolved, forcedMeta) = forcedCollection != null
                ? ( forcedCollection.ResolvedFiles.Count, forcedCollection.MetaManipulations.Count )
                : ( 0, 0 );
            var totalLines = activeResolved + forcedResolved + activeMeta + forcedMeta;
            if( totalLines == 0 )
            {
                return;
            }

            if( !ImGui.BeginTable( "##effective_changes", 2, flags, AutoFillSize ) )
            {
                return;
            }

            raii.Push( ImGui.EndTable );
            ImGui.TableSetupColumn( "##tableGamePathCol", ImGuiTableColumnFlags.None, LeftTextLength * ImGuiHelpers.GlobalScale );

            if( _filePathFilter.Length > 0 || _gamePathFilter.Length > 0 )
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
                            DrawLine( gamePath, file );
                        }
                        else if( ( row -= activeResolved ) < activeMeta )
                        {
                            // TODO
                            //var (manip, mod) = activeCollection!.MetaManipulations.Manipulations.ElementAt( row );
                            DrawLine( 0.ToString(), 0.ToString() );
                        }
                        else if( ( row -= activeMeta ) < forcedResolved )
                        {
                            var (gamePath, file) = forcedCollection!.ResolvedFiles.ElementAt( row );
                            DrawLine( gamePath, file );
                        }
                        else
                        {
                            // TODO
                            row              -= forcedResolved;
                            //var (manip, mod) =  forcedCollection!.MetaManipulations.Manipulations.ElementAt( row );
                            DrawLine( 0.ToString(), 0.ToString() );
                        }
                    }
                }
            }
        }
    }
}