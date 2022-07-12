using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using OtterGui.Raii;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public sealed partial class ConfigWindow : Window, IDisposable
{
    private readonly Penumbra              _penumbra;
    private readonly SettingsTab           _settingsTab;
    private readonly ModFileSystemSelector _selector;
    private readonly ModPanel              _modPanel;
    private readonly CollectionsTab        _collectionsTab;
    private readonly EffectiveTab          _effectiveTab;
    private readonly DebugTab              _debugTab;
    private readonly ResourceTab           _resourceTab;
    public readonly  ModEditWindow         ModEditPopup = new();

    public ConfigWindow( Penumbra penumbra )
        : base( GetLabel() )
    {
        _penumbra                  =  penumbra;
        _settingsTab               =  new SettingsTab( this );
        _selector                  =  new ModFileSystemSelector( _penumbra.ModFileSystem );
        _modPanel                  =  new ModPanel( this );
        _selector.SelectionChanged += _modPanel.OnSelectionChange;
        _collectionsTab            =  new CollectionsTab( this );
        _effectiveTab              =  new EffectiveTab();
        _debugTab                  =  new DebugTab( this );
        _resourceTab               =  new ResourceTab( this );
        if( Penumbra.Config.FixMainWindow )
        {
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        }

        Dalamud.PluginInterface.UiBuilder.DisableGposeUiHide    = !Penumbra.Config.HideUiInGPose;
        Dalamud.PluginInterface.UiBuilder.DisableCutsceneUiHide = !Penumbra.Config.HideUiInCutscenes;
        Dalamud.PluginInterface.UiBuilder.DisableUserUiHide     = !Penumbra.Config.HideUiWhenUiHidden;
        RespectCloseHotkey                                      = true;
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2( 800, 600 ),
            MaximumSize = new Vector2( 4096, 2160 ),
        };
        UpdateTutorialStep();
    }

    public override void Draw()
    {
        try
        {
            if( Penumbra.ImcExceptions > 0 )
            {
                DrawProblemWindow( $"There were {Penumbra.ImcExceptions} errors while trying to load IMC files from the game data.\n"
                  + "This usually means that your game installation was corrupted by updating the game while having TexTools mods still active.\n"
                  + "It is recommended to not use TexTools and Penumbra (or other Lumina-based tools) at the same time.\n\n"
                  + "Please use the Launcher's Repair Game Files function to repair your client installation." );
            }
            else if( Penumbra.IsNotInstalledPenumbra )
            {
                DrawProblemWindow(
                    $"You are loading a release version of Penumbra from \"{Dalamud.PluginInterface.AssemblyLocation.Directory?.FullName ?? "Unknown"}\" instead of the installedPlugins directory.\n\n"
                  + "You should not install Penumbra manually, but rather add the plugin repository under settings and then install it via the plugin installer.\n\n"
                  + "If you do not know how to do this, please take a look at the readme in Penumbras github repository or join us in discord.\n"
                  + "If you are developing for Penumbra and see this, you should compile your version in debug mode to avoid it." );
            }
            else if( Penumbra.DevPenumbraExists )
            {
                DrawProblemWindow(
                    $"You are loading a installed version of Penumbra from \"{Dalamud.PluginInterface.AssemblyLocation.Directory?.FullName ?? "Unknown"}\", "
                  + "but also still have some remnants of a custom install of Penumbra in your devPlugins folder.\n\n"
                  + "This can cause some issues, so please go to your \"%%appdata%%\\XIVLauncher\\devPlugins\" folder and delete the Penumbra folder from there.\n\n"
                  + "If you are developing for Penumbra, try to avoid mixing versions. This warning will not appear if compiled in Debug mode." );
            }
            else
            {
                using var bar = ImRaii.TabBar( string.Empty, ImGuiTabBarFlags.NoTooltip );
                SetupSizes();
                _settingsTab.Draw();
                DrawModsTab();
                _collectionsTab.Draw();
                DrawChangedItemTab();
                _effectiveTab.Draw();
                _debugTab.Draw();
                _resourceTab.Draw();
            }
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Exception thrown during UI Render:\n{e}" );
        }
    }

    private static void DrawProblemWindow( string text )
    {
        using var color = ImRaii.PushColor( ImGuiCol.Text, Colors.RegexWarningBorder );
        ImGui.NewLine();
        ImGui.NewLine();
        ImGui.TextWrapped( text );
        color.Pop();

        ImGui.NewLine();
        ImGui.NewLine();
        SettingsTab.DrawDiscordButton( 0 );
        ImGui.SameLine();
        SettingsTab.DrawSupportButton();
    }

    public void Dispose()
    {
        _selector.Dispose();
        _modPanel.Dispose();
        ModEditPopup.Dispose();
    }

    private static string GetLabel()
        => Penumbra.Version.Length == 0
            ? "Penumbra###PenumbraConfigWindow"
            : $"Penumbra v{Penumbra.Version}###PenumbraConfigWindow";

    private Vector2 _defaultSpace;
    private Vector2 _inputTextWidth;
    private Vector2 _iconButtonSize;

    private void SetupSizes()
    {
        _defaultSpace   = new Vector2( 0, 10 * ImGuiHelpers.GlobalScale );
        _inputTextWidth = new Vector2( 350f  * ImGuiHelpers.GlobalScale, 0 );
        _iconButtonSize = new Vector2( ImGui.GetFrameHeight() );
    }
}