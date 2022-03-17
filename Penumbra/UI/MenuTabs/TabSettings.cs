using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Logging;
using ImGuiNET;
using Penumbra.GameData.ByteString;
using Penumbra.Interop;
using Penumbra.Mods;
using Penumbra.UI.Custom;
using Penumbra.Util;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private class TabSettings
    {
        private readonly SettingsInterface _base;
        private readonly Configuration     _config;
        private          bool              _configChanged;
        private          string            _newModDirectory;

        public TabSettings( SettingsInterface ui )
        {
            _base            = ui;
            _config          = Penumbra.Config;
            _configChanged   = false;
            _newModDirectory = _config.ModDirectory;
        }

        private static bool DrawPressEnterWarning( string old )
        {
            const uint red   = 0xFF202080;
            using var  color = ImGuiRaii.PushColor( ImGuiCol.Button, red );
            var        w     = Vector2.UnitX * ImGui.CalcItemWidth();
            return ImGui.Button( $"Press Enter or Click Here to Save (Current Directory: {old})", w );
        }

        private static void DrawOpenDirectoryButton( int id, DirectoryInfo directory, bool condition )
        {
            ImGui.PushID( id );
            var ret = ImGui.Button( "Open Directory" );
            ImGuiCustom.HoverTooltip( "Open this directory in your configured file explorer." );
            if( ret && condition && Directory.Exists( directory.FullName ) )
            {
                Process.Start( new ProcessStartInfo( directory.FullName )
                {
                    UseShellExecute = true,
                } );
            }

            ImGui.PopID();
        }

        private void DrawRootFolder()
        {
            ImGui.BeginGroup();
            ImGui.SetNextItemWidth( SettingsMenu.InputTextWidth );
            var save = ImGui.InputText( "Root Directory", ref _newModDirectory, 255, ImGuiInputTextFlags.EnterReturnsTrue );
            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "This is where Penumbra will store your extracted mod files.\n"
              + "TTMP files are not copied, just extracted.\n"
              + "This directory needs to be accessible and you need write access here.\n"
              + "It is recommended that this directory is placed on a fast hard drive, preferably an SSD.\n"
              + "It should also be placed near the root of a logical drive - the shorter the total path to this folder, the better.\n"
              + "Definitely do not place it in your Dalamud directory or any sub-directory thereof." );
            ImGui.SameLine();
            var modManager = Penumbra.ModManager;
            DrawOpenDirectoryButton( 0, modManager.BasePath, modManager.Valid );
            ImGui.EndGroup();

            if( _config.ModDirectory == _newModDirectory || !_newModDirectory.Any() )
            {
                return;
            }

            if( save || DrawPressEnterWarning( _config.ModDirectory ) )
            {
                _base._menu.InstalledTab.Selector.ClearSelection();
                modManager.DiscoverMods( _newModDirectory );
                _base._menu.InstalledTab.Selector.Cache.TriggerListReset();
                _newModDirectory = _config.ModDirectory;
            }
        }

        private void DrawRediscoverButton()
        {
            if( ImGui.Button( "Rediscover Mods" ) )
            {
                _base._menu.InstalledTab.Selector.ClearSelection();
                Penumbra.ModManager.DiscoverMods();
                _base._menu.InstalledTab.Selector.Cache.TriggerListReset();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "Force Penumbra to completely re-scan your root directory as if it was restarted." );
        }

        private void DrawEnabledBox()
        {
            var enabled = _config.EnableMods;
            if( ImGui.Checkbox( "Enable Mods", ref enabled ) )
            {
                _base._penumbra.SetEnabled( enabled );
            }
        }

        private void DrawShowAdvancedBox()
        {
            var showAdvanced = _config.ShowAdvanced;
            if( ImGui.Checkbox( "Show Advanced Settings", ref showAdvanced ) )
            {
                _config.ShowAdvanced = showAdvanced;
                _configChanged       = true;
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "Enable some advanced options in this window and in the mod selector.\n"
              + "This is required to enable manually editing any mod information." );
        }

        private void DrawSortFoldersFirstBox()
        {
            var foldersFirst = _config.SortFoldersFirst;
            if( ImGui.Checkbox( "Sort Mod-Folders Before Mods", ref foldersFirst ) )
            {
                _config.SortFoldersFirst = foldersFirst;
                _base._menu.InstalledTab.Selector.Cache.TriggerListReset();
                _configChanged = true;
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "Prioritizes all mod-folders in the mod-selector in the Installed Mods tab so that folders come before single mods, instead of being sorted completely alphabetically" );
        }

        private void DrawScaleModSelectorBox()
        {
            var scaleModSelector = _config.ScaleModSelector;
            if( ImGui.Checkbox( "Scale Mod Selector With Window Size", ref scaleModSelector ) )
            {
                _config.ScaleModSelector = scaleModSelector;
                _configChanged           = true;
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "Instead of keeping the mod-selector in the Installed Mods tab a fixed width, this will let it scale with the total size of the Penumbra window." );
        }

        private void DrawDisableSoundStreamingBox()
        {
            var tmp = Penumbra.Config.DisableSoundStreaming;
            if( ImGui.Checkbox( "Disable Audio Streaming", ref tmp ) && tmp != Penumbra.Config.DisableSoundStreaming )
            {
                Penumbra.Config.DisableSoundStreaming = tmp;
                _configChanged                        = true;
                if( tmp )
                {
                    _base._penumbra.MusicManager.DisableStreaming();
                }
                else
                {
                    _base._penumbra.MusicManager.EnableStreaming();
                }

                _base.ReloadMods();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "Disable streaming in the games audio engine.\n"
              + "If you do not disable streaming, you can not replace sound files in the game (*.scd files), they will be ignored by Penumbra.\n\n"
              + "Only touch this if you experience sound problems.\n"
              + "If you toggle this, make sure no modified or to-be-modified sound file is currently playing or was recently playing, else you might crash." );
        }

        private void DrawLogLoadedFilesBox()
        {
            //ImGui.Checkbox( "Log Loaded Files", ref _base._penumbra.ResourceLoader.LogAllFiles );
            //ImGui.SameLine();
            //var regex = _base._penumbra.ResourceLoader.LogFileFilter?.ToString() ?? string.Empty;
            //var tmp   = regex;
            //ImGui.SetNextItemWidth( SettingsMenu.InputTextWidth );
            //if( ImGui.InputTextWithHint( "##LogFilter", "Matching this Regex...", ref tmp, 64 ) && tmp != regex )
            //{
            //    try
            //    {
            //        var newRegex = tmp.Length > 0 ? new Regex( tmp, RegexOptions.Compiled ) : null;
            //        _base._penumbra.ResourceLoader.LogFileFilter = newRegex;
            //    }
            //    catch( Exception e )
            //    {
            //        PluginLog.Debug( "Could not create regex:\n{Exception}", e );
            //    }
            //}
            //
            //ImGui.SameLine();
            //ImGuiComponents.HelpMarker( "Log all loaded files that match the given Regex to the PluginLog." );
        }

        private void DrawDisableNotificationsBox()
        {
            var fsWatch = _config.DisableFileSystemNotifications;
            if( ImGui.Checkbox( "Disable Filesystem Change Notifications", ref fsWatch ) )
            {
                _config.DisableFileSystemNotifications = fsWatch;
                _configChanged                         = true;
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "Currently does nothing." );
        }

        private void DrawEnableHttpApiBox()
        {
            var http = _config.EnableHttpApi;
            if( ImGui.Checkbox( "Enable HTTP API", ref http ) )
            {
                if( http )
                {
                    _base._penumbra.CreateWebServer();
                }
                else
                {
                    _base._penumbra.ShutdownWebServer();
                }

                _config.EnableHttpApi = http;
                _configChanged        = true;
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "Enables other applications, e.g. Anamnesis, to use some Penumbra functions, like requesting redraws." );
        }

        private static void DrawReloadResourceButton()
        {
            if( ImGui.Button( "Reload Resident Resources" ) )
            {
                Penumbra.ResidentResources.Reload();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "Reload some specific files that the game keeps in memory at all times.\n"
              + "You usually should not need to do this." );
        }

        private void DrawEnableFullResourceLoggingBox()
        {
            var tmp = _config.EnableFullResourceLogging;
            if( ImGui.Checkbox( "Enable Full Resource Logging", ref tmp ) && tmp != _config.EnableFullResourceLogging )
            {
                if( tmp )
                {
                    Penumbra.ResourceLoader.EnableFullLogging();
                }
                else
                {
                    Penumbra.ResourceLoader.DisableFullLogging();
                }

                _config.EnableFullResourceLogging = tmp;
                _configChanged                    = true;
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "[DEBUG] Enable the logging of all ResourceLoader events indiscriminately." );
        }

        private void DrawEnableDebugModeBox()
        {
            var tmp = _config.DebugMode;
            if( ImGui.Checkbox( "Enable Debug Mode", ref tmp ) && tmp != _config.DebugMode )
            {
                if( tmp )
                {
                    Penumbra.ResourceLoader.EnableDebug();
                }
                else
                {
                    Penumbra.ResourceLoader.DisableDebug();
                }

                _config.DebugMode = tmp;
                _configChanged    = true;
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "[DEBUG] Enable the Debug Tab and Resource Manager Tab as well as some additional data collection." );
        }

        private void DrawRequestedResourceLogging()
        {
            var tmp = _config.EnableResourceLogging;
            if( ImGui.Checkbox( "Enable Requested Resource Logging", ref tmp ) )
            {
                _base._penumbra.ResourceLogger.SetState( tmp );
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker( "Log all game paths FFXIV requests to the plugin log.\n"
              + "You can filter the logged paths for those containing the entered string or matching the regex, if the entered string compiles to a valid regex.\n"
              + "Red boundary indicates invalid regex." );
            ImGui.SameLine();
            var       tmpString = Penumbra.Config.ResourceLoggingFilter;
            using var color     = ImGuiRaii.PushColor( ImGuiCol.Border, 0xFF0000B0, !_base._penumbra.ResourceLogger.ValidRegex );
            using var style = ImGuiRaii.PushStyle( ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale,
                !_base._penumbra.ResourceLogger.ValidRegex );
            if( ImGui.InputTextWithHint( "##ResourceLogFilter", "Filter...", ref tmpString, Utf8GamePath.MaxGamePathLength ) )
            {
                _base._penumbra.ResourceLogger.SetFilter( tmpString );
            }
        }

        private void DrawAdvancedSettings()
        {
            DrawRequestedResourceLogging();
            DrawDisableSoundStreamingBox();
            DrawLogLoadedFilesBox();
            DrawDisableNotificationsBox();
            DrawEnableHttpApiBox();
            DrawReloadResourceButton();
            DrawEnableDebugModeBox();
            DrawEnableFullResourceLoggingBox();
        }

        public static unsafe void Text( Utf8String s )
        {
            ImGuiNative.igTextUnformatted( ( byte* )s.Path, ( byte* )s.Path + s.Length );
        }

        public void Draw()
        {
            if( !ImGui.BeginTabItem( "Settings" ) )
            {
                return;
            }

            using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

            DrawRootFolder();

            DrawRediscoverButton();

            ImGuiCustom.VerticalDistance( DefaultVerticalSpace );
            DrawEnabledBox();

            ImGuiCustom.VerticalDistance( DefaultVerticalSpace );
            DrawScaleModSelectorBox();
            DrawSortFoldersFirstBox();
            DrawShowAdvancedBox();

            if( _config.ShowAdvanced )
            {
                DrawAdvancedSettings();
            }

            if( _configChanged )
            {
                _config.Save();
                _configChanged = false;
            }
        }
    }
}