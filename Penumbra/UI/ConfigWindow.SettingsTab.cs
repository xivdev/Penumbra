using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.ByteString;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private string _newModDirectory = string.Empty;

    private static bool DrawPressEnterWarning( string old )
    {
        using var color = ImRaii.PushColor( ImGuiCol.Button, Colors.PressEnterWarningBg );
        var       w     = new Vector2( ImGui.CalcItemWidth(), 0 );
        return ImGui.Button( $"Press Enter or Click Here to Save (Current Directory: {old})", w );
    }

    private static void DrawOpenDirectoryButton( int id, DirectoryInfo directory, bool condition )
    {
        using var _   = ImRaii.PushId( id );
        var       ret = ImGui.Button( "Open Directory" );
        ImGuiUtil.HoverTooltip( "Open this directory in your configured file explorer." );
        if( ret && condition && Directory.Exists( directory.FullName ) )
        {
            Process.Start( new ProcessStartInfo( directory.FullName )
            {
                UseShellExecute = true,
            } );
        }
    }

    private void DrawRootFolder()
    {
        using var group = ImRaii.Group();
        ImGui.SetNextItemWidth( _inputTextWidth.X );
        var save = ImGui.InputText( "Root Directory", ref _newModDirectory, 255, ImGuiInputTextFlags.EnterReturnsTrue );
        ImGui.SameLine();
        ImGuiComponents.HelpMarker( "This is where Penumbra will store your extracted mod files.\n"
          + "TTMP files are not copied, just extracted.\n"
          + "This directory needs to be accessible and you need write access here.\n"
          + "It is recommended that this directory is placed on a fast hard drive, preferably an SSD.\n"
          + "It should also be placed near the root of a logical drive - the shorter the total path to this folder, the better.\n"
          + "Definitely do not place it in your Dalamud directory or any sub-directory thereof." );
        ImGui.SameLine();
        DrawOpenDirectoryButton( 0, Penumbra.ModManager.BasePath, Penumbra.ModManager.Valid );
        group.Dispose();

        if( Penumbra.Config.ModDirectory == _newModDirectory || _newModDirectory.Length == 0 )
        {
            return;
        }

        if( save || DrawPressEnterWarning( Penumbra.Config.ModDirectory ) )
        {
            Penumbra.ModManager.DiscoverMods( _newModDirectory );
        }
    }


    private void DrawRediscoverButton()
    {
        if( ImGui.Button( "Rediscover Mods" ) )
        {
            Penumbra.ModManager.DiscoverMods();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker( "Force Penumbra to completely re-scan your root directory as if it was restarted." );
    }

    private void DrawEnabledBox()
    {
        var enabled = Penumbra.Config.EnableMods;
        if( ImGui.Checkbox( "Enable Mods", ref enabled ) )
        {
            _penumbra.SetEnabled( enabled );
        }
    }

    private void DrawShowAdvancedBox()
    {
        var showAdvanced = Penumbra.Config.ShowAdvanced;
        if( ImGui.Checkbox( "Show Advanced Settings", ref showAdvanced ) )
        {
            Penumbra.Config.ShowAdvanced = showAdvanced;
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker( "Enable some advanced options in this window and in the mod selector.\n"
          + "This is required to enable manually editing any mod information." );
    }

    private void DrawFolderSortType()
    {
        // TODO provide all options
        var foldersFirst = Penumbra.Config.SortFoldersFirst;
        if( ImGui.Checkbox( "Sort Mod-Folders Before Mods", ref foldersFirst ) )
        {
            Penumbra.Config.SortFoldersFirst = foldersFirst;
            Selector.SetFilterDirty();
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "Prioritizes all mod-folders in the mod-selector in the Installed Mods tab so that folders come before single mods, instead of being sorted completely alphabetically" );
    }

    private void DrawScaleModSelectorBox()
    {
        // TODO set scale
        var scaleModSelector = Penumbra.Config.ScaleModSelector;
        if( ImGui.Checkbox( "Scale Mod Selector With Window Size", ref scaleModSelector ) )
        {
            Penumbra.Config.ScaleModSelector = scaleModSelector;
            Penumbra.Config.Save();
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
            Penumbra.Config.Save();
            if( tmp )
            {
                _penumbra.MusicManager.DisableStreaming();
            }
            else
            {
                _penumbra.MusicManager.EnableStreaming();
            }

            Penumbra.ModManager.DiscoverMods();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "Disable streaming in the games audio engine.\n"
          + "If you do not disable streaming, you can not replace sound files in the game (*.scd files), they will be ignored by Penumbra.\n\n"
          + "Only touch this if you experience sound problems.\n"
          + "If you toggle this, make sure no modified or to-be-modified sound file is currently playing or was recently playing, else you might crash." );
    }


    private void DrawEnableHttpApiBox()
    {
        var http = Penumbra.Config.EnableHttpApi;
        if( ImGui.Checkbox( "Enable HTTP API", ref http ) )
        {
            if( http )
            {
                _penumbra.CreateWebServer();
            }
            else
            {
                _penumbra.ShutdownWebServer();
            }

            Penumbra.Config.EnableHttpApi = http;
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "Enables other applications, e.g. Anamnesis, to use some Penumbra functions, like requesting redraws." );
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
        var tmp = Penumbra.Config.EnableFullResourceLogging;
        if( ImGui.Checkbox( "Enable Full Resource Logging", ref tmp ) && tmp != Penumbra.Config.EnableFullResourceLogging )
        {
            if( tmp )
            {
                Penumbra.ResourceLoader.EnableFullLogging();
            }
            else
            {
                Penumbra.ResourceLoader.DisableFullLogging();
            }

            Penumbra.Config.EnableFullResourceLogging = tmp;
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker( "[DEBUG] Enable the logging of all ResourceLoader events indiscriminately." );
    }

    private void DrawEnableDebugModeBox()
    {
        var tmp = Penumbra.Config.DebugMode;
        if( ImGui.Checkbox( "Enable Debug Mode", ref tmp ) && tmp != Penumbra.Config.DebugMode )
        {
            if( tmp )
            {
                Penumbra.ResourceLoader.EnableDebug();
            }
            else
            {
                Penumbra.ResourceLoader.DisableDebug();
            }

            Penumbra.Config.DebugMode = tmp;
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker( "[DEBUG] Enable the Debug Tab and Resource Manager Tab as well as some additional data collection." );
    }

    private void DrawRequestedResourceLogging()
    {
        var tmp = Penumbra.Config.EnableResourceLogging;
        if( ImGui.Checkbox( "Enable Requested Resource Logging", ref tmp ) )
        {
            _penumbra.ResourceLogger.SetState( tmp );
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker( "Log all game paths FFXIV requests to the plugin log.\n"
          + "You can filter the logged paths for those containing the entered string or matching the regex, if the entered string compiles to a valid regex.\n"
          + "Red boundary indicates invalid regex." );
        ImGui.SameLine();
        var       tmpString = Penumbra.Config.ResourceLoggingFilter;
        using var color     = ImRaii.PushColor( ImGuiCol.Border, 0xFF0000B0, !_penumbra.ResourceLogger.ValidRegex );
        using var style     = ImRaii.PushStyle( ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale, !_penumbra.ResourceLogger.ValidRegex );
        if( ImGui.InputTextWithHint( "##ResourceLogFilter", "Filter...", ref tmpString, Utf8GamePath.MaxGamePathLength ) )
        {
            _penumbra.ResourceLogger.SetFilter( tmpString );
        }
    }

    private void DrawAdvancedSettings()
    {
        DrawRequestedResourceLogging();
        DrawDisableSoundStreamingBox();
        DrawEnableHttpApiBox();
        DrawReloadResourceButton();
        DrawEnableDebugModeBox();
        DrawEnableFullResourceLoggingBox();
    }

    public void DrawSettingsTab()
    {
        using var tab = ImRaii.TabItem( "Settings" );
        if( !tab )
        {
            return;
        }

        using var child = ImRaii.Child( "##SettingsTab", -Vector2.One, false );
        if( !child )
        {
            return;
        }

        DrawRootFolder();

        DrawRediscoverButton();

        ImGui.Dummy( _verticalSpace );
        DrawEnabledBox();

        ImGui.Dummy( _verticalSpace );
        DrawFolderSortType();
        DrawScaleModSelectorBox();
        DrawShowAdvancedBox();

        if( Penumbra.Config.ShowAdvanced )
        {
            DrawAdvancedSettings();
        }

        if( ImGui.CollapsingHeader( "Colors" ) )
        {
            foreach( var color in Enum.GetValues< ColorId >() )
            {
                var (defaultColor, name, description) = color.Data();
                var currentColor = Penumbra.Config.Colors.TryGetValue( color, out var current ) ? current : defaultColor;
                if( ImGuiUtil.ColorPicker( name, description, currentColor, c => Penumbra.Config.Colors[ color ] = c, defaultColor ) )
                {
                    Penumbra.Config.Save();
                }
            }
        }
    }
}