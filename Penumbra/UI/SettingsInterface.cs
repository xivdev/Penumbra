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

        private bool _isImportRunning = false;

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

            if( !_isImportRunning )
            {
                DrawResourceMods();
                DrawEffectiveFileList();

                DrawDeleteModal();
            }

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
            var basePath = _plugin.Configuration.CurrentCollection;
            if( ImGui.InputText( "Root Folder", ref basePath, 255 ) )
            {
                _plugin.Configuration.CurrentCollection = basePath;
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

#if DEBUG

            ImGui.Text( "debug shit" );

            if( ImGui.Button( "Reload Player Resource" ) )
            {
                _plugin.ResourceLoader.ReloadPlayerResource();
            }

            if( _plugin.ResourceLoader != null )
            {
                ImGui.Checkbox( "DEBUG Log all loaded files", ref _plugin.ResourceLoader.LogAllFiles );
            }

            ImGui.SetCursorPosY( ImGui.GetCursorPosY() + 15 );
#endif

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
                                    new TexToolsImport( new DirectoryInfo( _plugin.Configuration.CurrentCollection ) );

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

            ImGui.SetCursorPosY( ImGui.GetCursorPosY() + 15 );

            if( ImGui.Button( "Save Settings" ) )
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

        void DrawResourceMods()
        {
            var ret = ImGui.BeginTabItem( "Mods" );
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
                    ImGui.SameLine();
                    ImGui.TextColored( new Vector4( 1f, 1f, 1f, 0.66f ), "by" );
                    ImGui.SameLine();
                    ImGui.Text( _selectedMod.Mod.Meta.Author );

                    ImGui.TextWrapped( _selectedMod.Mod.Meta.Description ?? "" );

                    ImGui.SetCursorPosY( ImGui.GetCursorPosY() + 10 );

                    var enabled = _selectedMod.Enabled;
                    if( ImGui.Checkbox( "Enabled", ref enabled ) )
                    {
                        _selectedMod.Enabled = enabled;
                        _plugin.ModManager.Mods.Save();
                        _plugin.ModManager.CalculateEffectiveFileList();
                    }

                    if( ImGui.Button( "Open Mod Folder" ) )
                    {
                        Process.Start( _selectedMod.Mod.ModBasePath.FullName );
                    }

                    ImGui.BeginTabBar( "PenumbraPluginDetails" );
                    if( ImGui.BeginTabItem( "Files" ) )
                    {
                        ImGui.SetNextItemWidth( -1 );
                        if( ImGui.ListBoxHeader( "##", AutoFillSize ) )
                        {
                            foreach( var file in _selectedMod.Mod.ModFiles )
                            {
                                ImGui.Selectable( file.FullName );
                            }
                        }

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