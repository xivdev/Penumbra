using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.FileSystem.Selector;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.Import;
using Penumbra.Mods;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Numerics;
using Penumbra.Api.Enums;

namespace Penumbra.UI.Classes;

public sealed partial class ModFileSystemSelector : FileSystemSelector< Mod, ModFileSystemSelector.ModState >
{
    private readonly FileDialogManager _fileManager = ConfigWindow.SetupFileManager();
    private          TexToolsImporter? _import;
    public ModSettings SelectedSettings { get; private set; } = ModSettings.Empty;
    public ModCollection SelectedSettingCollection { get; private set; } = ModCollection.Empty;

    public ModFileSystemSelector( ModFileSystem fileSystem )
        : base( fileSystem, Dalamud.KeyState )
    {
        SubscribeRightClickFolder( EnableDescendants, 10 );
        SubscribeRightClickFolder( DisableDescendants, 10 );
        SubscribeRightClickFolder( InheritDescendants, 15 );
        SubscribeRightClickFolder( OwnDescendants, 15 );
        SubscribeRightClickFolder( SetDefaultImportFolder, 100 );
        SubscribeRightClickLeaf( ToggleLeafFavorite, 0 );
        SubscribeRightClickMain( ClearDefaultImportFolder, 100 );
        AddButton( AddNewModButton, 0 );
        AddButton( AddImportModButton, 1 );
        AddButton( AddHelpButton, 2 );
        AddButton( DeleteModButton, 1000 );
        SetFilterTooltip();

        SelectionChanged                                      += OnSelectionChange;
        Penumbra.CollectionManager.CollectionChanged          += OnCollectionChange;
        Penumbra.CollectionManager.Current.ModSettingChanged  += OnSettingChange;
        Penumbra.CollectionManager.Current.InheritanceChanged += OnInheritanceChange;
        Penumbra.ModManager.ModDataChanged                    += OnModDataChange;
        Penumbra.ModManager.ModDiscoveryStarted               += StoreCurrentSelection;
        Penumbra.ModManager.ModDiscoveryFinished              += RestoreLastSelection;
        OnCollectionChange( CollectionType.Current, null, Penumbra.CollectionManager.Current, "" );
    }

    public override void Dispose()
    {
        base.Dispose();
        Penumbra.ModManager.ModDiscoveryStarted               -= StoreCurrentSelection;
        Penumbra.ModManager.ModDiscoveryFinished              -= RestoreLastSelection;
        Penumbra.ModManager.ModDataChanged                    -= OnModDataChange;
        Penumbra.CollectionManager.Current.ModSettingChanged  -= OnSettingChange;
        Penumbra.CollectionManager.Current.InheritanceChanged -= OnInheritanceChange;
        Penumbra.CollectionManager.CollectionChanged          -= OnCollectionChange;
        _import?.Dispose();
        _import = null;
    }

    public new ModFileSystem.Leaf? SelectedLeaf
        => base.SelectedLeaf;

    // Customization points.
    public override ISortMode< Mod > SortMode
        => Penumbra.Config.SortMode;

    protected override uint ExpandedFolderColor
        => ColorId.FolderExpanded.Value();

    protected override uint CollapsedFolderColor
        => ColorId.FolderCollapsed.Value();

    protected override uint FolderLineColor
        => ColorId.FolderLine.Value();

    protected override bool FoldersDefaultOpen
        => Penumbra.Config.OpenFoldersByDefault;

    protected override void DrawPopups()
    {
        _fileManager.Draw();
        DrawHelpPopup();
        DrawInfoPopup();

        if( ImGuiUtil.OpenNameField( "Create New Mod", ref _newModName ) )
        {
            try
            {
                var newDir = Mod.CreateModFolder( Penumbra.ModManager.BasePath, _newModName );
                Mod.CreateMeta( newDir, _newModName, Penumbra.Config.DefaultModAuthor, string.Empty, "1.0", string.Empty );
                Mod.CreateDefaultFiles( newDir );
                Penumbra.ModManager.AddMod( newDir );
                _newModName = string.Empty;
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not create directory for new Mod {_newModName}:\n{e}" );
            }
        }

        while( _modsToAdd.TryDequeue( out var dir ) )
        {
            Penumbra.ModManager.AddMod( dir );
            var mod = Penumbra.ModManager.LastOrDefault();
            if( mod != null )
            {
                MoveModToDefaultDirectory( mod );
                SelectByValue( mod );
            }
        }
    }

    protected override void DrawLeafName( FileSystem< Mod >.Leaf leaf, in ModState state, bool selected )
    {
        var flags = selected ? ImGuiTreeNodeFlags.Selected | LeafFlags : LeafFlags;
        using var c = ImRaii.PushColor( ImGuiCol.Text, state.Color.Value() )
           .Push( ImGuiCol.HeaderHovered, 0x4000FFFF, leaf.Value.Favorite );
        using var id = ImRaii.PushId( leaf.Value.Index );
        ImRaii.TreeNode( leaf.Value.Name, flags ).Dispose();
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

    private static void ToggleLeafFavorite( FileSystem< Mod >.Leaf mod )
    {
        if( ImGui.MenuItem( mod.Value.Favorite ? "Remove Favorite" : "Mark as Favorite" ) )
        {
            Penumbra.ModManager.ChangeModFavorite( mod.Value.Index, !mod.Value.Favorite );
        }
    }

    private static void SetDefaultImportFolder( ModFileSystem.Folder folder )
    {
        if( ImGui.MenuItem( "Set As Default Import Folder" ) )
        {
            var newName = folder.FullName();
            if( newName != Penumbra.Config.DefaultImportFolder )
            {
                Penumbra.Config.DefaultImportFolder = newName;
                Penumbra.Config.Save();
            }
        }
    }

    private static void ClearDefaultImportFolder()
    {
        if( ImGui.MenuItem( "Clear Default Import Folder" ) && Penumbra.Config.DefaultImportFolder.Length > 0 )
        {
            Penumbra.Config.DefaultImportFolder = string.Empty;
            Penumbra.Config.Save();
        }
    }


    // Add custom buttons.
    private string _newModName = string.Empty;

    private static void AddNewModButton( Vector2 size )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), size, "Create a new, empty mod of a given name.",
               !Penumbra.ModManager.Valid, true ) )
        {
            ImGui.OpenPopup( "Create New Mod" );
        }
    }

    // Add an import mods button that opens a file selector.
    // Only set the initial directory once.
    private bool _hasSetFolder;

    private void AddImportModButton( Vector2 size )
    {
        var button = ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.FileImport.ToIconString(), size,
            "Import one or multiple mods from Tex Tools Mod Pack Files or Penumbra Mod Pack Files.", !Penumbra.ModManager.Valid, true );
        ConfigWindow.OpenTutorial( ConfigWindow.BasicTutorialSteps.ModImport );
        if( !button )
        {
            return;
        }

        var modPath = _hasSetFolder && !Penumbra.Config.AlwaysOpenDefaultImport ? null
            : Penumbra.Config.DefaultModImportPath.Length > 0                   ? Penumbra.Config.DefaultModImportPath
            : Penumbra.Config.ModDirectory.Length         > 0                   ? Penumbra.Config.ModDirectory : null;
        _hasSetFolder = true;

        _fileManager.OpenFileDialog( "Import Mod Pack",
            "Mod Packs{.ttmp,.ttmp2,.pmp},TexTools Mod Packs{.ttmp,.ttmp2},Penumbra Mod Packs{.pmp},Archives{.zip,.7z,.rar}", ( s, f ) =>
            {
                if( s )
                {
                    _import = new TexToolsImporter( Penumbra.ModManager.BasePath, f.Count, f.Select( file => new FileInfo( file ) ),
                        AddNewMod );
                    ImGui.OpenPopup( "Import Status" );
                }
            }, 0, modPath );
    }

    // Draw the progress information for import.
    private void DrawInfoPopup()
    {
        var display = ImGui.GetIO().DisplaySize;
        var height  = Math.Max( display.Y / 4, 15 * ImGui.GetFrameHeightWithSpacing() );
        var width   = display.X / 8;
        var size    = new Vector2( width * 2, height );
        ImGui.SetNextWindowPos( ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, Vector2.One / 2 );
        ImGui.SetNextWindowSize( size );
        using var popup = ImRaii.Popup( "Import Status", ImGuiWindowFlags.Modal );
        if( _import == null || !popup.Success )
        {
            return;
        }

        using( var child = ImRaii.Child( "##import", new Vector2( -1, size.Y - ImGui.GetFrameHeight() * 2 ) ) )
        {
            if( child )
            {
                _import.DrawProgressInfo( new Vector2( -1, ImGui.GetFrameHeight() ) );
            }
        }

        if( _import.State == ImporterState.Done && ImGui.Button( "Close", -Vector2.UnitX )
        || _import.State  != ImporterState.Done && _import.DrawCancelButton( -Vector2.UnitX ) )
        {
            _import?.Dispose();
            _import = null;
            ImGui.CloseCurrentPopup();
        }
    }

    // Mods need to be added thread-safely outside of iteration.
    private readonly ConcurrentQueue< DirectoryInfo > _modsToAdd = new();

    // Clean up invalid directory if necessary.
    // Add successfully extracted mods.
    private void AddNewMod( FileInfo file, DirectoryInfo? dir, Exception? error )
    {
        if( error != null )
        {
            if( dir != null && Directory.Exists( dir.FullName ) )
            {
                try
                {
                    Directory.Delete( dir.FullName, true );
                }
                catch( Exception e )
                {
                    Penumbra.Log.Error( $"Error cleaning up failed mod extraction of {file.FullName} to {dir.FullName}:\n{e}" );
                }
            }

            if( error is not OperationCanceledException )
            {
                Penumbra.Log.Error( $"Error extracting {file.FullName}, mod skipped:\n{error}" );
            }
        }
        else if( dir != null )
        {
            _modsToAdd.Enqueue( dir );
        }
    }

    private void DeleteModButton( Vector2 size )
    {
        var keys = Penumbra.Config.DeleteModModifier.IsActive();
        var tt = SelectedLeaf == null
            ? "No mod selected."
            : "Delete the currently selected mod entirely from your drive.\n"
          + "This can not be undone.";
        if( !keys )
        {
            tt += $"\nHold {Penumbra.Config.DeleteModModifier} while clicking to delete the mod.";
        }

        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), size, tt, SelectedLeaf == null || !keys, true )
        && Selected != null )
        {
            Penumbra.ModManager.DeleteMod( Selected.Index );
        }
    }

    private static void AddHelpButton( Vector2 size )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.QuestionCircle.ToIconString(), size, "Open extended help.", false, true ) )
        {
            ImGui.OpenPopup( "ExtendedHelp" );
        }

        ConfigWindow.OpenTutorial( ConfigWindow.BasicTutorialSteps.AdvancedHelp );
    }

    // Helpers.
    private static void SetDescendants( ModFileSystem.Folder folder, bool enabled, bool inherit = false )
    {
        var mods = folder.GetAllDescendants( ISortMode< Mod >.Lexicographical ).OfType< ModFileSystem.Leaf >().Select( l =>
        {
            // Any mod handled here should not stay new.
            Penumbra.ModManager.NewMods.Remove( l.Value );
            return l.Value;
        } );

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

    private void OnModDataChange( ModDataChangeType type, Mod mod, string? oldName )
    {
        switch( type )
        {
            case ModDataChangeType.Name:
            case ModDataChangeType.Author:
            case ModDataChangeType.ModTags:
            case ModDataChangeType.LocalTags:
            case ModDataChangeType.Favorite:
                SetFilterDirty();
                break;
        }
    }

    private void OnInheritanceChange( bool _ )
    {
        SetFilterDirty();
        OnSelectionChange( Selected, Selected, default );
    }

    private void OnCollectionChange( CollectionType collectionType, ModCollection? oldCollection, ModCollection? newCollection, string _ )
    {
        if( collectionType != CollectionType.Current || oldCollection == newCollection )
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
        _lastSelectedDirectory = Selected?.ModPath.FullName ?? string.Empty;
        ClearSelection();
    }

    private void RestoreLastSelection()
    {
        if( _lastSelectedDirectory.Length > 0 )
        {
            var leaf = ( ModFileSystem.Leaf? )FileSystem.Root.GetAllDescendants( ISortMode< Mod >.Lexicographical )
               .FirstOrDefault( l => l is ModFileSystem.Leaf m && m.Value.ModPath.FullName == _lastSelectedDirectory );
            Select( leaf );
            _lastSelectedDirectory = string.Empty;
        }
    }

    // If a default import folder is setup, try to move the given mod in there.
    // If the folder does not exist, create it if possible.
    private void MoveModToDefaultDirectory( Mod mod )
    {
        if( Penumbra.Config.DefaultImportFolder.Length == 0 )
        {
            return;
        }

        try
        {
            var leaf = FileSystem.Root.GetChildren( ISortMode< Mod >.Lexicographical )
               .FirstOrDefault( f => f is FileSystem< Mod >.Leaf l && l.Value == mod );
            if( leaf == null )
            {
                throw new Exception( "Mod was not found at root." );
            }

            var folder = FileSystem.FindOrCreateAllFolders( Penumbra.Config.DefaultImportFolder );
            FileSystem.Move( leaf, folder );
        }
        catch( Exception e )
        {
            Penumbra.Log.Warning(
                $"Could not move newly imported mod {mod.Name} to default import folder {Penumbra.Config.DefaultImportFolder}:\n{e}" );
        }
    }

    private static void DrawHelpPopup()
    {
        ImGuiUtil.HelpPopup( "ExtendedHelp", new Vector2( 1000 * ImGuiHelpers.GlobalScale, 34.5f * ImGui.GetTextLineHeightWithSpacing() ), () =>
        {
            ImGui.Dummy( Vector2.UnitY * ImGui.GetTextLineHeight() );
            ImGui.TextUnformatted( "Mod Management" );
            ImGui.BulletText( "You can create empty mods or import mods with the buttons in this row." );
            using var indent = ImRaii.PushIndent();
            ImGui.BulletText( "Supported formats for import are: .ttmp, .ttmp2, .pmp." );
            ImGui.BulletText( "You can also support .zip, .7z or .rar archives, but only if they already contain Penumbra-styled mods with appropriate metadata." );
            indent.Pop( 1 );
            ImGui.BulletText( "You can also create empty mod folders and delete mods." );
            ImGui.BulletText( "For further editing of mods, select them and use the Edit Mod tab in the panel or the Advanced Editing popup." );
            ImGui.Dummy( Vector2.UnitY * ImGui.GetTextLineHeight() );
            ImGui.TextUnformatted( "Mod Selector" );
            ImGui.BulletText( "Select a mod to obtain more information or change settings." );
            ImGui.BulletText( "Names are colored according to your config and their current state in the collection:" );
            indent.Push();
            ImGuiUtil.BulletTextColored( ColorId.EnabledMod.Value(), "enabled in the current collection." );
            ImGuiUtil.BulletTextColored( ColorId.DisabledMod.Value(), "disabled in the current collection." );
            ImGuiUtil.BulletTextColored( ColorId.InheritedMod.Value(), "enabled due to inheritance from another collection." );
            ImGuiUtil.BulletTextColored( ColorId.InheritedDisabledMod.Value(), "disabled due to inheritance from another collection." );
            ImGuiUtil.BulletTextColored( ColorId.UndefinedMod.Value(), "unconfigured in all inherited collections." );
            ImGuiUtil.BulletTextColored( ColorId.NewMod.Value(),
                "newly imported during this session. Will go away when first enabling a mod or when Penumbra is reloaded." );
            ImGuiUtil.BulletTextColored( ColorId.HandledConflictMod.Value(),
                "enabled and conflicting with another enabled Mod, but on different priorities (i.e. the conflict is solved)." );
            ImGuiUtil.BulletTextColored( ColorId.ConflictingMod.Value(),
                "enabled and conflicting with another enabled Mod on the same priority." );
            ImGuiUtil.BulletTextColored( ColorId.FolderExpanded.Value(), "expanded mod folder." );
            ImGuiUtil.BulletTextColored( ColorId.FolderCollapsed.Value(), "collapsed mod folder" );
            indent.Pop( 1 );
            ImGui.BulletText( "Right-click a mod to enter its sort order, which is its name by default, possibly with a duplicate number." );
            indent.Push();
            ImGui.BulletText( "A sort order differing from the mods name will not be displayed, it will just be used for ordering." );
            ImGui.BulletText(
                "If the sort order string contains Forward-Slashes ('/'), the preceding substring will be turned into folders automatically." );
            indent.Pop( 1 );
            ImGui.BulletText(
                "You can drag and drop mods and subfolders into existing folders. Dropping them onto mods is the same as dropping them onto the parent of the mod." );
            ImGui.BulletText( "Right-clicking a folder opens a context menu." );
            ImGui.BulletText( "Right-clicking empty space allows you to expand or collapse all folders at once." );
            ImGui.BulletText( "Use the Filter Mods... input at the top to filter the list for mods whose name or path contain the text." );
            indent.Push();
            ImGui.BulletText( "You can enter n:[string] to filter only for names, without path." );
            ImGui.BulletText( "You can enter c:[string] to filter for Changed Items instead." );
            ImGui.BulletText( "You can enter a:[string] to filter for Mod Authors instead." );
            indent.Pop( 1 );
            ImGui.BulletText( "Use the expandable menu beside the input to filter for mods fulfilling specific criteria." );
        } );
    }
}