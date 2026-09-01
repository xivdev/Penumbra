using Dalamud.Interface;
using Dalamud.Plugin;
using Luna;
using Penumbra.UI.AdvancedWindow;
using Penumbra.UI.Classes;
using Penumbra.UI.Knowledge;
using Penumbra.UI.Tabs.Debug;

namespace Penumbra.UI;

public class PenumbraWindowSystem : IDisposable, IUiService
{
    private readonly IUiBuilder            _uiBuilder;
    private readonly WindowSystem          _windowSystem;
    private readonly FileDialogService     _fileDialog;
    public readonly  MainWindow.MainWindow Window;
    public readonly  PenumbraChangelog     Changelog;
    public readonly  KnowledgeWindow       KnowledgeWindow;

    public PenumbraWindowSystem(IDalamudPluginInterface pi, Configuration config, PenumbraChangelog changelog, MainWindow.MainWindow window,
        LaunchButton _, ModEditWindowFactory editWindowFactory, FileDialogService fileDialog, ImportPopup importPopup, DebugTab debugTab,
        KnowledgeWindow knowledgeWindow, WindowSystem windowSystem)
    {
        _uiBuilder          = pi.UiBuilder;
        _fileDialog         = fileDialog;
        _windowSystem       = windowSystem;
        KnowledgeWindow     = knowledgeWindow;
        Changelog           = changelog;
        Window              = window;
        _windowSystem.AddWindow(changelog.Changelog);
        _windowSystem.AddWindow(window);
        _windowSystem.AddWindow(importPopup);
        _windowSystem.AddWindow(debugTab);
        _windowSystem.AddWindow(KnowledgeWindow);
        _uiBuilder.OpenMainUi            += Window.Toggle;
        _uiBuilder.OpenConfigUi          += Window.OpenSettings;
        _uiBuilder.Draw                  += _fileDialog.Draw;
        _uiBuilder.DisableGposeUiHide    =  !config.Ui.HideUiInGPose;
        _uiBuilder.DisableCutsceneUiHide =  !config.Ui.HideUiInCutscenes;
        _uiBuilder.DisableUserUiHide     =  !config.Ui.HideUiWhenUiHidden;
    }

    public void ForceChangelogOpen()
        => Changelog.Changelog.ForceOpen = true;

    public void Dispose()
    {
        _uiBuilder.OpenMainUi   -= Window.Toggle;
        _uiBuilder.OpenConfigUi -= Window.OpenSettings;
        _uiBuilder.Draw         -= _fileDialog.Draw;
    }
}
