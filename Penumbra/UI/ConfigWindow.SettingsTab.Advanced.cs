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
    // Sets the resource logger state when toggled,
    // and the filter when entered.
    private void DrawRequestedResourceLogging()
    {
        var tmp = Penumbra.Config.EnableResourceLogging;
        if( ImGui.Checkbox( "##resourceLogging", ref tmp ) )
        {
            _penumbra.ResourceLogger.SetState( tmp );
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker( "Enable Requested Resource Logging", "Log all game paths FFXIV requests to the plugin log.\n"
          + "You can filter the logged paths for those containing the entered string or matching the regex, if the entered string compiles to a valid regex.\n"
          + "Red boundary indicates invalid regex." );

        ImGui.SameLine();

        // Red borders if the string is not a valid regex.
        var       tmpString = Penumbra.Config.ResourceLoggingFilter;
        using var color     = ImRaii.PushColor( ImGuiCol.Border, Colors.RegexWarningBorder, !_penumbra.ResourceLogger.ValidRegex );
        using var style     = ImRaii.PushStyle( ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale, !_penumbra.ResourceLogger.ValidRegex );
        ImGui.SetNextItemWidth( -1 );
        if( ImGui.InputTextWithHint( "##ResourceLogFilter", "Filter...", ref tmpString, Utf8GamePath.MaxGamePathLength ) )
        {
            _penumbra.ResourceLogger.SetFilter( tmpString );
        }
    }

    // Toggling audio streaming will need to apply to the music manager
    // and rediscover mods due to determining whether .scds will be loaded or not.
    private void DrawDisableSoundStreamingBox()
    {
        var tmp = Penumbra.Config.DisableSoundStreaming;
        if( ImGui.Checkbox( "##streaming", ref tmp ) && tmp != Penumbra.Config.DisableSoundStreaming )
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
        ImGuiUtil.LabeledHelpMarker( "Disable Audio Streaming",
            "Disable streaming in the games audio engine.\n"
          + "If you do not disable streaming, you can not replace sound files in the game (*.scd files), they will be ignored by Penumbra.\n\n"
          + "Only touch this if you experience sound problems.\n"
          + "If you toggle this, make sure no modified or to-be-modified sound file is currently playing or was recently playing, else you might crash.\n"
          + "You might need to restart your game for this to fully take effect." );
    }

    // Creates and destroys the web server when toggled.
    private void DrawEnableHttpApiBox()
    {
        var http = Penumbra.Config.EnableHttpApi;
        if( ImGui.Checkbox( "##http", ref http ) )
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
        ImGuiUtil.LabeledHelpMarker( "Enable HTTP API",
            "Enables other applications, e.g. Anamnesis, to use some Penumbra functions, like requesting redraws." );
    }

    // Should only be used for debugging.
    private static void DrawEnableFullResourceLoggingBox()
    {
        var tmp = Penumbra.Config.EnableFullResourceLogging;
        if( ImGui.Checkbox( "##fullLogging", ref tmp ) && tmp != Penumbra.Config.EnableFullResourceLogging )
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
        ImGuiUtil.LabeledHelpMarker( "Enable Full Resource Logging",
            "[DEBUG] Enable the logging of all ResourceLoader events indiscriminately." );
    }

    // Should only be used for debugging.
    private static void DrawEnableDebugModeBox()
    {
        var tmp = Penumbra.Config.DebugMode;
        if( ImGui.Checkbox( "##debugMode", ref tmp ) && tmp != Penumbra.Config.DebugMode )
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
        ImGuiUtil.LabeledHelpMarker( "Enable Debug Mode",
            "[DEBUG] Enable the Debug Tab and Resource Manager Tab as well as some additional data collection. Also open the config window on plugin load." );
    }

    private static void DrawReloadResourceButton()
    {
        if( ImGui.Button( "Reload Resident Resources" ) )
        {
            Penumbra.ResidentResources.Reload();
        }

        ImGuiUtil.HoverTooltip( "Reload some specific files that the game keeps in memory at all times.\n"
          + "You usually should not need to do this." );
    }

    private void DrawAdvancedSettings()
    {
        if( !Penumbra.Config.ShowAdvanced || !ImGui.CollapsingHeader( "Advanced" ) )
        {
            return;
        }

        DrawRequestedResourceLogging();
        DrawDisableSoundStreamingBox();
        DrawEnableHttpApiBox();
        DrawEnableDebugModeBox();
        DrawEnableFullResourceLoggingBox();
        DrawReloadResourceButton();
        ImGui.NewLine();
    }
}