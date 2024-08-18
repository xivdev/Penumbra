using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OtterGui.Services;

namespace Penumbra.UI;

/// <summary>
/// A Launch Button used in the title screen of the game,
/// using the Dalamud-provided collapsible submenu.
/// </summary>
public class LaunchButton : IDisposable, IUiService
{
    private readonly ConfigWindow     _configWindow;
    private readonly IUiBuilder       _uiBuilder;
    private readonly ITitleScreenMenu _title;
    private readonly string           _fileName;
    private readonly ITextureProvider _textureProvider;

    private IDalamudTextureWrap?           _icon;
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
        _icon            = null;
        _entry           = null;

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
            // TODO: update when API updated.
            _icon = _textureProvider.GetFromFile(_fileName).RentAsync().Result;
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
