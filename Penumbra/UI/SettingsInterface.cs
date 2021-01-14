using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using Newtonsoft.Json;
using Penumbra.Importer;
using Penumbra.Models;

namespace Penumbra.UI
{
    public class SettingsInterface
    {
        private readonly Plugin _plugin;

        public bool Visible = false;
        public bool ShowDebugBar = false;

        private static readonly Vector2 AutoFillSize = new Vector2( -1, -1 );
        private static readonly Vector2 ModListSize = new Vector2( 200, -1 );

        private static readonly Vector2 MinSettingsSize = new Vector2( 800, 450 );
        private static readonly Vector2 MaxSettingsSize = new Vector2( 69420, 42069 );

        private const string DialogDeleteMod = "PenumbraDeleteMod";

        private int _selectedModIndex;
        private int? _selectedModDeleteIndex;
        private ModInfo _selectedMod;

        public bool IsImportRunning = false;
        private TexToolsImport _texToolsImport = null!;

        public SettingsInterface( Plugin plugin )
        {
            _plugin = plugin;
#if DEBUG
            Visible = true;
            ShowDebugBar = true;
#endif
        }

        public void Draw()
        {
            if( ShowDebugBar && ImGui.BeginMainMenuBar() )
            {
                if( ImGui.BeginMenu( "Penumbra" ) )
                {
                    if( ImGui.MenuItem( "Toggle UI", "/penumbra", Visible ) )
                    {
                        Visible = !Visible;
                    }

                    if( ImGui.MenuItem( "Rediscover Mods" ) )
                    {
                        ReloadMods();
                    }

//                     ImGui.Separator();
// #if DEBUG
//                     ImGui.Text( _plugin.PluginDebugTitleStr );
// #else
//                     ImGui.Text( _plugin.Name );
// #endif

                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            if( !_plugin.PluginInterface.ClientState.Condition.Any() && !Visible )
            {
                // draw mods button on da menu :DDD
                var ss = ImGui.GetIO().DisplaySize;
                var padding = 50;
                var width = 200;
                var height = 45;

                // magic numbers
                ImGui.SetNextWindowPos( new Vector2( ss.X - padding - width, ss.Y - padding - height ), ImGuiCond.Always );

                if(
                    ImGui.Begin(
                        "Penumbra Menu Buttons",
                        ImGuiWindowFlags.AlwaysAutoResize |
                        ImGuiWindowFlags.NoBackground |
                        ImGuiWindowFlags.NoDecoration |
                        ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoScrollbar |
                        ImGuiWindowFlags.NoResize |
                        ImGuiWindowFlags.NoSavedSettings
                    )
                )
                {
                    if( ImGui.Button( "Manage Mods", new Vector2( width, height ) ) )
                    {
                        Visible = !Visible;
                    }

                    ImGui.End();
                }
            }

            if( !Visible )
            {
                return;
            }

            ImGui.SetNextWindowSizeConstraints( MinSettingsSize, MaxSettingsSize );
#if DEBUG
            var ret = ImGui.Begin( _plugin.PluginDebugTitleStr, ref Visible );
#else
            var ret = ImGui.Begin( _plugin.Name, ref Visible );
#endif
            if( !ret )
            {
                return;
            }

            ImGui.BeginTabBar( "PenumbraSettings" );

            DrawSettingsTab();
            DrawImportTab();


            if( !IsImportRunning )
            {
                DrawModBrowser();

                DrawInstalledMods();

                if( _plugin.Configuration.ShowAdvanced )
                {
                    DrawEffectiveFileList();
                }

                DrawDeleteModal();
            }

            ImGui.EndTabBar();

            ImGui.End();
        }

        void DrawImportTab()
        {
            var ret = ImGui.BeginTabItem( "Import Mods" );
            if( !ret )
            {
                return;
            }

            if( !IsImportRunning )
            {
                if( ImGui.Button( "Import TexTools Modpacks" ) )
                {
                    IsImportRunning = true;

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
                            foreach( var fileName in picker.FileNames )
                            {
                                PluginLog.Log( "-> {0} START", fileName );

                                try
                                {
                                    _texToolsImport = new TexToolsImport( new DirectoryInfo( _plugin.Configuration.CurrentCollection ) );
                                    _texToolsImport.ImportModPack( new FileInfo( fileName ) );
                                }
                                catch( Exception ex )
                                {
                                    PluginLog.LogError( ex, "Could not import one or more modpacks." );
                                }

                                PluginLog.Log( "-> {0} OK!", fileName );
                            }

                            _texToolsImport = null;
                            ReloadMods();
                        }

                        IsImportRunning = false;
                    } );
                }
            }
            else
            {
                ImGui.Button( "Import in progress..." );

                if( _texToolsImport != null )
                {
                    switch( _texToolsImport.State )
                    {
                        case ImporterState.None:
                            break;
                        case ImporterState.WritingPackToDisk:
                            ImGui.Text( "Writing modpack to disk before extracting..." );
                            break;
                        case ImporterState.ExtractingModFiles:
                        {
                            var str =
                                $"{_texToolsImport.CurrentModPack} - {_texToolsImport.CurrentProgress} of {_texToolsImport.TotalProgress} files";

                            ImGui.ProgressBar( _texToolsImport.Progress, new Vector2( -1, 0 ), str );
                            break;
                        }
                        case ImporterState.Done:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            ImGui.EndTabItem();
        }

        [Conditional( "DEBUG" )]
        void DrawModBrowser()
        {
            var ret = ImGui.BeginTabItem( "Available Mods" );
            if( !ret )
            {
                return;
            }

            ImGui.Text( "woah" );

            ImGui.EndTabItem();
        }

        void DrawSettingsTab()
        {
            var ret = ImGui.BeginTabItem( "Settings" );
            if( !ret )
            {
                return;
            }

            bool dirty = false;

            // FUCKKKKK
            var basePath = _plugin.Configuration.CurrentCollection;
            if( ImGui.InputText( "Root Folder", ref basePath, 255 ) )
            {
                _plugin.Configuration.CurrentCollection = basePath;
                dirty = true;
            }

            if( ImGui.Button( "Rediscover Mods" ) )
            {
                ReloadMods();
            }

            ImGui.SameLine();

            if( ImGui.Button( "Open Mods Folder" ) )
            {
                Process.Start( _plugin.Configuration.CurrentCollection );
            }

            ImGui.SetCursorPosY( ImGui.GetCursorPosY() + 15 );

            var showAdvanced = _plugin.Configuration.ShowAdvanced;
            if( ImGui.Checkbox( "Show Advanced Settings", ref showAdvanced ) )
            {
                _plugin.Configuration.ShowAdvanced = showAdvanced;
                dirty = true;
            }

            if( _plugin.Configuration.ShowAdvanced )
            {
                if( _plugin.ResourceLoader != null )
                {
                    ImGui.Checkbox( "Log all loaded files", ref _plugin.ResourceLoader.LogAllFiles );
                }

                var fswatch = _plugin.Configuration.DisableFileSystemNotifications;
                if( ImGui.Checkbox( "Disable filesystem change notifications", ref fswatch ) )
                {
                    _plugin.Configuration.DisableFileSystemNotifications = fswatch;
                    dirty = true;
                }

                var http = _plugin.Configuration.EnableHttpApi;
                if( ImGui.Checkbox( "Enable HTTP API", ref http ) )
                {
                    if( http )
                    {
                        _plugin.CreateWebServer();
                    }
                    else
                    {
                        _plugin.ShutdownWebServer();
                    }

                    _plugin.Configuration.EnableHttpApi = http;
                    dirty = true;
                }

                if( ImGui.Button( "Reload Player Resource" ) )
                {
                    _plugin.GameUtils.ReloadPlayerResources();
                }
            }

            if( dirty )
            {
                _plugin.Configuration.Save();
            }

            ImGui.EndTabItem();
        }

        void DrawModsSelector()
        {
            // Selector pane
            ImGui.BeginGroup();
            ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, new Vector2( 0, 0 ) );

            // Inlay selector list
            ImGui.BeginChild( "availableModList", new Vector2( 240, -ImGui.GetFrameHeightWithSpacing() ), true );

            if( _plugin.ModManager.Mods != null )
            {
                for( var modIndex = 0; modIndex < _plugin.ModManager.Mods.ModSettings.Count; modIndex++ )
                {
                    var settings = _plugin.ModManager.Mods.ModSettings[ modIndex ];

                    var changedColour = false;
                    if( !settings.Enabled )
                    {
                        ImGui.PushStyleColor( ImGuiCol.Text, 0xFF666666 );
                        changedColour = true;
                    }
                    else if( settings.Mod.FileConflicts.Any() )
                    {
                        ImGui.PushStyleColor( ImGuiCol.Text, 0xFFAAAAFF );
                        changedColour = true;
                    }

#if DEBUG
                    var selected = ImGui.Selectable(
                        $"id={modIndex} {settings.Mod.Meta.Name}",
                        modIndex == _selectedModIndex
                    );
#else
                    var selected = ImGui.Selectable( settings.Mod.Meta.Name, modIndex == _selectedModIndex );
#endif

                    if( changedColour )
                    {
                        ImGui.PopStyleColor();
                    }

                    if( selected )
                    {
                        _selectedModIndex = modIndex;
                        _selectedMod = settings;
                    }
                }
            }

            ImGui.EndChild();

            // Selector controls
            ImGui.PushStyleVar( ImGuiStyleVar.WindowPadding, new Vector2( 0, 0 ) );
            ImGui.PushStyleVar( ImGuiStyleVar.FrameRounding, 0 );
            ImGui.PushFont( UiBuilder.IconFont );
            if( _selectedModIndex != 0 )
            {
                if( ImGui.Button( FontAwesomeIcon.ArrowUp.ToIconString(), new Vector2( 60, 0 ) ) )
                {
                    _plugin.ModManager.ChangeModPriority( _selectedMod );
                    _selectedModIndex -= 1;
                }
            }
            else
            {
                ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.5f );
                ImGui.Button( FontAwesomeIcon.ArrowUp.ToIconString(), new Vector2( 60, 0 ) );
                ImGui.PopStyleVar();
            }

            ImGui.PopFont();

            if( ImGui.IsItemHovered() )
                ImGui.SetTooltip( "Move the selected mod up in priority" );

            ImGui.PushFont( UiBuilder.IconFont );

            ImGui.SameLine();

            if( _selectedModIndex != _plugin.ModManager.Mods?.ModSettings.Count - 1 )
            {
                if( ImGui.Button( FontAwesomeIcon.ArrowDown.ToIconString(), new Vector2( 60, 0 ) ) )
                {
                    _plugin.ModManager.ChangeModPriority( _selectedMod, true );
                    _selectedModIndex += 1;
                }
            }
            else
            {
                ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.5f );
                ImGui.Button( FontAwesomeIcon.ArrowDown.ToIconString(), new Vector2( 60, 0 ) );
                ImGui.PopStyleVar();
            }


            ImGui.PopFont();

            if( ImGui.IsItemHovered() )
                ImGui.SetTooltip( "Move the selected mod down in priority" );

            ImGui.PushFont( UiBuilder.IconFont );

            ImGui.SameLine();

            if( ImGui.Button( FontAwesomeIcon.Trash.ToIconString(), new Vector2( 60, 0 ) ) )
            {
                _selectedModDeleteIndex = _selectedModIndex;
            }

            ImGui.PopFont();

            if( ImGui.IsItemHovered() )
                ImGui.SetTooltip( "Delete the selected mod" );

            ImGui.PushFont( UiBuilder.IconFont );

            ImGui.SameLine();

            if( ImGui.Button( FontAwesomeIcon.Plus.ToIconString(), new Vector2( 60, 0 ) ) )
            {
            }

            ImGui.PopFont();

            if( ImGui.IsItemHovered() )
                ImGui.SetTooltip( "Add an empty mod" );

            ImGui.PopStyleVar( 3 );

            ImGui.EndGroup();
        }

        void DrawDeleteModal()
        {
            if( _selectedModDeleteIndex != null )
                ImGui.OpenPopup( DialogDeleteMod );

            var ret = ImGui.BeginPopupModal( DialogDeleteMod );
            if( !ret )
            {
                return;
            }

            if( _selectedMod?.Mod == null )
            {
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }

            ImGui.Text( "Are you sure you want to delete the following mod:" );
            // todo: why the fuck does this become null??????
            ImGui.Text( _selectedMod?.Mod?.Meta?.Name );

            if( ImGui.Button( "Yes, delete it" ) )
            {
                ImGui.CloseCurrentPopup();
                _plugin.ModManager.DeleteMod( _selectedMod.Mod );
                _selectedMod = null;
                _selectedModIndex = 0;
                _selectedModDeleteIndex = null;
            }

            ImGui.SameLine();

            if( ImGui.Button( "No, keep it" ) )
            {
                ImGui.CloseCurrentPopup();
                _selectedModDeleteIndex = null;
            }

            ImGui.EndPopup();
        }

        // Website button with On-Hover address if valid http(s), otherwise text.
        private void DrawWebsiteText()
        {
            if ((_selectedMod.Mod.Meta.Website?.Length ?? 0) > 0)
            {
                var validUrl = Uri.TryCreate(_selectedMod.Mod.Meta.Website, UriKind.Absolute, out Uri uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttps ||uriResult.Scheme == Uri.UriSchemeHttp);
                ImGui.SameLine();
                if (validUrl)
                {
                    if (ImGui.SmallButton("Open Website"))
                    { 
                        Process.Start( _selectedMod.Mod.Meta.Website );
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text( _selectedMod.Mod.Meta.Website );
                        ImGui.EndTooltip();
                    }
                }
                else
                {
                    ImGui.TextColored( new Vector4( 1f, 1f, 1f, 0.66f ), "from" );
                    ImGui.SameLine();
                    ImGui.Text(_selectedMod.Mod.Meta.Website);
                }
            }
        }

        // Create Mod-Handling buttons.
        private void DrawEditButtons()
        {
            ImGui.SameLine();
            if( ImGui.Button( "Open Mod Folder" ) )
            {
                Process.Start( _selectedMod.Mod.ModBasePath.FullName );
            }

            ImGui.SameLine();
            if( ImGui.Button( "Edit JSON" ) )
            {
                var metaPath = Path.Combine( _selectedMod.Mod.ModBasePath.FullName, "meta.json");
                File.WriteAllText( metaPath, JsonConvert.SerializeObject( _selectedMod.Mod.Meta, Formatting.Indented ) );
                Process.Start( metaPath );
            }

            ImGui.SameLine();
            if( ImGui.Button( "Reload JSON" ) )
            {
                ReloadMods();
                _selectedMod = _plugin.ModManager.Mods.ModSettings[ _selectedModIndex ];
            }
        }


        void DrawInstalledMods()
        {
            var ret = ImGui.BeginTabItem( "Installed Mods" );
            if( !ret )
            {
                return;
            }

            if( _plugin.ModManager.Mods == null )
            {
                ImGui.Text( "You don't have any mods :(" );
                ImGui.SetCursorPosY( ImGui.GetCursorPosY() + 20 );
                ImGui.Text( "You'll need to install them first by creating a folder close to the root of your drive (preferably an SSD)." );
                ImGui.Text( "For example: D:/ffxiv/mods/" );
                ImGui.Text( "And pasting that path into the settings tab and clicking the 'Rediscover Mods' button." );
                ImGui.Text( "You can return to this tab once you've done that." );
                ImGui.EndTabItem();
                return;
            }

            DrawModsSelector();

            ImGui.SameLine();

            if( _selectedMod != null )
            {
                try
                {
                    ImGui.BeginChild( "selectedModInfo", AutoFillSize, true );

                    ImGui.Text( _selectedMod.Mod.Meta.Name );
                    
                    // (Version ...) or nothing.
                    if ((_selectedMod.Mod.Meta.Version?.Length ?? 0) > 0)
                    {
                        ImGui.SameLine();
                        ImGui.Text($"(Version {_selectedMod.Mod.Meta.Version})" );
                    }

                    // by Author or Unknown.
                    ImGui.SameLine();
                    ImGui.TextColored( new Vector4( 1f, 1f, 1f, 0.66f ), "by" );
                    ImGui.SameLine();
                    if ((_selectedMod.Mod.Meta.Author?.Length ?? 0) > 0 )
                        ImGui.Text( _selectedMod.Mod.Meta.Author );
                    else
                        ImGui.Text( "Unknown" );
                    
                    DrawWebsiteText();

                    ImGui.SetCursorPosY( ImGui.GetCursorPosY() + 10 );

                    var enabled = _selectedMod.Enabled;
                    if( ImGui.Checkbox( "Enabled", ref enabled ) )
                    {
                        _selectedMod.Enabled = enabled;
                        _plugin.ModManager.Mods.Save();
                        _plugin.ModManager.CalculateEffectiveFileList();
                    }

                    DrawEditButtons();


                    ImGui.TextWrapped( _selectedMod.Mod.Meta.Description ?? "" );

                    ImGui.BeginTabBar( "PenumbraPluginDetails" );
                    if ( (_selectedMod.Mod.Meta.ChangedItems?.Count ?? 0 ) > 0)
                    {
                        if( ImGui.BeginTabItem( "Changed Items" ) )
                        {
                            ImGui.SetNextItemWidth( -1 );
                            if( ImGui.ListBoxHeader( "###",  AutoFillSize ) )
                                foreach(var item in _selectedMod.Mod.Meta.ChangedItems)
                                    ImGui.Selectable( item );
                            ImGui.ListBoxFooter();
                            ImGui.EndTabItem();
                        }
                    }

                    if( ImGui.BeginTabItem( "Files" ) )
                    {
                        ImGui.SetNextItemWidth( -1 );
                        if( ImGui.ListBoxHeader( "##", AutoFillSize ) )
                            foreach( var file in _selectedMod.Mod.ModFiles )
                                ImGui.Selectable( file.FullName );

                        ImGui.ListBoxFooter();
                        ImGui.EndTabItem();
                    }

                    if( _selectedMod.Mod.Meta.FileSwaps.Any() )
                    {
                        if( ImGui.BeginTabItem( "File Swaps" ) )
                        {
                            ImGui.SetNextItemWidth( -1 );
                            if( ImGui.ListBoxHeader( "##", AutoFillSize ) )
                            {
                                foreach( var file in _selectedMod.Mod.Meta.FileSwaps )
                                {
                                    // todo: fucking gross alloc every frame * items
                                    ImGui.Selectable( $"{file.Key} -> {file.Value}" );
                                }
                            }

                            ImGui.ListBoxFooter();
                            ImGui.EndTabItem();
                        }
                    }

                    if( _selectedMod.Mod.FileConflicts.Any() )
                    {
                        if( ImGui.BeginTabItem( "File Conflicts" ) )
                        {
                            ImGui.SetNextItemWidth( -1 );
                            if( ImGui.ListBoxHeader( "##", AutoFillSize ) )
                            {
                                foreach( var kv in _selectedMod.Mod.FileConflicts )
                                {
                                    var mod = kv.Key;
                                    var files = kv.Value;

                                    if( ImGui.Selectable( mod ) )
                                    {
                                        SelectModByName( mod );
                                    }

                                    ImGui.Indent( 15 );
                                    foreach( var file in files )
                                    {
                                        ImGui.Selectable( file );
                                    }

                                    ImGui.Unindent( 15 );
                                }
                            }

                            ImGui.ListBoxFooter();
                            ImGui.EndTabItem();
                        }
                    }

                    ImGui.EndTabBar();
                    ImGui.EndChild();
                }
                catch( Exception ex )
                {
                    PluginLog.LogError( ex, "fuck" );
                }
            }

            ImGui.EndTabItem();
        }

        void SelectModByName( string name )
        {
            for( var modIndex = 0; modIndex < _plugin.ModManager.Mods.ModSettings.Count; modIndex++ )
            {
                var mod = _plugin.ModManager.Mods.ModSettings[ modIndex ];

                if( mod.Mod.Meta.Name != name )
                {
                    continue;
                }

                _selectedMod = mod;
                _selectedModIndex = modIndex;
                return;
            }
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

            // create the directory if it doesn't exist
            Directory.CreateDirectory( _plugin.Configuration.CurrentCollection );

            _plugin.ModManager.DiscoverMods( _plugin.Configuration.CurrentCollection );
        }
    }
}