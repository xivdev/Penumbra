using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using Lumina.Data;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Files;
using Penumbra.Interop;
using Penumbra.Mods;
using Penumbra.String.Classes;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private ResourceTree[]? _quickImportTrees;
    private HashSet<ResourceTree.Node>? _quickImportUnfolded;
    private Dictionary<FullPath, IWritable?>? _quickImportWritables;
    private Dictionary<(Utf8GamePath, IWritable?), QuickImportAction>? _quickImportActions;

    private readonly FileDialogManager _quickImportFileDialog = ConfigWindow.SetupFileManager();

    private void DrawQuickImportTab()
    {
        using var tab = ImRaii.TabItem( "Import from Screen" );
        if( !tab )
        {
            _quickImportActions = null;
            return;
        }

        _quickImportUnfolded ??= new();
        _quickImportWritables ??= new();
        _quickImportActions ??= new();

        if( ImGui.Button( "Refresh Character List" ) )
        {
            try
            {
                _quickImportTrees = ResourceTree.FromObjectTable();
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not get character list for Import from Screen tab:\n{e}" );
                _quickImportTrees = Array.Empty<ResourceTree>();
            }
            _quickImportUnfolded.Clear();
            _quickImportWritables.Clear();
            _quickImportActions.Clear();
        }

        try
        {
            _quickImportTrees ??= ResourceTree.FromObjectTable();
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not get character list for Import from Screen tab:\n{e}" );
            _quickImportTrees ??= Array.Empty<ResourceTree>();
        }

        var textColorNonPlayer = ImGui.GetColorU32( ImGuiCol.Text );
        var textColorPlayer    = ( textColorNonPlayer & 0xFF000000u ) | ( ( textColorNonPlayer & 0x00FEFEFE ) >> 1 ) | 0x8000u; // Half green

        foreach( var (tree, index) in _quickImportTrees.WithIndex() )
        {
            using( var c = ImRaii.PushColor( ImGuiCol.Text, tree.PlayerRelated ? textColorPlayer : textColorNonPlayer ) )
            {
                if( !ImGui.CollapsingHeader( $"{tree.Name}##{index}", ( index == 0 ) ? ImGuiTreeNodeFlags.DefaultOpen : 0 ) )
                {
                    continue;
                }
            }
            using var id = ImRaii.PushId( index );

            ImGui.Text( $"Collection: {tree.CollectionName}" );

            using var table = ImRaii.Table( "##ResourceTree", 4,
                ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg );
            if( !table )
            {
                continue;
            }

            ImGui.TableSetupColumn( string.Empty , ImGuiTableColumnFlags.WidthStretch, 0.2f );
            ImGui.TableSetupColumn( "Game Path"  , ImGuiTableColumnFlags.WidthStretch, 0.3f );
            ImGui.TableSetupColumn( "Actual Path", ImGuiTableColumnFlags.WidthStretch, 0.5f );
            ImGui.TableSetupColumn( string.Empty , ImGuiTableColumnFlags.WidthFixed, 3 * ImGuiHelpers.GlobalScale + 2 * ImGui.GetFrameHeight() );
            ImGui.TableHeadersRow();

            DrawQuickImportNodes( tree.Nodes, 0 );
        }

        _quickImportFileDialog.Draw();
    }

    private void DrawQuickImportNodes( IEnumerable<ResourceTree.Node> resourceNodes, int level )
    {
        var debugMode = Penumbra.Config.DebugMode;
        var frameHeight = ImGui.GetFrameHeight();
        foreach( var (resourceNode, index) in resourceNodes.WithIndex() )
        {
            if( resourceNode.Internal && !debugMode )
            {
                continue;
            }
            using var id = ImRaii.PushId( index );
            ImGui.TableNextColumn();
            var unfolded = _quickImportUnfolded!.Contains( resourceNode );
            using( var indent = ImRaii.PushIndent( level ) )
            {
                ImGui.TableHeader( ( ( resourceNode.Children.Count > 0 ) ? ( unfolded ? "[-] " : "[+] " ) : string.Empty ) + resourceNode.Name );
                if( ImGui.IsItemClicked() && resourceNode.Children.Count > 0 )
                {
                    if( unfolded )
                    {
                        _quickImportUnfolded.Remove( resourceNode );
                    }
                    else
                    {
                        _quickImportUnfolded.Add( resourceNode );
                    }
                    unfolded = !unfolded;
                }
                if( debugMode )
                {
                    ImGuiUtil.HoverTooltip( $"Resource Type: {resourceNode.Type}\nSource Address: 0x{resourceNode.SourceAddress.ToString("X" + nint.Size * 2)}" );
                }
            }
            ImGui.TableNextColumn();
            var hasGamePaths = resourceNode.PossibleGamePaths.Length > 0;
            ImGui.Selectable( resourceNode.PossibleGamePaths.Length switch
            {
                0 => "(none)",
                1 => resourceNode.GamePath.ToString(),
                _ => "(multiple)",
            }, false, hasGamePaths ? 0 : ImGuiSelectableFlags.Disabled, new Vector2( ImGui.GetContentRegionAvail().X, frameHeight ) );
            if( hasGamePaths )
            {
                var allPaths = string.Join( '\n', resourceNode.PossibleGamePaths );
                if( ImGui.IsItemClicked() )
                {
                    ImGui.SetClipboardText( allPaths );
                }
                ImGuiUtil.HoverTooltip( $"{allPaths}\n\nClick to copy to clipboard." );
            }
            ImGui.TableNextColumn();
            var hasFullPath = resourceNode.FullPath.FullName.Length > 0;
            if( hasFullPath )
            {
                ImGui.Selectable( resourceNode.FullPath.ToString(), false, 0, new Vector2( ImGui.GetContentRegionAvail().X, frameHeight ) );
                if( ImGui.IsItemClicked() )
                {
                    ImGui.SetClipboardText( resourceNode.FullPath.ToString() );
                }
                ImGuiUtil.HoverTooltip( $"{resourceNode.FullPath}\n\nClick to copy to clipboard." );
            }
            else
            {
                ImGui.Selectable( "(unavailable)", false, ImGuiSelectableFlags.Disabled, new Vector2( ImGui.GetContentRegionAvail().X, frameHeight ) );
                ImGuiUtil.HoverTooltip( "The actual path to this file is unavailable.\nIt may be managed by another plug-in." );
            }
            ImGui.TableNextColumn();
            using( var spacing = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { X = 3 * ImGuiHelpers.GlobalScale } ) )
            {
                if( !_quickImportWritables!.TryGetValue( resourceNode.FullPath, out var writable ) )
                {
                    var path = resourceNode.FullPath.ToPath();
                    if( resourceNode.FullPath.IsRooted )
                    {
                        writable = new RawFileWritable( path );
                    }
                    else
                    {
                        var file = Dalamud.GameData.GetFile( path );
                        writable = ( file == null ) ? null : new RawGameFileWritable( file );
                    }
                    _quickImportWritables.Add( resourceNode.FullPath, writable );
                }
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Save.ToIconString(), new Vector2( frameHeight ), "Export this file.", !hasFullPath || writable == null, true ) )
                {
                    var fullPathStr = resourceNode.FullPath.FullName;
                    var ext = ( resourceNode.PossibleGamePaths.Length == 1 ) ? Path.GetExtension( resourceNode.GamePath.ToString() ) : Path.GetExtension( fullPathStr );
                    _quickImportFileDialog.SaveFileDialog( $"Export {Path.GetFileName( fullPathStr )} to...", ext, Path.GetFileNameWithoutExtension( fullPathStr ), ext, ( success, name ) =>
                    {
                        if( !success )
                        {
                            return;
                        }

                        try
                        {
                            File.WriteAllBytes( name, writable!.Write() );
                        }
                        catch( Exception e )
                        {
                            Penumbra.Log.Error( $"Could not export {fullPathStr}:\n{e}" );
                        }
                    } );
                }
                ImGui.SameLine();
                if( !_quickImportActions!.TryGetValue( (resourceNode.GamePath, writable), out var quickImport ) )
                {
                    quickImport = QuickImportAction.Prepare( this, resourceNode.GamePath, writable );
                    _quickImportActions.Add( (resourceNode.GamePath, writable), quickImport );
                }
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.FileImport.ToIconString(), new Vector2( frameHeight ), $"Add a copy of this file to {quickImport.OptionName}.", !quickImport.CanExecute, true ) )
                {
                    quickImport.Execute();
                    _quickImportActions.Remove( (resourceNode.GamePath, writable) );
                }
            }
            if( unfolded )
            {
                DrawQuickImportNodes( resourceNode.Children, level + 1 );
            }
        }
    }

    private record class RawFileWritable( string Path ) : IWritable
    {
        public bool Valid => true;

        public byte[] Write()
            => File.ReadAllBytes( Path );
    }

    private record class RawGameFileWritable( FileResource FileResource ) : IWritable
    {
        public bool Valid => true;

        public byte[] Write()
            => FileResource.Data;
    }

    private class QuickImportAction
    {
        public const string FallbackOptionName = "the current option";

        private readonly string       _optionName;
        private readonly Utf8GamePath _gamePath;
        private readonly Mod.Editor?  _editor;
        private readonly IWritable?   _file;
        private readonly string?      _targetPath;
        private readonly int          _subDirs;

        public string OptionName => _optionName;
        public Utf8GamePath GamePath => _gamePath;
        public bool CanExecute => !_gamePath.IsEmpty && _editor != null && _file != null && _targetPath != null;

        /// <summary>
        /// Creates a non-executable QuickImportAction.
        /// </summary>
        private QuickImportAction( string optionName, Utf8GamePath gamePath )
        {
            _optionName = optionName;
            _gamePath   = gamePath;
            _editor     = null;
            _file       = null;
            _targetPath = null;
            _subDirs    = 0;
        }

        /// <summary>
        /// Creates an executable QuickImportAction.
        /// </summary>
        private QuickImportAction( string optionName, Utf8GamePath gamePath, Mod.Editor editor, IWritable file, string targetPath, int subDirs )
        {
            _optionName = optionName;
            _gamePath   = gamePath;
            _editor     = editor;
            _file       = file;
            _targetPath = targetPath;
            _subDirs    = subDirs;
        }

        public static QuickImportAction Prepare( ModEditWindow owner, Utf8GamePath gamePath, IWritable? file )
        {
            var editor = owner._editor;
            if( editor == null )
            {
                return new QuickImportAction( FallbackOptionName, gamePath );
            }
            var subMod = editor.CurrentOption;
            var optionName = subMod.FullName;
            if( gamePath.IsEmpty || file == null || editor.FileChanges )
            {
                return new QuickImportAction( optionName, gamePath );
            }
            if( subMod.Files.ContainsKey( gamePath ) || subMod.FileSwaps.ContainsKey( gamePath ) )
            {
                return new QuickImportAction( optionName, gamePath );
            }
            var mod = owner._mod;
            if( mod == null )
            {
                return new QuickImportAction( optionName, gamePath );
            }
            var ( preferredPath, subDirs ) = GetPreferredPath( mod, subMod );
            var targetPath = new FullPath( Path.Combine( preferredPath.FullName, gamePath.ToString() ) ).FullName;
            if( File.Exists( targetPath ) )
            {
                return new QuickImportAction( optionName, gamePath );
            }

            return new QuickImportAction( optionName, gamePath, editor, file, targetPath, subDirs );
        }

        public Mod.Editor.FileRegistry Execute()
        {
            if( !CanExecute )
            {
                throw new InvalidOperationException();
            }
            var directory = Path.GetDirectoryName( _targetPath );
            if( directory != null )
            {
                Directory.CreateDirectory( directory );
            }
            File.WriteAllBytes( _targetPath!, _file!.Write() );
            _editor!.RevertFiles();
            var fileRegistry = _editor.AvailableFiles.First( file => file.File.FullName == _targetPath );
            _editor.AddPathsToSelected( new Mod.Editor.FileRegistry[] { fileRegistry }, _subDirs );
            _editor.ApplyFiles();

            return fileRegistry;
        }

        private static (DirectoryInfo, int) GetPreferredPath( Mod mod, ISubMod subMod )
        {
            var path = mod.ModPath;
            var subDirs = 0;
            if( subMod != mod.Default )
            {
                var name = subMod.Name;
                var fullName = subMod.FullName;
                if( fullName.EndsWith( ": " + name ) )
                {
                    path = Mod.Creator.NewOptionDirectory( path, fullName[..^( name.Length + 2 )] );
                    path = Mod.Creator.NewOptionDirectory( path, name );
                    subDirs = 2;
                }
                else
                {
                    path = Mod.Creator.NewOptionDirectory( path, fullName );
                    subDirs = 1;
                }
            }

            return (path, subDirs);
        }
    }
}
