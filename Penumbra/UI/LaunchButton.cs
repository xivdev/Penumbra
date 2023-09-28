using Dalamud.Interface;
using Dalamud.Interface.Internal;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Penumbra.UI;

/// <summary>
/// A Launch Button used in the title screen of the game,
/// using the Dalamud-provided collapsible submenu.
/// </summary>
public class LaunchButton : IDisposable
{
    private readonly ConfigWindow     _configWindow;
    private readonly UiBuilder        _uiBuilder;
    private readonly ITitleScreenMenu _title;
    private readonly string           _fileName;

    private IDalamudTextureWrap?  _icon;
    private TitleScreenMenuEntry? _entry;

    /// <summary>
    /// Register the launch button to be created on the next draw event.
    /// </summary>
    public LaunchButton(DalamudPluginInterface pi, ITitleScreenMenu title, ConfigWindow ui)
    {
        _uiBuilder    = pi.UiBuilder;
        _configWindow = ui;
        _title        = title;
        _icon         = null;
        _entry        = null;

        _fileName       =  Path.Combine(pi.AssemblyLocation.DirectoryName!, "tsmLogo.png");
        _uiBuilder.Draw += CreateEntry;
    }

    public void Dispose()
    {
        _icon?.Dispose();
        if (_entry != null)
            _title.RemoveEntry(_entry);
    }

    /// <summary>
    /// One-Time event to load the image and create the entry on the first drawn frame, but not before.
    /// </summary>
    private void CreateEntry()
    {
        try
        {
            _icon = _uiBuilder.LoadImage(_fileName);
            if (_icon != null)
                _entry = _title.AddEntry("Manage Penumbra", _icon, OnTriggered);

            _uiBuilder.Draw -= CreateEntry;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Could not register title screen menu entry:\n{ex}");
        }
    }

    private void OnTriggered()
        => _configWindow.Toggle();
}
