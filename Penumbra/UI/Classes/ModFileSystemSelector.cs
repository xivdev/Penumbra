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

namespace Penumbra.UI.Classes;

public sealed partial class ModFileSystemSelector : FileSystemSelector< Mod2, ModFileSystemSelector.ModState >
{
    public ModSettings2 SelectedSettings { get; private set; } = ModSettings2.Empty;
    public ModCollection SelectedSettingCollection { get; private set; } = ModCollection.Empty;

    public ModFileSystemSelector( ModFileSystemA fileSystem, IReadOnlySet< Mod2 > newMods )
        : base( fileSystem )
    {
        _newMods = newMods;
        SubscribeRightClickFolder( EnableDescendants, 10 );
        SubscribeRightClickFolder( DisableDescendants, 10 );
        SubscribeRightClickFolder( InheritDescendants, 15 );
        SubscribeRightClickFolder( OwnDescendants, 15 );
        AddButton( AddNewModButton, 0 );
        AddButton( DeleteModButton, 1000 );
        SetFilterTooltip();

        Penumbra.CollectionManager.CollectionChanged          += OnCollectionChange;
        Penumbra.CollectionManager.Current.ModSettingChanged  += OnSettingChange;
        Penumbra.CollectionManager.Current.InheritanceChanged += OnInheritanceChange;
        Penumbra.ModManager.ModDiscoveryStarted               += StoreCurrentSelection;
        Penumbra.ModManager.ModDiscoveryFinished              += RestoreLastSelection;
        OnCollectionChange( ModCollection.Type.Current, null, Penumbra.CollectionManager.Current, null );
    }

    public override void Dispose()
    {
        base.Dispose();
        Penumbra.ModManager.ModDiscoveryStarted               -= StoreCurrentSelection;
        Penumbra.ModManager.ModDiscoveryFinished              -= RestoreLastSelection;
        Penumbra.CollectionManager.Current.ModSettingChanged  -= OnSettingChange;
        Penumbra.CollectionManager.Current.InheritanceChanged -= OnInheritanceChange;
        Penumbra.CollectionManager.CollectionChanged          -= OnCollectionChange;
    }

    // Customization points.
    public override SortMode SortMode
        => Penumbra.Config.SortFoldersFirst ? SortMode.FoldersFirst : SortMode.Lexicographical;

    protected override uint ExpandedFolderColor
        => ColorId.FolderExpanded.Value();

    protected override uint CollapsedFolderColor
        => ColorId.FolderCollapsed.Value();

    protected override uint FolderLineColor
        => ColorId.FolderLine.Value();

    protected override void DrawLeafName( FileSystem< Mod2 >.Leaf leaf, in ModState state, bool selected )
    {
        var       flags = selected ? ImGuiTreeNodeFlags.Selected | LeafFlags : LeafFlags;
        using var c     = ImRaii.PushColor( ImGuiCol.Text, state.Color );
        using var _     = ImRaii.TreeNode( leaf.Value.Name, flags );
    }


    // Add custom context menu items.
    private static void EnableDescendants( ModFileSystemA.Folder folder )
    {
        if( ImGui.MenuItem( "Enable Descendants" ) )
        {
            SetDescendants( folder, true );
        }
    }

    private static void DisableDescendants( ModFileSystemA.Folder folder )
    {
        if( ImGui.MenuItem( "Disable Descendants" ) )
        {
            SetDescendants( folder, false );
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


    // Add custom buttons.
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


    // Helpers.
    private static void SetDescendants( ModFileSystemA.Folder folder, bool enabled, bool inherit = false )
    {
        var mods = folder.GetAllDescendants( SortMode.Lexicographical ).OfType< ModFileSystemA.Leaf >().Select( l => l.Value );
        if( inherit )
        {
            Penumbra.CollectionManager.Current.SetMultipleModInheritances( mods, enabled );
        }
        else
        {
            Penumbra.CollectionManager.Current.SetMultipleModStates( mods, enabled );
        }
    }

    // Automatic cache update functions.
    private void OnSettingChange( ModSettingChange type, int modIdx, int oldValue, int groupIdx, bool inherited )
    {
        // TODO: maybe make more efficient
        SetFilterDirty();
        if( modIdx == Selected?.Index )
        {
            OnSelectionChange( SelectedLeaf, SelectedLeaf, default );
        }
    }

    private void OnInheritanceChange( bool _ )
    {
        SetFilterDirty();
        OnSelectionChange( SelectedLeaf, SelectedLeaf, default );
    }

    private void OnCollectionChange( ModCollection.Type type, ModCollection? oldCollection, ModCollection? newCollection, string? _ )
    {
        if( type != ModCollection.Type.Current || oldCollection == newCollection )
        {
            return;
        }

        if( oldCollection != null )
        {
            oldCollection.ModSettingChanged  -= OnSettingChange;
            oldCollection.InheritanceChanged -= OnInheritanceChange;
        }

        if( newCollection != null )
        {
            newCollection.ModSettingChanged  += OnSettingChange;
            newCollection.InheritanceChanged += OnInheritanceChange;
        }

        SetFilterDirty();
        OnSelectionChange( SelectedLeaf, SelectedLeaf, default );
    }

    private void OnSelectionChange( ModFileSystemA.Leaf? _1, ModFileSystemA.Leaf? newSelection, in ModState _2 )
    {
        if( newSelection == null )
        {
            SelectedSettings          = ModSettings2.Empty;
            SelectedSettingCollection = ModCollection.Empty;
        }
        else
        {
            ( var settings, SelectedSettingCollection ) = Penumbra.CollectionManager.Current[ newSelection.Value.Index ];
            SelectedSettings                            = settings ?? ModSettings2.Empty;
        }
    }

    // Keep selections across rediscoveries if possible.
    private string _lastSelectedDirectory = string.Empty;

    private void StoreCurrentSelection()
    {
        _lastSelectedDirectory = Selected?.BasePath.FullName ?? string.Empty;
        ClearSelection();
    }

    private void RestoreLastSelection()
    {
        if( _lastSelectedDirectory.Length > 0 )
        {
            SelectedLeaf = ( ModFileSystemA.Leaf? )FileSystem.Root.GetAllDescendants( SortMode.Lexicographical )
               .FirstOrDefault( l => l is ModFileSystemA.Leaf m && m.Value.BasePath.FullName == _lastSelectedDirectory );
            _lastSelectedDirectory = string.Empty;
        }
    }
}