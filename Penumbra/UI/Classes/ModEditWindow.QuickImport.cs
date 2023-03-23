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
    private ResourceTreeViewer? _quickImportViewer;
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

        _quickImportViewer ??= new( "Import from Screen tab", 2, OnQuickImportRefresh, DrawQuickImportActions );
        _quickImportWritables ??= new();
        _quickImportActions ??= new();

        _quickImportViewer.Draw();

        _quickImportFileDialog.Draw();
    }

    private void OnQuickImportRefresh()
    {
        _quickImportWritables?.Clear();
        _quickImportActions?.Clear();
    }

    private void DrawQuickImportActions( ResourceTree.Node resourceNode, Vector2 buttonSize )
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
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Save.ToIconString(), buttonSize, "Export this file.", resourceNode.FullPath.FullName.Length == 0 || writable == null, true ) )
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
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.FileImport.ToIconString(), buttonSize, $"Add a copy of this file to {quickImport.OptionName}.", !quickImport.CanExecute, true ) )
        {
            quickImport.Execute();
            _quickImportActions.Remove( (resourceNode.GamePath, writable) );
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
