using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dalamud.Logging;
using ImGuiNET;
using Penumbra.Importer;
using Penumbra.Mods;
using Penumbra.UI.Custom;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabImport
        {
            private const string LabelTab               = "Import Mods";
            private const string LabelImportButton      = "Import TexTools Modpacks";
            private const string LabelFileDialog        = "Pick one or more modpacks.";
            private const string LabelFileImportRunning = "Import in progress...";
            private const string FileTypeFilter         = "TexTools TTMP Modpack (*.ttmp2)|*.ttmp*|All files (*.*)|*.*";
            private const string TooltipModpack1        = "Writing modpack to disk before extracting...";

            private const uint ColorRed    = 0xFF0000C8;
            private const uint ColorYellow = 0xFF00C8C8;

            private static readonly Vector2 ImportBarSize = new( -1, 0 );

            private          bool              _isImportRunning;
            private          string            _errorMessage = string.Empty;
            private          TexToolsImport?   _texToolsImport;
            private readonly SettingsInterface _base;
            private readonly ModManager        _manager;

            public readonly HashSet< string > NewMods = new();

            public TabImport( SettingsInterface ui )
            {
                _base    = ui;
                _manager = Service< ModManager >.Get();
            }

            public bool IsImporting()
                => _isImportRunning;

            private void RunImportTask()
            {
                _isImportRunning = true;
                Task.Run( async () =>
                {
                    try
                    {
                        var picker = new OpenFileDialog
                        {
                            Multiselect     = true,
                            Filter          = FileTypeFilter,
                            CheckFileExists = true,
                            Title           = LabelFileDialog,
                        };

                        var result = await picker.ShowDialogAsync();

                        if( result == DialogResult.OK )
                        {
                            _errorMessage = string.Empty;

                            foreach( var fileName in picker.FileNames )
                            {
                                PluginLog.Information( $"-> {fileName} START" );

                                try
                                {
                                    _texToolsImport = new TexToolsImport( _manager.BasePath );
                                    var dir = _texToolsImport.ImportModPack( new FileInfo( fileName ) );
                                    if( dir.Name.Any() )
                                    {
                                        NewMods.Add( dir.Name );
                                    }

                                    PluginLog.Information( $"-> {fileName} OK!" );
                                }
                                catch( Exception ex )
                                {
                                    PluginLog.LogError( ex, "Failed to import modpack at {0}", fileName );
                                    _errorMessage = ex.Message;
                                }
                            }

                            var directory = _texToolsImport?.ExtractedDirectory;
                            _texToolsImport = null;
                            _base.ReloadMods();
                            if( directory != null )
                            {
                                _base._menu.InstalledTab.Selector.SelectModOnUpdate( directory.Name );
                            }
                        }
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Error opening file picker dialogue:\n{e}" );
                    }

                    _isImportRunning = false;
                } );
            }

            private void DrawImportButton()
            {
                if( !_manager.Valid )
                {
                    using var style = ImGuiRaii.PushStyle( ImGuiStyleVar.Alpha, 0.5f );
                    ImGui.Button( LabelImportButton );
                    style.Pop();

                    using var color = ImGuiRaii.PushColor( ImGuiCol.Text, ColorRed );
                    ImGui.Text( "Can not import since the mod directory path is not valid." );
                    ImGui.Dummy( Vector2.UnitY * ImGui.GetTextLineHeightWithSpacing() );
                    color.Pop();

                    ImGui.Text( "Please set the mod directory in the settings tab." );
                    ImGui.Text( "This folder should preferably be close to the root directory of your (preferably SSD) drive, for example" );
                    color.Push( ImGuiCol.Text, ColorYellow );
                    ImGui.Text( "        D:\\ffxivmods" );
                    color.Pop();
                    ImGui.Text( "You can return to this tab once you've done that." );
                }
                else if( ImGui.Button( LabelImportButton ) )
                {
                    RunImportTask();
                }
            }

            private void DrawImportProgress()
            {
                ImGui.Button( LabelFileImportRunning );

                if( _texToolsImport == null )
                {
                    return;
                }

                switch( _texToolsImport.State )
                {
                    case ImporterState.None: break;
                    case ImporterState.WritingPackToDisk:
                        ImGui.Text( TooltipModpack1 );
                        break;
                    case ImporterState.ExtractingModFiles:
                    {
                        var str =
                            $"{_texToolsImport.CurrentModPack} - {_texToolsImport.CurrentProgress} of {_texToolsImport.TotalProgress} files";

                        ImGui.ProgressBar( _texToolsImport.Progress, ImportBarSize, str );
                        break;
                    }
                    case ImporterState.Done: break;
                    default:                 throw new ArgumentOutOfRangeException();
                }
            }

            private void DrawFailedImportMessage()
            {
                using var color = ImGuiRaii.PushColor( ImGuiCol.Text, ColorRed );
                ImGui.Text( $"One or more of your modpacks failed to import:\n\t\t{_errorMessage}" );
            }

            public void Draw()
            {
                if( !ImGui.BeginTabItem( LabelTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                if( !_isImportRunning )
                {
                    DrawImportButton();
                }
                else
                {
                    DrawImportProgress();
                }

                if( _errorMessage.Any() )
                {
                    DrawFailedImportMessage();
                }
            }
        }
    }
}