using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Interop;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class SettingsTab
    {
        private void DrawAdvancedSettings()
        {
            var header = ImGui.CollapsingHeader( "Advanced" );
            OpenTutorial( BasicTutorialSteps.AdvancedSettings );

            if( !header )
            {
                return;
            }

            Checkbox( "Auto Deduplicate on Import",
                "Automatically deduplicate mod files on import. This will make mod file sizes smaller, but deletes (binary identical) files.",
                Penumbra.Config.AutoDeduplicateOnImport, v => Penumbra.Config.AutoDeduplicateOnImport = v );
            Checkbox( "Keep Default Metadata Changes on Import",
                "Normally, metadata changes that equal their default values, which are sometimes exported by TexTools, are discarded. "
              + "Toggle this to keep them, for example if an option in a mod is supposed to disable a metadata change from a prior option.",
                Penumbra.Config.KeepDefaultMetaChanges, v => Penumbra.Config.KeepDefaultMetaChanges = v );
            DrawWaitForPluginsReflection();
            DrawRequestedResourceLogging();
            DrawEnableHttpApiBox();
            DrawEnableDebugModeBox();
            DrawEnableFullResourceLoggingBox();
            DrawReloadResourceButton();
            DrawReloadFontsButton();
            ImGui.NewLine();
        }

        // Sets the resource logger state when toggled,
        // and the filter when entered.
        private void DrawRequestedResourceLogging()
        {
            var tmp = Penumbra.Config.EnableResourceLogging;
            if( ImGui.Checkbox( "##resourceLogging", ref tmp ) )
            {
                _window._penumbra.ResourceLogger.SetState( tmp );
            }

            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( "Enable Requested Resource Logging", "Log all game paths FFXIV requests to the plugin log.\n"
              + "You can filter the logged paths for those containing the entered string or matching the regex, if the entered string compiles to a valid regex.\n"
              + "Red boundary indicates invalid regex." );

            ImGui.SameLine();

            // Red borders if the string is not a valid regex.
            var       tmpString = Penumbra.Config.ResourceLoggingFilter;
            using var color     = ImRaii.PushColor( ImGuiCol.Border, Colors.RegexWarningBorder, !_window._penumbra.ResourceLogger.ValidRegex );
            using var style = ImRaii.PushStyle( ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale,
                !_window._penumbra.ResourceLogger.ValidRegex );
            ImGui.SetNextItemWidth( -1 );
            if( ImGui.InputTextWithHint( "##ResourceLogFilter", "Filter...", ref tmpString, Utf8GamePath.MaxGamePathLength ) )
            {
                _window._penumbra.ResourceLogger.SetFilter( tmpString );
            }
        }

        // Creates and destroys the web server when toggled.
        private void DrawEnableHttpApiBox()
        {
            var http = Penumbra.Config.EnableHttpApi;
            if( ImGui.Checkbox( "##http", ref http ) )
            {
                if( http )
                {
                    _window._penumbra.CreateWebServer();
                }
                else
                {
                    _window._penumbra.ShutdownWebServer();
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
            if( ImGui.Button( "Reload Resident Resources" ) && Penumbra.CharacterUtility.Ready )
            {
                Penumbra.ResidentResources.Reload();
            }

            ImGuiUtil.HoverTooltip( "Reload some specific files that the game keeps in memory at all times.\n"
              + "You usually should not need to do this." );
        }

        private static void DrawReloadFontsButton()
        {
            if( ImGuiUtil.DrawDisabledButton( "Reload Fonts", Vector2.Zero, "Force the game to reload its font files.", !FontReloader.Valid ) )
            {
                FontReloader.Reload();
            }
        }

        private static void DrawWaitForPluginsReflection()
        {
            if( !Dalamud.GetDalamudConfig( Dalamud.WaitingForPluginsOption, out bool value ) )
            {
                using var disabled = ImRaii.Disabled();
                Checkbox( "Wait for Plugins on Startup (Disabled, can not access Dalamud Configuration)", string.Empty, false, v => { } );
            }
            else
            {
                Checkbox( "Wait for Plugins on Startup", "This changes a setting in the Dalamud Configuration found at /xlsettings -> General.", value,
                    v => Dalamud.SetDalamudConfig( Dalamud.WaitingForPluginsOption, v, "doWaitForPluginsOnStartup" ) );
            }
        }
    }
}