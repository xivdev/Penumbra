using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Dalamud.Logging;
using ImGuiNET;
using Penumbra.GameData.Enums;
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
            private const string LabelTempFolder           = "Temporary Folder";
            private const string LabelRediscoverButton     = "Rediscover Mods";
            private const string LabelOpenFolder           = "Open Mods Folder";
            private const string LabelOpenTempFolder       = "Open Temporary Folder";
            private const string LabelEnabled              = "Enable Mods";
            private const string LabelEnabledPlayerWatch   = "Enable automatic Character Redraws";
            private const string LabelWaitFrames           = "Wait Frames";
            private const string LabelSortFoldersFirst     = "Sort Mod Folders Before Mods";
            private const string LabelScaleModSelector     = "Scale Mod Selector With Window Size";
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
                _config        = Penumbra.Config;
                _configChanged = false;
            }

            private void DrawRootFolder()
            {
                var basePath = _config.ModDirectory;
                if( ImGui.InputText( LabelRootFolder, ref basePath, 255, ImGuiInputTextFlags.EnterReturnsTrue )
                 && _config.ModDirectory != basePath )
                {
                    _base._menu.InstalledTab.Selector.ClearSelection();
                    _base._modManager.DiscoverMods( basePath );
                    _base._menu.InstalledTab.Selector.Cache.TriggerListReset();
                }
            }

            private void DrawTempFolder()
            {
                var tempPath = _config.TempDirectory;
                ImGui.SetNextItemWidth( 400 );
                if( ImGui.InputText( LabelTempFolder, ref tempPath, 255, ImGuiInputTextFlags.EnterReturnsTrue )
                 && _config.TempDirectory != tempPath )
                {
                    _base._modManager.SetTempDirectory( tempPath );
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( "The folder used to store temporary meta manipulation files.\n"
                      + "Leave this blank if you have no reason not to.\n"
                      + "A folder 'penumbrametatmp' will be created as a subdirectory to the specified directory.\n"
                      + "If none is specified (i.e. this is blank) this folder will be created in the root folder instead." );
                }

                ImGui.SameLine();
                if( ImGui.Button( LabelOpenTempFolder ) )
                {
                    if( !Directory.Exists( _base._modManager.TempPath.FullName ) || !_base._modManager.TempWritable )
                    {
                        return;
                    }

                    Process.Start( _base._modManager.TempPath.FullName );
                }
            }

            private void DrawRediscoverButton()
            {
                if( ImGui.Button( LabelRediscoverButton ) )
                {
                    _base._menu.InstalledTab.Selector.ClearSelection();
                    _base._modManager.DiscoverMods();
                    _base._menu.InstalledTab.Selector.Cache.TriggerListReset();
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
                    _base._penumbra.ObjectReloader.RedrawAll( enabled ? RedrawType.WithSettings : RedrawType.WithoutSettings );
                    if( _config.EnablePlayerWatch )
                    {
                        Penumbra.PlayerWatcher.SetStatus( enabled );
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

            private void DrawSortFoldersFirstBox()
            {
                var foldersFirst = _config.SortFoldersFirst;
                if( ImGui.Checkbox( LabelSortFoldersFirst, ref foldersFirst ) )
                {
                    _config.SortFoldersFirst = foldersFirst;
                    _base._menu.InstalledTab.Selector.Cache.TriggerListReset();
                    _configChanged = true;
                }
            }

            private void DrawScaleModSelectorBox()
            {
                var scaleModSelector = _config.ScaleModSelector;
                if( ImGui.Checkbox( LabelScaleModSelector, ref scaleModSelector ) )
                {
                    _config.ScaleModSelector = scaleModSelector;
                    _configChanged           = true;
                }
            }

            private void DrawLogLoadedFilesBox()
            {
                ImGui.Checkbox( LabelLogLoadedFiles, ref _base._penumbra.ResourceLoader.LogAllFiles );
                ImGui.SameLine();
                var regex = _base._penumbra.ResourceLoader.LogFileFilter?.ToString() ?? string.Empty;
                var tmp   = regex;
                if( ImGui.InputTextWithHint( "##LogFilter", "Matching this Regex...", ref tmp, 64 ) && tmp != regex )
                {
                    try
                    {
                        var newRegex = tmp.Length > 0 ? new Regex( tmp, RegexOptions.Compiled ) : null;
                        _base._penumbra.ResourceLoader.LogFileFilter = newRegex;
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
                        _base._penumbra.CreateWebServer();
                    }
                    else
                    {
                        _base._penumbra.ShutdownWebServer();
                    }

                    _config.EnableHttpApi = http;
                    _configChanged        = true;
                }
            }

            private void DrawEnabledPlayerWatcher()
            {
                var enabled = _config.EnablePlayerWatch;
                if( ImGui.Checkbox( LabelEnabledPlayerWatch, ref enabled ) )
                {
                    _config.EnablePlayerWatch = enabled;
                    _configChanged           = true;
                    Penumbra.PlayerWatcher.SetStatus( enabled );
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip(
                        "If this setting is enabled, penumbra will keep tabs on characters that have a corresponding collection setup in the Collections tab.\n"
                      + "Penumbra will try to automatically redraw those characters using their collection when they first appear in an instance, or when they change their current equip." );
                }

                if( _config.EnablePlayerWatch && _config.ShowAdvanced )
                {
                    var waitFrames = _config.WaitFrames;
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth( 50 );
                    if( ImGui.InputInt( LabelWaitFrames, ref waitFrames, 0, 0 )
                     && waitFrames != _config.WaitFrames
                     && waitFrames > 0
                     && waitFrames < 3000 )
                    {
                        _base._penumbra.ObjectReloader.DefaultWaitFrames = waitFrames;
                        _config.WaitFrames                               = waitFrames;
                        _configChanged                                   = true;
                    }

                    if( ImGui.IsItemHovered() )
                    {
                        ImGui.SetTooltip(
                            "The number of frames penumbra waits after some events (like zone changes) until it starts trying to redraw actors again, in a range of [1, 3001].\n"
                          + "Keep this as low as possible while producing stable results." );
                    }
                }
            }

            private static void DrawReloadResourceButton()
            {
                if( ImGui.Button( LabelReloadResource ) )
                {
                    Service< ResidentResources >.Get().ReloadPlayerResources();
                }
            }

            private void DrawAdvancedSettings()
            {
                DrawTempFolder();
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

                ImGui.EndTabItem();
            }
        }
    }
}