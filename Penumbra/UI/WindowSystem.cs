using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Penumbra.UI.AdvancedWindow;
using Penumbra.UI.Tabs;

namespace Penumbra.UI;

public class PenumbraWindowSystem : IDisposable
{
    private readonly UiBuilder         _uiBuilder;
    private readonly WindowSystem      _windowSystem;
    private readonly FileDialogService _fileDialog;
    public readonly  ConfigWindow      Window;
    public readonly  PenumbraChangelog Changelog;

    public PenumbraWindowSystem(DalamudPluginInterface pi, Configuration config, PenumbraChangelog changelog, ConfigWindow window,
        LaunchButton _, ModEditWindow editWindow, FileDialogService fileDialog, ImportPopup importPopup, DebugTab debugTab)
    {
        _uiBuilder    = pi.UiBuilder;
        _fileDialog   = fileDialog;
        Changelog     = changelog;
        Window        = window;
        _windowSystem = new WindowSystem("Penumbra");
        _windowSystem.AddWindow(changelog.Changelog);
        _windowSystem.AddWindow(window);
        _windowSystem.AddWindow(editWindow);
        _windowSystem.AddWindow(importPopup);
        _windowSystem.AddWindow(debugTab);
        _uiBuilder.OpenConfigUi          += Window.Toggle;
        _uiBuilder.Draw                  += _windowSystem.Draw;
        _uiBuilder.Draw                  += _fileDialog.Draw;
        _uiBuilder.DisableGposeUiHide    =  !config.HideUiInGPose;
        _uiBuilder.DisableCutsceneUiHide =  !config.HideUiInCutscenes;
        _uiBuilder.DisableUserUiHide     =  !config.HideUiWhenUiHidden;
    }

    public void ForceChangelogOpen()
        => Changelog.Changelog.ForceOpen = true;

    public void Dispose()
    {
        _uiBuilder.OpenConfigUi -= Window.Toggle;
        _uiBuilder.Draw         -= _windowSystem.Draw;
        _uiBuilder.Draw         -= _fileDialog.Draw;
    }
}
