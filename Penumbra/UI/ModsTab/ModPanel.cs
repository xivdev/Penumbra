using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Penumbra.Mods;
using Penumbra.Services;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.ModsTab;

public class ModPanel : IDisposable, Luna.IUiService
{
    private readonly MultiModPanel  _multiModPanel;
    private readonly ModSelection   _selection;
    private readonly ModEditWindow  _editWindow;
    private readonly ModPanelHeader _header;
    private readonly ModPanelTabBar _tabs;
    private          bool           _resetCursor;

    public ModPanel(IDalamudPluginInterface pi, ModSelection selection, ModEditWindow editWindow, ModPanelTabBar tabs,
        MultiModPanel multiModPanel, CommunicatorService communicator)
    {
        _selection     = selection;
        _editWindow    = editWindow;
        _tabs          = tabs;
        _multiModPanel = multiModPanel;
        _header        = new ModPanelHeader(pi, communicator);
        _selection.Subscribe(OnSelectionChange, ModSelection.Priority.ModPanel);
        OnSelectionChange(new ModSelection.Arguments(null, _selection.Mod));
    }

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
            ImGui.SetScrollX(0);
        }

        _header.Draw();
        ImGui.SetCursorPosX(ImGui.GetScrollX() + ImGui.GetCursorPosX());
        using var child = ImRaii.Child("Tabs",
            new Vector2(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X, ImGui.GetContentRegionAvail().Y));
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
            _editWindow.IsOpen = false;
            _valid             = false;
        }
        else
        {
            if (_editWindow.IsOpen)
                _editWindow.ChangeMod(arguments.NewSelection);
            _valid = true;
            _mod   = arguments.NewSelection;
            _header.ChangeMod(_mod);
            _tabs.Settings.Reset();
            _tabs.Edit.Reset();
        }
    }
}
