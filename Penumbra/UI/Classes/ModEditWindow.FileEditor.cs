using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Files;
using Penumbra.Mods;
using Penumbra.String.Classes;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private class FileEditor< T > where T : class, IWritable
    {
        private readonly string                                           _tabName;
        private readonly string                                           _fileType;
        private readonly Func< IReadOnlyList< Mod.Editor.FileRegistry > > _getFiles;
        private readonly Func< T, bool, bool >                            _drawEdit;
        private readonly Func< string >                                   _getInitialPath;

        private Mod.Editor.FileRegistry? _currentPath;
        private T?                       _currentFile;
        private Exception?               _currentException;
        private bool                     _changed;

        private string     _defaultPath = string.Empty;
        private bool       _inInput;
        private T?         _defaultFile;
        private Exception? _defaultException;

        private IReadOnlyList< Mod.Editor.FileRegistry > _list = null!;

        private readonly FileDialogManager _fileDialog = ConfigWindow.SetupFileManager();

        public FileEditor( string tabName, string fileType, Func< IReadOnlyList< Mod.Editor.FileRegistry > > getFiles,
            Func< T, bool, bool > drawEdit, Func< string > getInitialPath )
        {
            _tabName        = tabName;
            _fileType       = fileType;
            _getFiles       = getFiles;
            _drawEdit       = drawEdit;
            _getInitialPath = getInitialPath;
        }

        public void Draw()
        {
            _list = _getFiles();
            using var tab = ImRaii.TabItem( _tabName );
            if( !tab )
            {
                return;
            }

            ImGui.NewLine();
            DrawFileSelectCombo();
            SaveButton();
            ImGui.SameLine();
            ResetButton();
            ImGui.SameLine();
            DefaultInput();
            ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );

            DrawFilePanel();
        }

        private void DefaultInput()
        {
            using var spacing = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { X = 3 * ImGuiHelpers.GlobalScale } );
            ImGui.SetNextItemWidth( ImGui.GetContentRegionAvail().X - 3 * ImGuiHelpers.GlobalScale - ImGui.GetFrameHeight() );
            ImGui.InputTextWithHint( "##defaultInput", "Input game path to compare...", ref _defaultPath, Utf8GamePath.MaxGamePathLength );
            _inInput = ImGui.IsItemActive();
            if( ImGui.IsItemDeactivatedAfterEdit() && _defaultPath.Length > 0 )
            {
                _fileDialog.Reset();
                try
                {
                    var file = Dalamud.GameData.GetFile( _defaultPath );
                    if( file != null )
                    {
                        _defaultException = null;
                        _defaultFile      = Activator.CreateInstance( typeof( T ), file.Data ) as T;
                    }
                    else
                    {
                        _defaultFile      = null;
                        _defaultException = new Exception( "File does not exist." );
                    }
                }
                catch( Exception e )
                {
                    _defaultFile      = null;
                    _defaultException = e;
                }
            }

            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Save.ToIconString(), new Vector2( ImGui.GetFrameHeight() ), "Export this file.", _defaultFile == null, true ) )
            {
                _fileDialog.SaveFileDialog( $"Export {_defaultPath} to...", _fileType, Path.GetFileNameWithoutExtension( _defaultPath ), _fileType, ( success, name ) =>
                {
                    if( !success )
                    {
                        return;
                    }

                    try
                    {
                        File.WriteAllBytes( name, _defaultFile?.Write() ?? throw new Exception( "File invalid." ) );
                    }
                    catch( Exception e )
                    {
                        Penumbra.Log.Error( $"Could not export {_defaultPath}:\n{e}" );
                    }
                }, _getInitialPath() );
            }

            _fileDialog.Draw();
        }

        public void Reset()
        {
            _currentException = null;
            _currentPath      = null;
            _currentFile      = null;
            _changed          = false;
        }

        private void DrawFileSelectCombo()
        {
            ImGui.SetNextItemWidth( ImGui.GetContentRegionAvail().X );
            using var combo = ImRaii.Combo( "##fileSelect", _currentPath?.RelPath.ToString() ?? $"Select {_fileType} File..." );
            if( !combo )
            {
                return;
            }

            foreach( var file in _list )
            {
                if( ImGui.Selectable( file.RelPath.ToString(), ReferenceEquals( file, _currentPath ) ) )
                {
                    UpdateCurrentFile( file );
                }

                if( ImGui.IsItemHovered() )
                {
                    using var tt = ImRaii.Tooltip();
                    ImGui.TextUnformatted( "All Game Paths" );
                    ImGui.Separator();
                    using var t = ImRaii.Table( "##Tooltip", 2, ImGuiTableFlags.SizingFixedFit );
                    foreach( var (option, gamePath) in file.SubModUsage )
                    {
                        ImGui.TableNextColumn();
                        ConfigWindow.Text( gamePath.Path );
                        ImGui.TableNextColumn();
                        using var color = ImRaii.PushColor( ImGuiCol.Text, ColorId.ItemId.Value() );
                        ImGui.TextUnformatted( option.FullName );
                    }
                }

                if( file.SubModUsage.Count > 0 )
                {
                    ImGui.SameLine();
                    using var color = ImRaii.PushColor( ImGuiCol.Text, ColorId.ItemId.Value() );
                    ImGuiUtil.RightAlign( file.SubModUsage[ 0 ].Item2.Path.ToString() );
                }
            }
        }

        private void UpdateCurrentFile( Mod.Editor.FileRegistry path )
        {
            if( ReferenceEquals( _currentPath, path ) )
            {
                return;
            }

            _changed          = false;
            _currentPath      = path;
            _currentException = null;
            try
            {
                var bytes = File.ReadAllBytes( _currentPath.File.FullName );
                _currentFile = Activator.CreateInstance( typeof( T ), bytes ) as T;
            }
            catch( Exception e )
            {
                _currentFile      = null;
                _currentException = e;
            }
        }

        private void SaveButton()
        {
            if( ImGuiUtil.DrawDisabledButton( "Save to File", Vector2.Zero,
                   $"Save the selected {_fileType} file with all changes applied. This is not revertible.", !_changed ) )
            {
                File.WriteAllBytes( _currentPath!.File.FullName, _currentFile!.Write() );
                _changed = false;
            }
        }

        private void ResetButton()
        {
            if( ImGuiUtil.DrawDisabledButton( "Reset Changes", Vector2.Zero,
                   $"Reset all changes made to the {_fileType} file.", !_changed ) )
            {
                var tmp = _currentPath;
                _currentPath = null;
                UpdateCurrentFile( tmp! );
            }
        }

        private void DrawFilePanel()
        {
            using var child = ImRaii.Child( "##filePanel", -Vector2.One, true );
            if( !child )
            {
                return;
            }

            if( _currentPath != null )
            {
                if( _currentFile == null )
                {
                    ImGui.TextUnformatted( $"Could not parse selected {_fileType} file." );
                    if( _currentException != null )
                    {
                        using var tab = ImRaii.PushIndent();
                        ImGuiUtil.TextWrapped( _currentException.ToString() );
                    }
                }
                else
                {
                    using var id = ImRaii.PushId( 0 );
                    _changed |= _drawEdit( _currentFile, false );
                }
            }

            if( !_inInput && _defaultPath.Length > 0 )
            {
                if( _currentPath != null )
                {
                    ImGui.NewLine();
                    ImGui.NewLine();
                    ImGui.TextUnformatted( $"Preview of {_defaultPath}:" );
                    ImGui.Separator();
                }

                if( _defaultFile == null )
                {
                    ImGui.TextUnformatted( $"Could not parse provided {_fileType} game file:\n" );
                    if( _defaultException != null )
                    {
                        using var tab = ImRaii.PushIndent();
                        ImGuiUtil.TextWrapped( _defaultException.ToString() );
                    }
                }
                else
                {
                    using var id = ImRaii.PushId( 1 );
                    _drawEdit( _defaultFile, true );
                }
            }
        }
    }
}