using Dalamud.Plugin;
using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Services;

namespace Penumbra.UI.ModsTab;

public class ModPanel : IDisposable, IPanel
{
    private readonly MultiModPanel  _multiModPanel;
    private readonly ModSelection   _selection;
    private readonly ModPanelHeader _header;
    private readonly ModPanelTabBar _tabs;
    private          bool           _resetCursor;

    public ModPanel(IDalamudPluginInterface pi, ModSelection selection, ModPanelTabBar tabs,
        MultiModPanel multiModPanel, CommunicatorService communicator)
    {
        _selection     = selection;
        _tabs          = tabs;
        _multiModPanel = multiModPanel;
        _header        = new ModPanelHeader(pi, communicator);
        _selection.Subscribe(OnSelectionChange, ModSelection.Priority.ModPanel);
        OnSelectionChange(new ModSelection.Arguments(null, _selection.Mod));
    }

    public ReadOnlySpan<byte> Id
        => "MP"u8;

    public void Draw()
    {
        if (!_valid)
        {
            _multiModPanel.Draw();
            return;
        }

        if (_resetCursor)
        {
            _resetCursor = false;
            Im.Scroll.X  = 0;
        }

        _header.Draw();
        Im.Cursor.X += Im.Scroll.X;
        using var child = Im.Child.Begin("Tabs"u8,
            Im.ContentRegion.Available with { X = Im.Window.MaximumContentRegion.X - Im.Window.MinimumContentRegion.X });
        if (child)
            _tabs.Draw(_mod);
    }

    public void Dispose()
    {
        _selection.Unsubscribe(OnSelectionChange);
        _header.Dispose();
    }

    private bool _valid;
    private Mod  _mod = null!;

    private void OnSelectionChange(in ModSelection.Arguments arguments)
    {
        _resetCursor = true;
        if (arguments.NewSelection is null || _selection.Mod is null)
        {
            _valid = false;
        }
        else
        {
            _valid = true;
            _mod   = arguments.NewSelection;
            _header.ChangeMod(_mod);
            _tabs.Settings.Reset();
            _tabs.Edit.Reset();
        }
    }
}
