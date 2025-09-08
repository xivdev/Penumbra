using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Penumbra.UI;

/// <summary>
/// A Launch Button used in the title screen of the game,
/// using the Dalamud-provided collapsible submenu.
/// </summary>
public class LaunchButton : IDisposable, Luna.IUiService
{
    private readonly ConfigWindow     _configWindow;
    private readonly IUiBuilder       _uiBuilder;
    private readonly ITitleScreenMenu _title;
    private readonly string           _fileName;
    private readonly ITextureProvider _textureProvider;

    private IReadOnlyTitleScreenMenuEntry? _entry;

    /// <summary>
    /// Register the launch button to be created on the next draw event.
    /// </summary>
    public LaunchButton(IDalamudPluginInterface pi, ITitleScreenMenu title, ConfigWindow ui, ITextureProvider textureProvider)
    {
        _uiBuilder       = pi.UiBuilder;
        _configWindow    = ui;
        _textureProvider = textureProvider;
        _title           = title;
        _entry           = null;

        _fileName       =  Path.Combine(pi.AssemblyLocation.DirectoryName!, "tsmLogo.png");
        _uiBuilder.Draw += CreateEntry;
    }

    public void Dispose()
    {
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
            // TODO: update when API updated.
            var icon = _textureProvider.GetFromFile(_fileName);
            _entry = _title.AddEntry("Manage Penumbra", icon, OnTriggered);

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
