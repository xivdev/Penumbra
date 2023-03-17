using System;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Penumbra.UI;
using Penumbra.UI.Classes;

namespace Penumbra;

public class PenumbraWindowSystem : IDisposable
{
    private readonly UiBuilder         _uiBuilder;
    private readonly WindowSystem      _windowSystem;
    public readonly  ConfigWindow      Window;
    public readonly  PenumbraChangelog Changelog;

    public PenumbraWindowSystem(DalamudPluginInterface pi, PenumbraChangelog changelog, ConfigWindow window, LaunchButton _,
        ModEditWindow editWindow)
    {
        _uiBuilder    = pi.UiBuilder;
        Changelog     = changelog;
        Window        = window;
        _windowSystem = new WindowSystem("Penumbra");
        _windowSystem.AddWindow(changelog.Changelog);
        _windowSystem.AddWindow(window);
        _windowSystem.AddWindow(editWindow);

        _uiBuilder.OpenConfigUi += Window.Toggle;
        _uiBuilder.Draw         += _windowSystem.Draw;
    }

    public void ForceChangelogOpen()
        => Changelog.Changelog.ForceOpen = true;

    public void Dispose()
    {
        _uiBuilder.OpenConfigUi -= Window.Toggle;
        _uiBuilder.Draw         -= _windowSystem.Draw;
    }
}
