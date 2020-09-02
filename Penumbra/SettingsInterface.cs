using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using Penumbra.Importer;

namespace Penumbra
{
    public class SettingsInterface
    {
        private readonly Plugin _plugin;

        public bool Visible { get; set; } = true;

        private static readonly Vector2 AutoFillSize = new Vector2( -1, -1 );
        private static readonly Vector2 ModListSize = new Vector2( 200, -1 );

        private static readonly Vector2 MinSettingsSize = new Vector2( 650, 450 );
        private static readonly Vector2 MaxSettingsSize = new Vector2( 69420, 42069 );

        private int _selectedModIndex;
        private ResourceMod _selectedMod;

        private bool _isImportRunning = false;

        public SettingsInterface( Plugin plugin )
        {
            _plugin = plugin;
        }

        public void Draw()
        {
            ImGui.SetNextWindowSizeConstraints( MinSettingsSize, MaxSettingsSize );
            var ret = ImGui.Begin( _plugin.Name );
            if( !ret )
            {
                return;
            }

            ImGui.BeginTabBar( "PenumbraSettings" );

            DrawSettingsTab();
            DrawResourceMods();
            DrawEffectiveFileList();

            ImGui.EndTabBar();

            ImGui.End();
        }

        void DrawSettingsTab()
        {
            var ret = ImGui.BeginTabItem( "Settings" );
            if( !ret )
            {
                return;
            }

            // FUCKKKKK
            var basePath = _plugin.Configuration.BaseFolder;
            if( ImGui.InputText( "Root Folder", ref basePath, 255 ) )
            {
                _plugin.Configuration.BaseFolder = basePath;
            }

            if( ImGui.Button( "Rediscover Mods" ) )
            {
                ReloadMods();
            }

            if( !_isImportRunning )
            {
                if( ImGui.Button( "Import TexTools Modpacks" ) )
                {
                    _isImportRunning = true;

                    Task.Run( async () =>
                    {
                        var picker = new OpenFileDialog
                        {
                            Multiselect = true,
                            Filter = "TexTools TTMP Modpack (*.ttmp2)|*.ttmp*|All files (*.*)|*.*",
                            CheckFileExists = true,
                            Title = "Pick one or more modpacks."
                        };

                        var result = await picker.ShowDialogAsync();

                        if( result == DialogResult.OK )
                        {
                            try
                            {
                                var importer =
                                    new TexToolsImport( new DirectoryInfo( _plugin.Configuration.BaseFolder ) );

                                foreach( var fileName in picker.FileNames )
                                {
                                    PluginLog.Log( "-> {0} START", fileName );

                                    importer.ImportModPack( new FileInfo( fileName ) );

                                    PluginLog.Log( "-> {0} OK!", fileName );
                                }

                                ReloadMods();
                            }
                            catch( Exception ex )
                            {
                                PluginLog.LogError( ex, "Could not import one or more modpacks." );
                            }
                        }

                        _isImportRunning = false;
                    } );
                }
            }
            else
            {
                ImGui.Button( "Import in progress..." );
            }

            if( ImGui.Button( "Save Settings" ) )
                _plugin.Configuration.Save();

            ImGui.EndTabItem();
        }

        void DrawModsSelector()
        {
            // Selector pane
            ImGui.BeginGroup();
            ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, new Vector2( 0, 0 ) );

            // Inlay selector list
            ImGui.BeginChild( "availableModList", new Vector2( 180, -ImGui.GetFrameHeightWithSpacing() ), true );

            for( var modIndex = 0; modIndex < _plugin.ModManager.AvailableMods.Count; modIndex++ )
            {
                var mod = _plugin.ModManager.AvailableMods.ElementAt( modIndex );

                if( ImGui.Selectable( mod.Value.Meta.Name, modIndex == _selectedModIndex ) )
                {
                    _selectedModIndex = modIndex;
                    _selectedMod = mod.Value;
                }
            }

            ImGui.EndChild();

            // Selector controls
            ImGui.PushStyleVar( ImGuiStyleVar.WindowPadding, new Vector2( 0, 0 ) );
            ImGui.PushStyleVar( ImGuiStyleVar.FrameRounding, 0 );
            ImGui.PushFont( UiBuilder.IconFont );
            if( _selectedModIndex != 0 )
            {
                if( ImGui.Button( FontAwesomeIcon.ArrowUp.ToIconString(), new Vector2( 45, 0 ) ) )
                {
                }
            }
            else
            {
                ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.5f );
                ImGui.Button( FontAwesomeIcon.ArrowUp.ToIconString(), new Vector2( 45, 0 ) );
                ImGui.PopStyleVar();
            }

            ImGui.PopFont();

            if( ImGui.IsItemHovered() )
                ImGui.SetTooltip( "Move the selected mod up in priority" );

            ImGui.PushFont( UiBuilder.IconFont );

            ImGui.SameLine();

            if( _selectedModIndex != _plugin.ModManager.AvailableMods.Count - 1 )
            {
                if( ImGui.Button( FontAwesomeIcon.ArrowDown.ToIconString(), new Vector2( 45, 0 ) ) )
                {
                }
            }
            else
            {
                ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.5f );
                ImGui.Button( FontAwesomeIcon.ArrowDown.ToIconString(), new Vector2( 45, 0 ) );
                ImGui.PopStyleVar();
            }


            ImGui.PopFont();

            if( ImGui.IsItemHovered() )
                ImGui.SetTooltip( "Move the selected mod down in priority" );

            ImGui.PushFont( UiBuilder.IconFont );

            ImGui.SameLine();

            if( ImGui.Button( FontAwesomeIcon.Trash.ToIconString(), new Vector2( 45, 0 ) ) )
            {
            }

            ImGui.PopFont();

            if( ImGui.IsItemHovered() )
                ImGui.SetTooltip( "Delete the selected mod" );

            ImGui.PushFont( UiBuilder.IconFont );

            ImGui.SameLine();

            if( ImGui.Button( FontAwesomeIcon.Plus.ToIconString(), new Vector2( 45, 0 ) ) )
            {
            }

            ImGui.PopFont();

            if( ImGui.IsItemHovered() )
                ImGui.SetTooltip( "Add an empty mod" );

            ImGui.PopStyleVar( 3 );

            ImGui.EndGroup();
        }

        void DrawResourceMods()
        {
            var ret = ImGui.BeginTabItem( "Resource Mods" );
            if( !ret )
            {
                return;
            }

            DrawModsSelector();

            ImGui.SameLine();

            if( _selectedMod != null )
            {
                try
                {
                    ImGui.BeginChild( "selectedModInfo", AutoFillSize, true );

                    ImGui.Text( _selectedMod.Meta.Name );
                    ImGui.SameLine();
                    ImGui.TextColored( new Vector4( 1f, 1f, 1f, 0.66f ), "by" );
                    ImGui.SameLine();
                    ImGui.Text( _selectedMod.Meta.Author );

                    ImGui.TextWrapped( _selectedMod.Meta.Description ?? "" );

                    ImGui.SetCursorPosY( ImGui.GetCursorPosY() + 12 );

                    // list files
                    ImGui.Text( "Files:" );
                    ImGui.SetNextItemWidth( -1 );
                    if( ImGui.ListBoxHeader( "##", AutoFillSize ) )
                    {
                        foreach( var file in _selectedMod.ModFiles )
                        {
                            ImGui.Selectable( file.FullName );
                        }
                    }

                    ImGui.ListBoxFooter();

                    ImGui.EndChild();
                }
                catch( Exception ex )
                {
                    PluginLog.LogError( ex, "fuck" );
                }
            }

            ImGui.EndTabItem();
        }

        void DrawEffectiveFileList()
        {
            var ret = ImGui.BeginTabItem( "Effective File List" );
            if( !ret )
            {
                return;
            }

            if( ImGui.ListBoxHeader( "##", AutoFillSize ) )
            {
                // todo: virtualise this
                foreach( var file in _plugin.ModManager.ResolvedFiles )
                {
                    ImGui.Selectable( file.Value.FullName );
                }
            }

            ImGui.ListBoxFooter();

            ImGui.EndTabItem();
        }

        private void ReloadMods()
        {
            _selectedMod = null;

            // haha yikes
            _plugin.ModManager = new ModManager( new DirectoryInfo( _plugin.Configuration.BaseFolder ) );
            _plugin.ModManager.DiscoverMods();
        }
    }
}