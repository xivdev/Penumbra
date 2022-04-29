using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Logging;
using ImGuiNET;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.FileSystem.Selector;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.Import;
using Penumbra.Mods;

namespace Penumbra.UI.Classes;

public sealed partial class ModFileSystemSelector : FileSystemSelector< Mod, ModFileSystemSelector.ModState >
{
    private readonly FileDialogManager _fileManager = new();
    private          TexToolsImporter? _import;
    public ModSettings SelectedSettings { get; private set; } = ModSettings.Empty;
    public ModCollection SelectedSettingCollection { get; private set; } = ModCollection.Empty;

    public ModFileSystemSelector( ModFileSystem fileSystem )
        : base( fileSystem )
    {
        SubscribeRightClickFolder( EnableDescendants, 10 );
        SubscribeRightClickFolder( DisableDescendants, 10 );
        SubscribeRightClickFolder( InheritDescendants, 15 );
        SubscribeRightClickFolder( OwnDescendants, 15 );
        AddButton( AddNewModButton, 0 );
        AddButton( AddImportModButton, 1 );
        AddButton( DeleteModButton, 1000 );
        SetFilterTooltip();

        SelectionChanged                                      += OnSelectionChange;
        Penumbra.CollectionManager.CollectionChanged          += OnCollectionChange;
        Penumbra.CollectionManager.Current.ModSettingChanged  += OnSettingChange;
        Penumbra.CollectionManager.Current.InheritanceChanged += OnInheritanceChange;
        Penumbra.ModManager.ModMetaChanged                    += OnModMetaChange;
        Penumbra.ModManager.ModDiscoveryStarted               += StoreCurrentSelection;
        Penumbra.ModManager.ModDiscoveryFinished              += RestoreLastSelection;
        OnCollectionChange( ModCollection.Type.Current, null, Penumbra.CollectionManager.Current, null );
    }

    public override void Dispose()
    {
        base.Dispose();
        Penumbra.ModManager.ModDiscoveryStarted               -= StoreCurrentSelection;
        Penumbra.ModManager.ModDiscoveryFinished              -= RestoreLastSelection;
        Penumbra.ModManager.ModMetaChanged                    -= OnModMetaChange;
        Penumbra.CollectionManager.Current.ModSettingChanged  -= OnSettingChange;
        Penumbra.CollectionManager.Current.InheritanceChanged -= OnInheritanceChange;
        Penumbra.CollectionManager.CollectionChanged          -= OnCollectionChange;
    }

    public new ModFileSystem.Leaf? SelectedLeaf
        => base.SelectedLeaf;

    // Customization points.
    public override SortMode SortMode
        => Penumbra.Config.SortMode;

    protected override uint ExpandedFolderColor
        => ColorId.FolderExpanded.Value();

    protected override uint CollapsedFolderColor
        => ColorId.FolderCollapsed.Value();

    protected override uint FolderLineColor
        => ColorId.FolderLine.Value();

    protected override void DrawLeafName( FileSystem< Mod >.Leaf leaf, in ModState state, bool selected )
    {
        var       flags = selected ? ImGuiTreeNodeFlags.Selected | LeafFlags : LeafFlags;
        using var c     = ImRaii.PushColor( ImGuiCol.Text, state.Color );
        using var id    = ImRaii.PushId( leaf.Value.Index );
        using var _     = ImRaii.TreeNode( leaf.Value.Name, flags );
    }


    // Add custom context menu items.
    private static void EnableDescendants( ModFileSystem.Folder folder )
    {
        if( ImGui.MenuItem( "Enable Descendants" ) )
        {
            SetDescendants( folder, true );
        }
    }

    private static void DisableDescendants( ModFileSystem.Folder folder )
    {
        if( ImGui.MenuItem( "Disable Descendants" ) )
        {
            SetDescendants( folder, false );
        }
    }

    private static void InheritDescendants( ModFileSystem.Folder folder )
    {
        if( ImGui.MenuItem( "Inherit Descendants" ) )
        {
            SetDescendants( folder, true, true );
        }
    }

    private static void OwnDescendants( ModFileSystem.Folder folder )
    {
        if( ImGui.MenuItem( "Stop Inheriting Descendants" ) )
        {
            SetDescendants( folder, false, true );
        }
    }


    // Add custom buttons.
    private string _newModName = string.Empty;

    private void AddNewModButton( Vector2 size )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), size, "Create a new, empty mod of a given name.",
               !Penumbra.ModManager.Valid, true ) )
        {
            ImGui.OpenPopup( "Create New Mod" );
        }

        if( ImGuiUtil.OpenNameField( "Create New Mod", ref _newModName ) )
        {
            try
            {
                var newDir = Mod.CreateModFolder( Penumbra.ModManager.BasePath, _newModName );
                Mod.CreateMeta( newDir, _newModName, Penumbra.Config.DefaultModAuthor, string.Empty, "1.0", string.Empty );
                Penumbra.ModManager.AddMod( newDir );
                _newModName = string.Empty;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not create directory for new Mod {_newModName}:\n{e}" );
            }
        }
    }

    // Add an import mods button that opens a file selector.
    // Only set the initial directory once.
    private bool _hasSetFolder;

    private void AddImportModButton( Vector2 size )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.FileImport.ToIconString(), size,
               "Import one or multiple mods from Tex Tools Mod Pack Files.", !Penumbra.ModManager.Valid, true ) )
        {
            var modPath = _hasSetFolder                           ? null
                : Penumbra.Config.DefaultModImportPath.Length > 0 ? Penumbra.Config.DefaultModImportPath
                : Penumbra.Config.ModDirectory.Length         > 0 ? Penumbra.Config.ModDirectory : null;
            _hasSetFolder = true;
            _fileManager.OpenFileDialog( "Import Mod Pack", "TexTools Mod Packs{.ttmp,.ttmp2}", ( s, f ) =>
            {
                if( s )
                {
                    _import = new TexToolsImporter( Penumbra.ModManager.BasePath, f.Count, f.Select( file => new FileInfo( file ) ) );
                    ImGui.OpenPopup( "Import Status" );
                }
            }, 0, modPath );
        }

        _fileManager.Draw();
        DrawInfoPopup();
    }

    // Draw the progress information for import.
    private void DrawInfoPopup()
    {
        var display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowSize( display    / 4 );
        ImGui.SetNextWindowPos( 3 * display / 8 );
        using var popup = ImRaii.Popup( "Import Status", ImGuiWindowFlags.Modal );
        if( _import != null && popup.Success )
        {
            _import.DrawProgressInfo( new Vector2( -1, ImGui.GetFrameHeight() ) );
            if( _import.State == ImporterState.Done )
            {
                ImGui.SetCursorPosY( ImGui.GetWindowHeight() - ImGui.GetFrameHeight() * 2 );
                if( ImGui.Button( "Close", -Vector2.UnitX ) )
                {
                    AddNewMods( _import.ExtractedMods );
                    _import = null;
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    // Clean up invalid directories if necessary.
    // Add all successfully extracted mods.
    private static void AddNewMods( IEnumerable< (FileInfo File, DirectoryInfo? Mod, Exception? Error) > list )
    {
        foreach( var (file, dir, error) in list )
        {
            if( error != null )
            {
                if( dir != null && Directory.Exists( dir.FullName ) )
                {
                    try
                    {
                        Directory.Delete( dir.FullName );
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Error cleaning up failed mod extraction of {file.FullName} to {dir.FullName}:\n{e}" );
                    }
                }

                PluginLog.Error( $"Error extracting {file.FullName}, mod skipped:\n{error}" );
                continue;
            }

            if( dir != null )
            {
                Penumbra.ModManager.AddMod( dir );
            }
        }
    }

    private void DeleteModButton( Vector2 size )
    {
        var keys = ImGui.GetIO().KeyCtrl && ImGui.GetIO().KeyShift;
        var tt = SelectedLeaf == null
            ? "No mod selected."
            : "Delete the currently selected mod entirely from your drive.\n"
          + "This can not be undone.";
        if( !keys )
        {
            tt += "\nHold Control and Shift while clicking to delete the mod.";
        }

        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), size, tt, SelectedLeaf == null || !keys, true )
        && Selected != null )
        {
            Penumbra.ModManager.DeleteMod( Selected.Index );
        }
    }


    // Helpers.
    private static void SetDescendants( ModFileSystem.Folder folder, bool enabled, bool inherit = false )
    {
        var mods = folder.GetAllDescendants( SortMode.Lexicographical ).OfType< ModFileSystem.Leaf >().Select( l => l.Value );
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
            OnSelectionChange( Selected, Selected, default );
        }
    }

    private void OnModMetaChange( MetaChangeType type, Mod mod, string? oldName )
    {
        switch( type )
        {
            case MetaChangeType.Name:
            case MetaChangeType.Author:
                SetFilterDirty();
                break;
        }
    }

    private void OnInheritanceChange( bool _ )
    {
        SetFilterDirty();
        OnSelectionChange( Selected, Selected, default );
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
        OnSelectionChange( Selected, Selected, default );
    }

    private void OnSelectionChange( Mod? _1, Mod? newSelection, in ModState _2 )
    {
        if( newSelection == null )
        {
            SelectedSettings          = ModSettings.Empty;
            SelectedSettingCollection = ModCollection.Empty;
        }
        else
        {
            ( var settings, SelectedSettingCollection ) = Penumbra.CollectionManager.Current[ newSelection.Index ];
            SelectedSettings                            = settings ?? ModSettings.Empty;
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
            base.SelectedLeaf = ( ModFileSystem.Leaf? )FileSystem.Root.GetAllDescendants( SortMode.Lexicographical )
               .FirstOrDefault( l => l is ModFileSystem.Leaf m && m.Value.BasePath.FullName == _lastSelectedDirectory );
            OnSelectionChange( null, base.SelectedLeaf?.Value, default );
            _lastSelectedDirectory = string.Empty;
        }
    }
}