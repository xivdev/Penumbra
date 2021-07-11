using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Dalamud.Plugin;
using ImGuiNET;
using Penumbra.Interop;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabSettings
        {
            private const string LabelTab                  = "Settings";
            private const string LabelRootFolder           = "Root Folder";
            private const string LabelRediscoverButton     = "Rediscover Mods";
            private const string LabelOpenFolder           = "Open Mods Folder";
            private const string LabelEnabled              = "Enable Mods";
            private const string LabelEnabledPlayerWatch   = "Enable automatic Character Redraws";
            private const string LabelShowAdvanced         = "Show Advanced Settings";
            private const string LabelLogLoadedFiles       = "Log all loaded files";
            private const string LabelDisableNotifications = "Disable filesystem change notifications";
            private const string LabelEnableHttpApi        = "Enable HTTP API";
            private const string LabelReloadResource       = "Reload Player Resource";

            private readonly SettingsInterface _base;
            private readonly Configuration     _config;
            private          bool              _configChanged;

            public TabSettings( SettingsInterface ui )
            {
                _base          = ui;
                _config        = _base._plugin.Configuration!;
                _configChanged = false;
            }

            private void DrawRootFolder()
            {
                var basePath = _config.ModDirectory;
                if( ImGui.InputText( LabelRootFolder, ref basePath, 255, ImGuiInputTextFlags.EnterReturnsTrue )
                 && _config.ModDirectory != basePath )
                {
                    _config.ModDirectory = basePath;
                    _configChanged       = true;
                    _base.ReloadMods();
                    _base._menu.InstalledTab.Selector.ClearSelection();
                }
            }

            private void DrawRediscoverButton()
            {
                if( ImGui.Button( LabelRediscoverButton ) )
                {
                    _base.ReloadMods();
                    _base._menu.InstalledTab.Selector.ClearSelection();
                }
            }

            private void DrawOpenModsButton()
            {
                if( ImGui.Button( LabelOpenFolder ) )
                {
                    if( !Directory.Exists( _config.ModDirectory ) || !Service< ModManager >.Get().Valid )
                    {
                        return;
                    }

                    Process.Start( _config.ModDirectory );
                }
            }

            private void DrawEnabledBox()
            {
                var enabled = _config.IsEnabled;
                if( ImGui.Checkbox( LabelEnabled, ref enabled ) )
                {
                    _config.IsEnabled = enabled;
                    _configChanged    = true;
                    _base._plugin.ActorRefresher.RedrawAll( enabled ? Redraw.WithSettings : Redraw.WithoutSettings );
                    if( _config.EnableActorWatch )
                    {
                        _base._plugin.PlayerWatcher.SetActorWatch( enabled );
                    }
                }
            }

            private void DrawShowAdvancedBox()
            {
                var showAdvanced = _config.ShowAdvanced;
                if( ImGui.Checkbox( LabelShowAdvanced, ref showAdvanced ) )
                {
                    _config.ShowAdvanced = showAdvanced;
                    _configChanged       = true;
                }
            }

            private void DrawLogLoadedFilesBox()
            {
                ImGui.Checkbox( LabelLogLoadedFiles, ref _base._plugin.ResourceLoader.LogAllFiles );
                ImGui.SameLine();
                var regex = _base._plugin.ResourceLoader.LogFileFilter?.ToString() ?? string.Empty;
                var tmp   = regex;
                if( ImGui.InputTextWithHint( "##LogFilter", "Matching this Regex...", ref tmp, 64 ) && tmp != regex )
                {
                    try
                    {
                        var newRegex = tmp.Length > 0 ? new Regex( tmp, RegexOptions.Compiled ) : null;
                        _base._plugin.ResourceLoader.LogFileFilter = newRegex;
                    }
                    catch( Exception e )
                    {
                        PluginLog.Debug( "Could not create regex:\n{Exception}", e );
                    }
                }
            }

            private void DrawDisableNotificationsBox()
            {
                var fswatch = _config.DisableFileSystemNotifications;
                if( ImGui.Checkbox( LabelDisableNotifications, ref fswatch ) )
                {
                    _config.DisableFileSystemNotifications = fswatch;
                    _configChanged                         = true;
                }
            }

            private void DrawEnableHttpApiBox()
            {
                var http = _config.EnableHttpApi;
                if( ImGui.Checkbox( LabelEnableHttpApi, ref http ) )
                {
                    if( http )
                    {
                        _base._plugin.CreateWebServer();
                    }
                    else
                    {
                        _base._plugin.ShutdownWebServer();
                    }

                    _config.EnableHttpApi = http;
                    _configChanged        = true;
                }
            }

            private void DrawEnabledPlayerWatcher()
            {
                var enabled = _config.EnableActorWatch;
                if( ImGui.Checkbox( LabelEnabledPlayerWatch, ref enabled ) )
                {
                    _config.EnableActorWatch = enabled;
                    _configChanged           = true;
                    _base._plugin.PlayerWatcher.SetActorWatch( enabled );
                }
            }

            private static void DrawReloadResourceButton()
            {
                if( ImGui.Button( LabelReloadResource ) )
                {
                    Service< GameResourceManagement >.Get().ReloadPlayerResources();
                }
            }

            private void DrawAdvancedSettings()
            {
                DrawLogLoadedFilesBox();
                DrawDisableNotificationsBox();
                DrawEnableHttpApiBox();
                DrawReloadResourceButton();
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                {
                    return;
                }

                DrawRootFolder();

                DrawRediscoverButton();
                ImGui.SameLine();
                DrawOpenModsButton();

                Custom.ImGuiCustom.VerticalDistance( DefaultVerticalSpace );
                DrawEnabledBox();
                DrawEnabledPlayerWatcher();

                Custom.ImGuiCustom.VerticalDistance( DefaultVerticalSpace );
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

                ImGui.EndTabItem();
            }
        }
    }
}