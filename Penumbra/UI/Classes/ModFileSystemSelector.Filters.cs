using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.Mods;

namespace Penumbra.UI.Classes;

public partial class ModFileSystemSelector
{
    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public struct ModState
    {
        public ColorId Color;
    }

    private const StringComparison IgnoreCase   = StringComparison.OrdinalIgnoreCase;
    private       LowerString      _modFilter   = LowerString.Empty;
    private       int              _filterType  = -1;
    private       ModFilter        _stateFilter = ModFilterExtensions.UnfilteredStateMods;

    private void SetFilterTooltip()
    {
        FilterTooltip = "Filter mods for those where their full paths or names contain the given substring.\n"
          + "Enter c:[string] to filter for mods changing specific items.\n"
          + "Enter t:[string] to filter for mods set to specific tags.\n"
          + "Enter n:[string] to filter only for mod names and no paths.\n"
          + "Enter a:[string] to filter for mods by specific authors.";
    }

    // Appropriately identify and set the string filter and its type.
    protected override bool ChangeFilter( string filterValue )
    {
        ( _modFilter, _filterType ) = filterValue.Length switch
        {
            0 => ( LowerString.Empty, -1 ),
            > 1 when filterValue[ 1 ] == ':' =>
                filterValue[ 0 ] switch
                {
                    'n' => filterValue.Length == 2 ? ( LowerString.Empty, -1 ) : ( new LowerString( filterValue[ 2.. ] ), 1 ),
                    'N' => filterValue.Length == 2 ? ( LowerString.Empty, -1 ) : ( new LowerString( filterValue[ 2.. ] ), 1 ),
                    'a' => filterValue.Length == 2 ? ( LowerString.Empty, -1 ) : ( new LowerString( filterValue[ 2.. ] ), 2 ),
                    'A' => filterValue.Length == 2 ? ( LowerString.Empty, -1 ) : ( new LowerString( filterValue[ 2.. ] ), 2 ),
                    'c' => filterValue.Length == 2 ? ( LowerString.Empty, -1 ) : ( new LowerString( filterValue[ 2.. ] ), 3 ),
                    'C' => filterValue.Length == 2 ? ( LowerString.Empty, -1 ) : ( new LowerString( filterValue[ 2.. ] ), 3 ),
                    't' => filterValue.Length == 2 ? ( LowerString.Empty, -1 ) : ( new LowerString( filterValue[ 2.. ] ), 4 ),
                    'T' => filterValue.Length == 2 ? ( LowerString.Empty, -1 ) : ( new LowerString( filterValue[ 2.. ] ), 4 ),
                    _   => ( new LowerString( filterValue ), 0 ),
                },
            _ => ( new LowerString( filterValue ), 0 ),
        };

        return true;
    }

    // Check the state filter for a specific pair of has/has-not flags.
    // Uses count == 0 to check for has-not and count != 0 for has.
    // Returns true if it should be filtered and false if not.
    private bool CheckFlags( int count, ModFilter hasNoFlag, ModFilter hasFlag )
    {
        return count switch
        {
            0 when _stateFilter.HasFlag( hasNoFlag ) => false,
            0                                        => true,
            _ when _stateFilter.HasFlag( hasFlag )   => false,
            _                                        => true,
        };
    }

    // The overwritten filter method also computes the state.
    // Folders have default state and are filtered out on the direct string instead of the other options.
    // If any filter is set, they should be hidden by default unless their children are visible,
    // or they contain the path search string.
    protected override bool ApplyFiltersAndState( FileSystem< Mod >.IPath path, out ModState state )
    {
        if( path is ModFileSystem.Folder f )
        {
            state = default;
            return ModFilterExtensions.UnfilteredStateMods != _stateFilter
             || FilterValue.Length > 0 && !f.FullName().Contains( FilterValue, IgnoreCase );
        }

        return ApplyFiltersAndState( ( ModFileSystem.Leaf )path, out state );
    }

    // Apply the string filters.
    private bool ApplyStringFilters( ModFileSystem.Leaf leaf, Mod mod )
    {
        return _filterType switch
        {
            -1 => false,
            0  => !( leaf.FullName().Contains( _modFilter.Lower, IgnoreCase ) || mod.Name.Contains( _modFilter ) ),
            1  => !mod.Name.Contains( _modFilter ),
            2  => !mod.Author.Contains( _modFilter ),
            3  => !mod.LowerChangedItemsString.Contains( _modFilter.Lower ),
            4  => !mod.AllTagsLower.Contains( _modFilter.Lower ),
            _  => false, // Should never happen
        };
    }

    // Only get the text color for a mod if no filters are set.
    private static ColorId GetTextColor( Mod mod, ModSettings? settings, ModCollection collection )
    {
        if( Penumbra.ModManager.NewMods.Contains( mod ) )
        {
            return ColorId.NewMod;
        }

        if( settings == null )
        {
            return ColorId.UndefinedMod;
        }

        if( !settings.Enabled )
        {
            return collection != Penumbra.CollectionManager.Current ? ColorId.InheritedDisabledMod : ColorId.DisabledMod;
        }

        var conflicts = Penumbra.CollectionManager.Current.Conflicts( mod );
        if( conflicts.Count == 0 )
        {
            return collection != Penumbra.CollectionManager.Current ? ColorId.InheritedMod : ColorId.EnabledMod;
        }

        return conflicts.Any( c => !c.Solved )
            ? ColorId.ConflictingMod
            : ColorId.HandledConflictMod;
    }

    private bool CheckStateFilters( Mod mod, ModSettings? settings, ModCollection collection, ref ModState state )
    {
        var isNew = Penumbra.ModManager.NewMods.Contains( mod );
        // Handle mod details.
        if( CheckFlags( mod.TotalFileCount, ModFilter.HasNoFiles, ModFilter.HasFiles )
        || CheckFlags( mod.TotalSwapCount, ModFilter.HasNoFileSwaps, ModFilter.HasFileSwaps )
        || CheckFlags( mod.TotalManipulations, ModFilter.HasNoMetaManipulations, ModFilter.HasMetaManipulations )
        || CheckFlags( mod.HasOptions ? 1 : 0, ModFilter.HasNoConfig, ModFilter.HasConfig )
        || CheckFlags( isNew ? 1 : 0, ModFilter.NotNew, ModFilter.IsNew ) )
        {
            return true;
        }

        // Handle Favoritism
        if( !_stateFilter.HasFlag( ModFilter.Favorite )   && mod.Favorite
        || !_stateFilter.HasFlag( ModFilter.NotFavorite ) && !mod.Favorite )
        {
            return true;
        }

        // Handle Inheritance
        if( collection == Penumbra.CollectionManager.Current )
        {
            if( !_stateFilter.HasFlag( ModFilter.Uninherited ) )
            {
                return true;
            }
        }
        else
        {
            state.Color = ColorId.InheritedMod;
            if( !_stateFilter.HasFlag( ModFilter.Inherited ) )
            {
                return true;
            }
        }

        // Handle settings.
        if( settings == null )
        {
            state.Color = ColorId.UndefinedMod;
            if( !_stateFilter.HasFlag( ModFilter.Undefined )
            || !_stateFilter.HasFlag( ModFilter.Disabled )
            || !_stateFilter.HasFlag( ModFilter.NoConflict ) )
            {
                return true;
            }
        }
        else if( !settings.Enabled )
        {
            state.Color = collection == Penumbra.CollectionManager.Current ? ColorId.DisabledMod : ColorId.InheritedDisabledMod;
            if( !_stateFilter.HasFlag( ModFilter.Disabled )
            || !_stateFilter.HasFlag( ModFilter.NoConflict ) )
            {
                return true;
            }
        }
        else
        {
            if( !_stateFilter.HasFlag( ModFilter.Enabled ) )
            {
                return true;
            }

            // Conflicts can only be relevant if the mod is enabled.
            var conflicts = Penumbra.CollectionManager.Current.Conflicts( mod );
            if( conflicts.Count > 0 )
            {
                if( conflicts.Any( c => !c.Solved ) )
                {
                    if( !_stateFilter.HasFlag( ModFilter.UnsolvedConflict ) )
                    {
                        return true;
                    }

                    state.Color = ColorId.ConflictingMod;
                }
                else
                {
                    if( !_stateFilter.HasFlag( ModFilter.SolvedConflict ) )
                    {
                        return true;
                    }

                    state.Color = ColorId.HandledConflictMod;
                }
            }
            else if( !_stateFilter.HasFlag( ModFilter.NoConflict ) )
            {
                return true;
            }
        }

        // isNew color takes precedence before other colors.
        if( isNew )
        {
            state.Color = ColorId.NewMod;
        }

        return false;
    }

    // Combined wrapper for handling all filters and setting state.
    private bool ApplyFiltersAndState( ModFileSystem.Leaf leaf, out ModState state )
    {
        state = new ModState { Color = ColorId.EnabledMod };
        var mod = leaf.Value;
        var (settings, collection) = Penumbra.CollectionManager.Current[ mod.Index ];

        if( ApplyStringFilters( leaf, mod ) )
        {
            return true;
        }

        if( _stateFilter != ModFilterExtensions.UnfilteredStateMods )
        {
            return CheckStateFilters( mod, settings, collection, ref state );
        }

        state.Color = GetTextColor( mod, settings, collection );
        return false;
    }

    private void DrawFilterCombo( ref bool everything )
    {
        using var combo = ImRaii.Combo( "##filterCombo", string.Empty,
            ImGuiComboFlags.NoPreview | ImGuiComboFlags.PopupAlignLeft | ImGuiComboFlags.HeightLargest );
        if( combo )
        {
            using var style = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing,
                ImGui.GetStyle().ItemSpacing with { Y = 3 * ImGuiHelpers.GlobalScale } );
            var flags = ( int )_stateFilter;


            if( ImGui.Checkbox( "Everything", ref everything ) )
            {
                _stateFilter = everything ? ModFilterExtensions.UnfilteredStateMods : 0;
                SetFilterDirty();
            }

            ImGui.Dummy( new Vector2( 0, 5 * ImGuiHelpers.GlobalScale ) );
            foreach( ModFilter flag in Enum.GetValues( typeof( ModFilter ) ) )
            {
                if( ImGui.CheckboxFlags( flag.ToName(), ref flags, ( int )flag ) )
                {
                    _stateFilter = ( ModFilter )flags;
                    SetFilterDirty();
                }
            }
        }
    }

    // Add the state filter combo-button to the right of the filter box.
    protected override float CustomFilters( float width )
    {
        var pos            = ImGui.GetCursorPos();
        var remainingWidth = width - ImGui.GetFrameHeight();
        var comboPos       = new Vector2( pos.X + remainingWidth, pos.Y );

        var everything = _stateFilter == ModFilterExtensions.UnfilteredStateMods;

        ImGui.SetCursorPos( comboPos );
        // Draw combo button
        using var color = ImRaii.PushColor( ImGuiCol.Button, Colors.FilterActive, !everything );
        DrawFilterCombo( ref everything );
        ConfigWindow.OpenTutorial( ConfigWindow.BasicTutorialSteps.ModFilters );
        if( ImGui.IsItemClicked( ImGuiMouseButton.Right ) )
        {
            _stateFilter = ModFilterExtensions.UnfilteredStateMods;
            SetFilterDirty();
        }

        ImGuiUtil.HoverTooltip( "Filter mods for their activation status.\nRight-Click to clear all filters." );
        ImGui.SetCursorPos( pos );
        return remainingWidth;
    }
}