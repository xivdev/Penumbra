using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using Penumbra.Interop;
using Penumbra.Mods;
using Penumbra.UI.Custom;
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
            private const string LabelManageModsOffset     = "\"Manage mods\" title screen button offset";

            private readonly SettingsInterface _base;
            private readonly Configuration     _config;
            private          bool              _configChanged;
            private          string            _newModDirectory;
            private          string            _newTempDirectory;


            public TabSettings( SettingsInterface ui )
            {
                _base             = ui;
                _config           = Penumbra.Config;
                _configChanged    = false;
                _newModDirectory  = _config.ModDirectory;
                _newTempDirectory = _config.TempDirectory;
            }

            private static bool DrawPressEnterWarning( string old, float? width = null )
            {
                const uint red   = 0xFF202080;
                using var  color = ImGuiRaii.PushColor( ImGuiCol.Button, red );
                var        w     = Vector2.UnitX * ( width ?? ImGui.CalcItemWidth() );
                return ImGui.Button( $"Press Enter or Click Here to Save (Current Directory: {old})", w );
            }

            private void DrawRootFolder()
            {
                var save = ImGui.InputText( LabelRootFolder, ref _newModDirectory, 255, ImGuiInputTextFlags.EnterReturnsTrue );
                if( _config.ModDirectory == _newModDirectory || !_newModDirectory.Any() )
                {
                    return;
                }

                if( save || DrawPressEnterWarning( _config.ModDirectory ) )
                {
                    _base._menu.InstalledTab.Selector.ClearSelection();
                    _base._modManager.DiscoverMods( _newModDirectory );
                    _base._menu.InstalledTab.Selector.Cache.TriggerListReset();
                    _newModDirectory = _config.ModDirectory;
                }
            }

            private void DrawTempFolder()
            {
                ImGui.SetNextItemWidth( 400 * ImGuiHelpers.GlobalScale );
                ImGui.BeginGroup();
                var save = ImGui.InputText( LabelTempFolder, ref _newTempDirectory, 255, ImGuiInputTextFlags.EnterReturnsTrue );

                ImGuiCustom.HoverTooltip( "The folder used to store temporary meta manipulation files.\n"
                  + "Leave this blank if you have no reason not to.\n"
                  + "A folder 'penumbrametatmp' will be created as a subdirectory to the specified directory.\n"
                  + "If none is specified (i.e. this is blank) this folder will be created in the root folder instead." );

                ImGui.SameLine();
                if( ImGui.Button( LabelOpenTempFolder ) )
                {
                    if( !Directory.Exists( _base._modManager.TempPath.FullName ) || !_base._modManager.TempWritable )
                    {
                        return;
                    }

                    Process.Start( new ProcessStartInfo( _base._modManager.TempPath.FullName )
                    {
                        UseShellExecute = true,
                    } );
                }

                ImGui.EndGroup();
                if( _newTempDirectory == _config.TempDirectory )
                {
                    return;
                }

                if( save || DrawPressEnterWarning( _config.TempDirectory, 400 ) )
                {
                    _base._modManager.SetTempDirectory( _newTempDirectory );
                    _newTempDirectory = _config.TempDirectory;
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

                    Process.Start( new ProcessStartInfo( _config.ModDirectory )
                    {
                        UseShellExecute = true,
                    } );
                }
            }

            private void DrawEnabledBox()
            {
                var enabled = _config.IsEnabled;
                if( ImGui.Checkbox( LabelEnabled, ref enabled ) )
                {
                    _base._penumbra.SetEnabled( enabled );
                }
            }

            private void DrawShowAdvancedBox()
            {
                var showAdvanced = _config.ShowAdvanced;
                if( ImGui.Checkbox( LabelShowAdvanced, ref showAdvanced ) )
                {
                    _config.ShowAdvanced = showAdvanced;
                    _configChanged = true;
                }
            }

            private void DrawManageModsButtonOffset()
            {
                var manageModsButtonOffset = _config.ManageModsButtonOffset;
                ImGui.SetNextItemWidth( 150f );
                if( ImGui.DragFloat2( LabelManageModsOffset, ref manageModsButtonOffset, 1f ) )
                {
                    _config.ManageModsButtonOffset = manageModsButtonOffset;
                    _configChanged = true;
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
                var fsWatch = _config.DisableFileSystemNotifications;
                if( ImGui.Checkbox( LabelDisableNotifications, ref fsWatch ) )
                {
                    _config.DisableFileSystemNotifications = fsWatch;
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
                    _configChanged            = true;
                    Penumbra.PlayerWatcher.SetStatus( enabled );
                }

                ImGuiCustom.HoverTooltip(
                    "If this setting is enabled, penumbra will keep tabs on characters that have a corresponding collection setup in the Collections tab.\n"
                  + "Penumbra will try to automatically redraw those characters using their collection when they first appear in an instance, or when they change their current equip." );

                if( !_config.EnablePlayerWatch || !_config.ShowAdvanced )
                {
                    return;
                }

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

                ImGuiCustom.HoverTooltip(
                    "The number of frames penumbra waits after some events (like zone changes) until it starts trying to redraw actors again, in a range of [1, 3001].\n"
                  + "Keep this as low as possible while producing stable results." );
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
                if( !ImGui.BeginTabItem( LabelTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                DrawRootFolder();

                DrawRediscoverButton();
                ImGui.SameLine();
                DrawOpenModsButton();

                ImGuiCustom.VerticalDistance( DefaultVerticalSpace );
                DrawEnabledBox();
                DrawEnabledPlayerWatcher();

                ImGuiCustom.VerticalDistance( DefaultVerticalSpace );
                DrawScaleModSelectorBox();
                DrawSortFoldersFirstBox();
                DrawManageModsButtonOffset();
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
}