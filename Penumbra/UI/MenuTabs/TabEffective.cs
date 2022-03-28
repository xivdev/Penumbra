using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface;
using ImGuiNET;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Util;
using Penumbra.UI.Custom;
using Penumbra.Util;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private class TabEffective
    {
        private const string LabelTab = "Effective Changes";

        private LowerString _gamePathFilter = LowerString.Empty;
        private LowerString _filePathFilter = LowerString.Empty;

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
            var tmp = _gamePathFilter.Text;
            if( ImGui.InputTextWithHint( "##effective_changes_gfilter", "Filter game path...", ref tmp, 256 ) )
            {
                _gamePathFilter = tmp;
            }

            ImGui.SameLine( ( LeftTextLength + _arrowLength ) * ImGuiHelpers.GlobalScale + 3 * ImGui.GetStyle().ItemSpacing.X );
            ImGui.SetNextItemWidth( -1 );
            tmp = _filePathFilter.Text;
            if( ImGui.InputTextWithHint( "##effective_changes_ffilter", "Filter file path...", ref tmp, 256 ) )
            {
                _filePathFilter = tmp;
            }
        }

        private bool CheckFilters( KeyValuePair< Utf8GamePath, FullPath > kvp )
        {
            if( _gamePathFilter.Length > 0 && !kvp.Key.ToString().Contains( _gamePathFilter.Lower ) )
            {
                return false;
            }

            return _filePathFilter.Length == 0 || kvp.Value.FullName.ToLowerInvariant().Contains( _filePathFilter.Lower );
        }

        private bool CheckFilters( KeyValuePair< Utf8GamePath, Utf8GamePath > kvp )
        {
            if( _gamePathFilter.Length > 0 && !kvp.Key.ToString().Contains( _gamePathFilter.Lower ) )
            {
                return false;
            }

            return _filePathFilter.Length == 0 || kvp.Value.ToString().Contains( _filePathFilter.Lower );
        }

        private bool CheckFilters( (string, LowerString) kvp )
        {
            if( _gamePathFilter.Length > 0 && !kvp.Item1.ToLowerInvariant().Contains( _gamePathFilter.Lower ) )
            {
                return false;
            }

            return _filePathFilter.Length == 0 || kvp.Item2.Contains( _filePathFilter.Lower );
        }

        private void DrawFilteredRows( ModCollection active )
        {
            foreach( var (gp, fp) in active.ResolvedFiles.Where( CheckFilters ) )
            {
                DrawLine( gp, fp );
            }

            var cache = active.MetaCache;
            if( cache == null )
            {
                return;
            }

            foreach( var (mp, mod) in cache.Cmp.Manipulations
                       .Select( p => ( p.Key.ToString(), Penumbra.ModManager.Mods[ p.Value ].Meta.Name ) )
                       .Where( CheckFilters ) )
            {
                DrawLine( mp, mod );
            }

            foreach( var (mp, mod) in cache.Eqp.Manipulations
                       .Select( p => ( p.Key.ToString(), Penumbra.ModManager.Mods[ p.Value ].Meta.Name ) )
                       .Where( CheckFilters ) )
            {
                DrawLine( mp, mod );
            }

            foreach( var (mp, mod) in cache.Eqdp.Manipulations
                       .Select( p => ( p.Key.ToString(), Penumbra.ModManager.Mods[ p.Value ].Meta.Name ) )
                       .Where( CheckFilters ) )
            {
                DrawLine( mp, mod );
            }

            foreach( var (mp, mod) in cache.Gmp.Manipulations
                       .Select( p => ( p.Key.ToString(), Penumbra.ModManager.Mods[ p.Value ].Meta.Name ) )
                       .Where( CheckFilters ) )
            {
                DrawLine( mp, mod );
            }

            foreach( var (mp, mod) in cache.Est.Manipulations
                       .Select( p => ( p.Key.ToString(), Penumbra.ModManager.Mods[ p.Value ].Meta.Name ) )
                       .Where( CheckFilters ) )
            {
                DrawLine( mp, mod );
            }

            foreach( var (mp, mod) in cache.Imc.Manipulations
                       .Select( p => ( p.Key.ToString(), Penumbra.ModManager.Mods[ p.Value ].Meta.Name ) )
                       .Where( CheckFilters ) )
            {
                DrawLine( mp, mod );
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

            var resolved      = Penumbra.CollectionManager.Default.ResolvedFiles;
            var meta          = Penumbra.CollectionManager.Default.MetaCache;
            var metaCount     = meta?.Count ?? 0;
            var resolvedCount = resolved.Count;

            var totalLines = resolvedCount + metaCount;
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
                DrawFilteredRows( Penumbra.CollectionManager.Default );
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
                        if( row < resolvedCount )
                        {
                            var (gamePath, file) = resolved.ElementAt( row );
                            DrawLine( gamePath, file );
                        }
                        else if( ( row -= resolved.Count ) < metaCount )
                        {
                            // TODO
                            //var (manip, mod) = activeCollection!.MetaManipulations.Manipulations.ElementAt( row );
                            DrawLine( 0.ToString(), 0.ToString() );
                        }
                    }
                }
            }
        }
    }
}