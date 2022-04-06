using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.FileSystem.Selector;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.Mods;

namespace Penumbra.UI;

public sealed class ModFileSystemSelector : FileSystemSelector< Mod, ModState >
{
    private readonly IReadOnlySet< Mod > _newMods = new HashSet<Mod>();
    private          LowerString         _modFilter        = LowerString.Empty;
    private          LowerString         _modFilterAuthor  = LowerString.Empty;
    private          LowerString         _modFilterChanges = LowerString.Empty;
    private          LowerString         _modFilterName    = LowerString.Empty;
    private          ModFilter           _stateFilter      = ModFilterExtensions.UnfilteredStateMods;

    public ModFilter StateFilter
    {
        get => _stateFilter;
        set
        {
            var diff = _stateFilter != value;
            _stateFilter = value;
            if( diff )
            {
                SetFilterDirty();
            }
        }
    }

    protected override bool ChangeFilter( string filterValue )
    {
        if( filterValue.StartsWith( "c:", StringComparison.InvariantCultureIgnoreCase ) )
        {
            _modFilterChanges = new LowerString( filterValue[ 2.. ] );
            _modFilter        = LowerString.Empty;
            _modFilterAuthor  = LowerString.Empty;
            _modFilterName    = LowerString.Empty;
        }
        else if( filterValue.StartsWith( "a:", StringComparison.InvariantCultureIgnoreCase ) )
        {
            _modFilterAuthor  = new LowerString( filterValue[ 2.. ] );
            _modFilter        = LowerString.Empty;
            _modFilterChanges = LowerString.Empty;
            _modFilterName    = LowerString.Empty;
        }
        else if( filterValue.StartsWith( "n:", StringComparison.InvariantCultureIgnoreCase ) )
        {
            _modFilterName    = new LowerString( filterValue[ 2.. ] );
            _modFilter        = LowerString.Empty;
            _modFilterChanges = LowerString.Empty;
            _modFilterAuthor  = LowerString.Empty;
        }
        else
        {
            _modFilter        = new LowerString( filterValue );
            _modFilterAuthor  = LowerString.Empty;
            _modFilterChanges = LowerString.Empty;
            _modFilterName    = LowerString.Empty;
        }

        return true;
    }

    private bool CheckFlags( int count, ModFilter hasNoFlag, ModFilter hasFlag )
    {
        if( count == 0 )
        {
            if( StateFilter.HasFlag( hasNoFlag ) )
            {
                return false;
            }
        }
        else if( StateFilter.HasFlag( hasFlag ) )
        {
            return false;
        }

        return true;
    }

    private ModState GetModState( Mod mod, ModSettings? settings )
    {
        if( settings?.Enabled != true )
        {
            return new ModState { Color = ImGui.GetColorU32( ImGuiCol.TextDisabled ) };
        }

        return new ModState { Color = ImGui.GetColorU32( ImGuiCol.Text ) };
    }

    protected override bool ApplyFiltersAndState( FileSystem< Mod >.IPath path, out ModState state )
    {
        if( path is ModFileSystemA.Folder f )
        {
            return base.ApplyFiltersAndState( f, out state );
        }

        return ApplyFiltersAndState( ( ModFileSystemA.Leaf )path, out state );
    }

    private bool CheckPath( string path, Mod mod )
        => _modFilter.IsEmpty
         || path.Contains( _modFilter.Lower, StringComparison.InvariantCultureIgnoreCase )
         || mod.Meta.Name.Contains( _modFilter );

    private bool CheckName( Mod mod )
        => _modFilterName.IsEmpty || mod.Meta.Name.Contains( _modFilterName );

    private bool CheckAuthor( Mod mod )
        => _modFilterAuthor.IsEmpty || mod.Meta.Author.Contains( _modFilterAuthor );

    private bool CheckItems( Mod mod )
        => _modFilterChanges.IsEmpty || mod.LowerChangedItemsString.Contains( _modFilterChanges.Lower );

    private bool ApplyFiltersAndState( ModFileSystemA.Leaf leaf, out ModState state )
    {
        state = new ModState { Color = Colors.DefaultTextColor };
        var mod = leaf.Value;
        var (settings, collection) = Current[ mod.Index ];
        // Check string filters.
        if( !( CheckPath( leaf.FullName(), mod )
            && CheckName( mod )
            && CheckAuthor( mod )
            && CheckItems( mod ) ) )
        {
            return true;
        }

        var isNew = _newMods.Contains( mod );
        if( CheckFlags( mod.Resources.ModFiles.Count, ModFilter.HasNoFiles, ModFilter.HasFiles )
        || CheckFlags( mod.Meta.FileSwaps.Count, ModFilter.HasNoFileSwaps, ModFilter.HasFileSwaps )
        || CheckFlags( mod.Resources.MetaManipulations.Count, ModFilter.HasNoMetaManipulations, ModFilter.HasMetaManipulations )
        || CheckFlags( mod.Meta.HasGroupsWithConfig ? 1 : 0, ModFilter.HasNoConfig, ModFilter.HasConfig )
        || CheckFlags( isNew ? 1 : 0, ModFilter.IsNew, ModFilter.NotNew ) )
        {
            return true;
        }

        if( settings == null )
        {
            state.Color = Colors.DisabledModColor;
            if( !StateFilter.HasFlag( ModFilter.Undefined ) )
            {
                return true;
            }

            settings = new ModSettings();
        }


        if( !settings.Enabled )
        {
            state.Color = Colors.DisabledModColor;
            if( !StateFilter.HasFlag( ModFilter.Disabled ) )
            {
                return true;
            }
        }
        else
        {
            if( !StateFilter.HasFlag( ModFilter.Enabled ) )
            {
                return true;
            }

            var conflicts = Penumbra.CollectionManager.Current.ModConflicts( mod.Index ).ToList();
            if( conflicts.Count > 0 )
            {
                if( conflicts.Any( c => !c.Solved ) )
                {
                    if( !StateFilter.HasFlag( ModFilter.UnsolvedConflict ) )
                    {
                        return true;
                    }

                    state.Color = Colors.ConflictingModColor;
                }
                else
                {
                    if( !StateFilter.HasFlag( ModFilter.SolvedConflict ) )
                    {
                        return true;
                    }

                    state.Color = Colors.HandledConflictModColor;
                }
            }
            else if( !StateFilter.HasFlag( ModFilter.NoConflict ) )
            {
                return true;
            }
        }

        if( collection == Current )
        {
            if( !StateFilter.HasFlag( ModFilter.Uninherited ) )
            {
                return true;
            }
        }
        else
        {
            if( !StateFilter.HasFlag( ModFilter.Inherited ) )
            {
                return true;
            }
        }

        if( isNew )
        {
            state.Color = Colors.NewModColor;
        }

        return false;
    }


    protected override float CustomFilters( float width )
    {
        var pos            = ImGui.GetCursorPos();
        var remainingWidth = width - ImGui.GetFrameHeight();
        var comboPos       = new Vector2( pos.X + remainingWidth, pos.Y );
        ImGui.SetCursorPos( comboPos );
        using var combo = ImRaii.Combo( "##filterCombo", string.Empty,
            ImGuiComboFlags.NoPreview | ImGuiComboFlags.PopupAlignLeft | ImGuiComboFlags.HeightLargest );

        if( combo )
        {
            ImGui.Text( "A" );
            ImGui.Text( "B" );
            ImGui.Text( "C" );
        }

        combo.Dispose();
        ImGui.SetCursorPos( pos );
        return remainingWidth;
    }


    public ModFileSystemSelector( ModFileSystemA fileSystem )
        : base( fileSystem )
    {
        SubscribeRightClickFolder( EnableDescendants, 10 );
        SubscribeRightClickFolder( DisableDescendants, 10 );
        SubscribeRightClickFolder( InheritDescendants, 15 );
        SubscribeRightClickFolder( OwnDescendants, 15 );
        AddButton( AddNewModButton, 0 );
        AddButton( DeleteModButton, 1000 );
    }

    private static ModCollection Current
        => Penumbra.CollectionManager.Current;

    private static void EnableDescendants( ModFileSystemA.Folder folder )
    {
        if( ImGui.MenuItem( "Enable Descendants" ) )
        {
            SetDescendants( folder, true, false );
        }
    }

    private static void DisableDescendants( ModFileSystemA.Folder folder )
    {
        if( ImGui.MenuItem( "Disable Descendants" ) )
        {
            SetDescendants( folder, false, false );
        }
    }

    private static void InheritDescendants( ModFileSystemA.Folder folder )
    {
        if( ImGui.MenuItem( "Inherit Descendants" ) )
        {
            SetDescendants( folder, true, true );
        }
    }

    private static void OwnDescendants( ModFileSystemA.Folder folder )
    {
        if( ImGui.MenuItem( "Stop Inheriting Descendants" ) )
        {
            SetDescendants( folder, false, true );
        }
    }

    private static void AddNewModButton( Vector2 size )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), size, "Create a new, empty mod of a given name.", false, true ) )
        { }
    }

    private void DeleteModButton( Vector2 size )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), size,
               "Delete the currently selected mod entirely from your drive.", SelectedLeaf == null, true ) )
        { }
    }

    private static void SetDescendants( ModFileSystemA.Folder folder, bool enabled, bool inherit = false )
    {
        var mods = folder.GetAllDescendants( SortMode.Lexicographical ).OfType< ModFileSystemA.Leaf >().Select( l => l.Value );
        if( inherit )
        {
            Current.SetMultipleModInheritances( mods, enabled );
        }
        else
        {
            Current.SetMultipleModStates( mods, enabled );
        }
    }

    public override SortMode SortMode
        => Penumbra.Config.SortFoldersFirst ? SortMode.FoldersFirst : SortMode.Lexicographical;

    protected override void DrawLeafName( FileSystem< Mod >.Leaf leaf, in ModState state, bool selected )
    {
        var       flags = selected ? ImGuiTreeNodeFlags.Selected | LeafFlags : LeafFlags;
        using var c     = ImRaii.PushColor( ImGuiCol.Text, state.Color );
        using var _     = ImRaii.TreeNode( leaf.Value.Meta.Name, flags );
    }
}