using ImGuiNET;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System;
using Penumbra.Importer;
using Dalamud.Plugin;
using System.Numerics;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabImport
        {
            private const string LabelTab               = "Import Mods";
            private const string LabelImportButton      = "Import TexTools Modpacks";
            private const string FileTypeFilter         = "TexTools TTMP Modpack (*.ttmp2)|*.ttmp*|All files (*.*)|*.*";
            private const string LabelFileDialog        = "Pick one or more modpacks.";
            private const string LabelFileImportRunning = "Import in progress...";
            private const string TooltipModpack1        = "Writing modpack to disk before extracting...";
            private const string FailedImport           = "One or more of your modpacks failed to import.\nPlease submit a bug report.";

            private const uint ColorRed                 = 0xFF0000C8;

            private static readonly Vector2 ImportBarSize = new( -1, 0 );

            private bool                       _isImportRunning = false;
            private bool                       _hasError = false;
            private TexToolsImport             _texToolsImport = null!;
            private readonly SettingsInterface _base;

            public TabImport(SettingsInterface ui) => _base = ui;

            public bool IsImporting() => _isImportRunning;

            private void RunImportTask()
            {
                _isImportRunning = true;
                Task.Run( async () =>
                {
                    var picker = new OpenFileDialog
                    {
                        Multiselect = true,
                        Filter = FileTypeFilter,
                        CheckFileExists = true,
                        Title = LabelFileDialog
                    };

                    var result = await picker.ShowDialogAsync();

                    if( result == DialogResult.OK )
                    {
                        _hasError = false;

                        foreach( var fileName in picker.FileNames )
                        {
                            PluginLog.Log( $"-> {fileName} START");

                            try
                            {
                                _texToolsImport = new TexToolsImport( new DirectoryInfo( _base._plugin.Configuration.CurrentCollection ) );
                                _texToolsImport.ImportModPack( new FileInfo( fileName ) );

                                PluginLog.Log( $"-> {fileName} OK!" );
                            }
                            catch( Exception ex )
                            {
                                PluginLog.LogError( ex, "Failed to import modpack at {0}", fileName );
                                _hasError = true;
                            }
                        }

                        _texToolsImport = null;
                        _base.ReloadMods();
                    }
                    _isImportRunning = false;
                } );
            }

            private void DrawImportButton()
            {
                if( ImGui.Button( LabelImportButton ) )
                {
                    RunImportTask();
                }
            }

            private void DrawImportProgress()
            {
                ImGui.Button( LabelFileImportRunning );

                if( _texToolsImport != null )
                {
                    switch( _texToolsImport.State )
                    {
                        case ImporterState.None:
                            break;
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
                        case ImporterState.Done:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            private void DrawFailedImportMessage()
            {
                ImGui.PushStyleColor( ImGuiCol.Text, ColorRed );
                ImGui.Text( FailedImport );
                ImGui.PopStyleColor();
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                    return;

                if( !_isImportRunning )
                    DrawImportButton();
                else
                    DrawImportProgress();

                if (_hasError)
                    DrawFailedImportMessage();

                ImGui.EndTabItem();
            }
        }
    }
}