using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Mods;
using Penumbra.UI.Classes;
using Penumbra.Util;

namespace Penumbra.UI;

public sealed partial class ConfigWindow : Window, IDisposable
{
    private readonly Penumbra              _penumbra;
    private readonly ModFileSystemSelector _selector;
    private readonly ModPanel              _modPanel;
    public readonly  ModEditWindow         ModEditPopup = new();

    private readonly SettingsTab     _settingsTab;
    private readonly CollectionsTab  _collectionsTab;
    private readonly ModsTab         _modsTab;
    private readonly ChangedItemsTab _changedItemsTab;
    private readonly EffectiveTab    _effectiveTab;
    private readonly DebugTab        _debugTab;
    private readonly ResourceTab     _resourceTab;
    private readonly ResourceWatcher _resourceWatcher;

    public TabType SelectTab = TabType.None;
    public void SelectMod( Mod mod )
        => _selector.SelectByValue( mod );

    public ConfigWindow( Penumbra penumbra, ResourceWatcher watcher )
        : base( GetLabel() )
    {
        _penumbra        = penumbra;
        _resourceWatcher = watcher;

        _settingsTab               =  new SettingsTab( this );
        _selector                  =  new ModFileSystemSelector( _penumbra.ModFileSystem );
        _modPanel                  =  new ModPanel( this );
        _modsTab                   =  new ModsTab( _selector, _modPanel, _penumbra );
        _selector.SelectionChanged += _modPanel.OnSelectionChange;
        _collectionsTab            =  new CollectionsTab( this );
        _changedItemsTab           =  new ChangedItemsTab( this );
        _effectiveTab              =  new EffectiveTab();
        _debugTab                  =  new DebugTab( this );
        _resourceTab               =  new ResourceTab();
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

    private ReadOnlySpan< byte > ToLabel( TabType type )
        => type switch
        {
            TabType.Settings         => _settingsTab.Label,
            TabType.Mods             => _modsTab.Label,
            TabType.Collections      => _collectionsTab.Label,
            TabType.ChangedItems     => _changedItemsTab.Label,
            TabType.EffectiveChanges => _effectiveTab.Label,
            TabType.ResourceWatcher  => _resourceWatcher.Label,
            TabType.Debug            => _debugTab.Label,
            TabType.ResourceManager  => _resourceTab.Label,
            _                        => ReadOnlySpan< byte >.Empty,
        };

    public override void Draw()
    {
        using var performance = Penumbra.Performance.Measure( PerformanceType.UiMainWindow );

        try
        {
            if( Penumbra.ValidityChecker.ImcExceptions.Count > 0 )
            {
                DrawProblemWindow( $"There were {Penumbra.ValidityChecker.ImcExceptions.Count} errors while trying to load IMC files from the game data.\n"
                  + "This usually means that your game installation was corrupted by updating the game while having TexTools mods still active.\n"
                  + "It is recommended to not use TexTools and Penumbra (or other Lumina-based tools) at the same time.\n\n"
                  + "Please use the Launcher's Repair Game Files function to repair your client installation.", true );
            }
            else if( !Penumbra.ValidityChecker.IsValidSourceRepo )
            {
                DrawProblemWindow(
                    $"You are loading a release version of Penumbra from the repository \"{Dalamud.PluginInterface.SourceRepository}\" instead of the official repository.\n"
                  + $"Please use the official repository at {ValidityChecker.Repository}.\n\n"
                  + "If you are developing for Penumbra and see this, you should compile your version in debug mode to avoid it.", false );
            }
            else if( Penumbra.ValidityChecker.IsNotInstalledPenumbra )
            {
                DrawProblemWindow(
                    $"You are loading a release version of Penumbra from \"{Dalamud.PluginInterface.AssemblyLocation.Directory?.FullName ?? "Unknown"}\" instead of the installedPlugins directory.\n\n"
                  + "You should not install Penumbra manually, but rather add the plugin repository under settings and then install it via the plugin installer.\n\n"
                  + "If you do not know how to do this, please take a look at the readme in Penumbras github repository or join us in discord.\n"
                  + "If you are developing for Penumbra and see this, you should compile your version in debug mode to avoid it.", false );
            }
            else if( Penumbra.ValidityChecker.DevPenumbraExists )
            {
                DrawProblemWindow(
                    $"You are loading a installed version of Penumbra from \"{Dalamud.PluginInterface.AssemblyLocation.Directory?.FullName ?? "Unknown"}\", "
                  + "but also still have some remnants of a custom install of Penumbra in your devPlugins folder.\n\n"
                  + "This can cause some issues, so please go to your \"%%appdata%%\\XIVLauncher\\devPlugins\" folder and delete the Penumbra folder from there.\n\n"
                  + "If you are developing for Penumbra, try to avoid mixing versions. This warning will not appear if compiled in Debug mode.", false );
            }
            else
            {
                SetupSizes();
                if( TabBar.Draw( string.Empty, ImGuiTabBarFlags.NoTooltip, ToLabel( SelectTab ), _settingsTab, _modsTab, _collectionsTab,
                       _changedItemsTab, _effectiveTab, _resourceWatcher, _debugTab, _resourceTab ) )
                {
                    SelectTab = TabType.None;
                }
            }
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Exception thrown during UI Render:\n{e}" );
        }
    }

    private static void DrawProblemWindow( string text, bool withExceptions )
    {
        using var color = ImRaii.PushColor( ImGuiCol.Text, Colors.RegexWarningBorder );
        ImGui.NewLine();
        ImGui.NewLine();
        ImGuiUtil.TextWrapped( text );
        color.Pop();

        ImGui.NewLine();
        ImGui.NewLine();
        SettingsTab.DrawDiscordButton( 0 );
        ImGui.SameLine();
        SettingsTab.DrawSupportButton();
        ImGui.NewLine();
        ImGui.NewLine();

        if( withExceptions )
        {
            ImGui.TextUnformatted( "Exceptions" );
            ImGui.Separator();
            using var box = ImRaii.ListBox( "##Exceptions", new Vector2( -1, -1 ) );
            foreach( var exception in Penumbra.ValidityChecker.ImcExceptions )
            {
                ImGuiUtil.TextWrapped( exception.ToString() );
                ImGui.Separator();
                ImGui.NewLine();
            }
        }
    }

    public void Dispose()
    {
        _selector.Dispose();
        _modPanel.Dispose();
        _collectionsTab.Dispose();
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