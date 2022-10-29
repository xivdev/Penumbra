using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.String.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private class EffectiveTab
    {
        // Draw the effective tab if ShowAdvanced is on.
        public void Draw()
        {
            using var tab = ImRaii.TabItem( "Effective Changes" );
            if( !tab )
            {
                return;
            }

            SetupEffectiveSizes();
            DrawFilters();
            using var child = ImRaii.Child( "##EffectiveChangesTab", -Vector2.One, false );
            if( !child )
            {
                return;
            }

            var       height = ImGui.GetTextLineHeightWithSpacing() + 2 * ImGui.GetStyle().CellPadding.Y;
            var       skips  = ImGuiClip.GetNecessarySkips( height );
            using var table  = ImRaii.Table( "##EffectiveChangesTable", 3, ImGuiTableFlags.RowBg );
            if( !table )
            {
                return;
            }

            ImGui.TableSetupColumn( "##gamePath", ImGuiTableColumnFlags.WidthFixed, _effectiveLeftTextLength );
            ImGui.TableSetupColumn( string.Empty, ImGuiTableColumnFlags.WidthFixed, _effectiveArrowLength );
            ImGui.TableSetupColumn( "##file", ImGuiTableColumnFlags.WidthFixed, _effectiveRightTextLength );

            DrawEffectiveRows( Penumbra.CollectionManager.Current, skips, height,
                _effectiveFilePathFilter.Length > 0 || _effectiveGamePathFilter.Length > 0 );
        }

        // Sizes
        private float _effectiveLeftTextLength;
        private float _effectiveRightTextLength;
        private float _effectiveUnscaledArrowLength;
        private float _effectiveArrowLength;

        // Filters
        private LowerString _effectiveGamePathFilter = LowerString.Empty;
        private LowerString _effectiveFilePathFilter = LowerString.Empty;

        // Setup table sizes.
        private void SetupEffectiveSizes()
        {
            if( _effectiveUnscaledArrowLength == 0 )
            {
                using var font = ImRaii.PushFont( UiBuilder.IconFont );
                _effectiveUnscaledArrowLength =
                    ImGui.CalcTextSize( FontAwesomeIcon.LongArrowAltLeft.ToIconString() ).X / ImGuiHelpers.GlobalScale;
            }

            _effectiveArrowLength     = _effectiveUnscaledArrowLength * ImGuiHelpers.GlobalScale;
            _effectiveLeftTextLength  = 450                           * ImGuiHelpers.GlobalScale;
            _effectiveRightTextLength = ImGui.GetWindowSize().X - _effectiveArrowLength - _effectiveLeftTextLength;
        }

        // Draw the header line for filters
        private void DrawFilters()
        {
            var tmp = _effectiveGamePathFilter.Text;
            ImGui.SetNextItemWidth( _effectiveLeftTextLength );
            if( ImGui.InputTextWithHint( "##gamePathFilter", "Filter game path...", ref tmp, 256 ) )
            {
                _effectiveGamePathFilter = tmp;
            }

            ImGui.SameLine( _effectiveArrowLength + _effectiveLeftTextLength + 3 * ImGui.GetStyle().ItemSpacing.X );
            ImGui.SetNextItemWidth( -1 );
            tmp = _effectiveFilePathFilter.Text;
            if( ImGui.InputTextWithHint( "##fileFilter", "Filter file path...", ref tmp, 256 ) )
            {
                _effectiveFilePathFilter = tmp;
            }
        }

        // Draw all rows respecting filters and using clipping.
        private void DrawEffectiveRows( ModCollection active, int skips, float height, bool hasFilters )
        {
            // We can use the known counts if no filters are active.
            var stop = hasFilters
                ? ImGuiClip.FilteredClippedDraw( active.ResolvedFiles, skips, CheckFilters, DrawLine )
                : ImGuiClip.ClippedDraw( active.ResolvedFiles, skips, DrawLine, active.ResolvedFiles.Count );

            var m = active.MetaCache;
            // If no meta manipulations are active, we can just draw the end dummy.
            if( m is { Count: > 0 } )
            {
                // Filters mean we can not use the known counts.
                if( hasFilters )
                {
                    var it2 = m.Select( p => ( p.Key.ToString(), p.Value.Name ) );
                    if( stop >= 0 )
                    {
                        ImGuiClip.DrawEndDummy( stop + it2.Count( CheckFilters ), height );
                    }
                    else
                    {
                        stop = ImGuiClip.FilteredClippedDraw( it2, skips, CheckFilters, DrawLine, ~stop );
                        ImGuiClip.DrawEndDummy( stop, height );
                    }
                }
                else
                {
                    if( stop >= 0 )
                    {
                        ImGuiClip.DrawEndDummy( stop + m.Count, height );
                    }
                    else
                    {
                        stop = ImGuiClip.ClippedDraw( m, skips, DrawLine, m.Count, ~stop );
                        ImGuiClip.DrawEndDummy( stop, height );
                    }
                }
            }
            else
            {
                ImGuiClip.DrawEndDummy( stop, height );
            }
        }

        // Draw a line for a game path and its redirected file.
        private static void DrawLine( KeyValuePair< Utf8GamePath, ModPath > pair )
        {
            var (path, name) = pair;
            ImGui.TableNextColumn();
            CopyOnClickSelectable( path.Path );

            ImGui.TableNextColumn();
            ImGuiUtil.PrintIcon( FontAwesomeIcon.LongArrowAltLeft );
            ImGui.TableNextColumn();
            CopyOnClickSelectable( name.Path.InternalName );
            ImGuiUtil.HoverTooltip( $"\nChanged by {name.Mod.Name}." );
        }

        // Draw a line for a path and its name.
        private static void DrawLine( (string, LowerString) pair )
        {
            var (path, name) = pair;
            ImGui.TableNextColumn();
            ImGuiUtil.CopyOnClickSelectable( path );

            ImGui.TableNextColumn();
            ImGuiUtil.PrintIcon( FontAwesomeIcon.LongArrowAltLeft );
            ImGui.TableNextColumn();
            ImGuiUtil.CopyOnClickSelectable( name );
        }

        // Draw a line for a unfiltered/unconverted manipulation and mod-index pair.
        private static void DrawLine( KeyValuePair< MetaManipulation, IMod > pair )
        {
            var (manipulation, mod) = pair;
            ImGui.TableNextColumn();
            ImGuiUtil.CopyOnClickSelectable( manipulation.ToString() ?? string.Empty );

            ImGui.TableNextColumn();
            ImGuiUtil.PrintIcon( FontAwesomeIcon.LongArrowAltLeft );
            ImGui.TableNextColumn();
            ImGuiUtil.CopyOnClickSelectable( mod.Name );
        }

        // Check filters for file replacements.
        private bool CheckFilters( KeyValuePair< Utf8GamePath, ModPath > kvp )
        {
            var (gamePath, fullPath) = kvp;
            if( _effectiveGamePathFilter.Length > 0 && !gamePath.ToString().Contains( _effectiveGamePathFilter.Lower ) )
            {
                return false;
            }

            return _effectiveFilePathFilter.Length == 0 || fullPath.Path.FullName.ToLowerInvariant().Contains( _effectiveFilePathFilter.Lower );
        }

        // Check filters for meta manipulations.
        private bool CheckFilters( (string, LowerString) kvp )
        {
            var (name, path) = kvp;
            if( _effectiveGamePathFilter.Length > 0 && !name.ToLowerInvariant().Contains( _effectiveGamePathFilter.Lower ) )
            {
                return false;
            }

            return _effectiveFilePathFilter.Length == 0 || path.Contains( _effectiveFilePathFilter.Lower );
        }
    }
}