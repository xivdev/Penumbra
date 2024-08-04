using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Services;
using Penumbra.Interop.Services;
using Penumbra.UI.AdvancedWindow;
using Penumbra.UI.Knowledge;
using Penumbra.UI.Tabs.Debug;

namespace Penumbra.UI;

public class PenumbraWindowSystem : IDisposable, IUiService
{
    private readonly IUiBuilder         _uiBuilder;
    private readonly WindowSystem       _windowSystem;
    private readonly FileDialogService  _fileDialog;
    private readonly TextureArraySlicer _textureArraySlicer;
    public readonly  ConfigWindow       Window;
    public readonly  PenumbraChangelog  Changelog;
    public readonly  KnowledgeWindow    KnowledgeWindow;

    public PenumbraWindowSystem(IDalamudPluginInterface pi, Configuration config, PenumbraChangelog changelog, ConfigWindow window,
        LaunchButton _, ModEditWindow editWindow, FileDialogService fileDialog, ImportPopup importPopup, DebugTab debugTab,
        KnowledgeWindow knowledgeWindow, TextureArraySlicer textureArraySlicer)
    {
        _uiBuilder          = pi.UiBuilder;
        _fileDialog         = fileDialog;
        _textureArraySlicer = textureArraySlicer;
        KnowledgeWindow     = knowledgeWindow;
        Changelog           = changelog;
        Window              = window;
        _windowSystem       = new WindowSystem("Penumbra");
        _windowSystem.AddWindow(changelog.Changelog);
        _windowSystem.AddWindow(window);
        _windowSystem.AddWindow(editWindow);
        _windowSystem.AddWindow(importPopup);
        _windowSystem.AddWindow(debugTab);
        _windowSystem.AddWindow(KnowledgeWindow);
        _uiBuilder.OpenMainUi            += Window.Toggle;
        _uiBuilder.OpenConfigUi          += Window.OpenSettings;
        _uiBuilder.Draw                  += _windowSystem.Draw;
        _uiBuilder.Draw                  += _fileDialog.Draw;
        _uiBuilder.Draw                  += _textureArraySlicer.Tick;
        _uiBuilder.DisableGposeUiHide    =  !config.HideUiInGPose;
        _uiBuilder.DisableCutsceneUiHide =  !config.HideUiInCutscenes;
        _uiBuilder.DisableUserUiHide     =  !config.HideUiWhenUiHidden;
    }

    public void ForceChangelogOpen()
        => Changelog.Changelog.ForceOpen = true;

    public void Dispose()
    {
        _uiBuilder.OpenMainUi   -= Window.Toggle;
        _uiBuilder.OpenConfigUi -= Window.OpenSettings;
        _uiBuilder.Draw         -= _windowSystem.Draw;
        _uiBuilder.Draw         -= _fileDialog.Draw;
        _uiBuilder.Draw         -= _textureArraySlicer.Tick;
    }
}
