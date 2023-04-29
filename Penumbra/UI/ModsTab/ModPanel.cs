using System;
using Dalamud.Plugin;
using Penumbra.Mods;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.ModsTab;

public class ModPanel : IDisposable
{
    private readonly ModFileSystemSelector       _selector;
    private readonly ModEditWindow _editWindow;
    private readonly ModPanelHeader              _header;
    private readonly ModPanelTabBar              _tabs;

    public ModPanel(DalamudPluginInterface pi, ModFileSystemSelector selector, ModEditWindow editWindow, ModPanelTabBar tabs)
    {
        _selector                  =  selector;
        _editWindow                =  editWindow;
        _tabs                      =  tabs;
        _header                    =  new ModPanelHeader(pi);
        _selector.SelectionChanged += OnSelectionChange;
    }

    public void Draw()
    {
        if (!_valid)
            return;

        _header.Draw();
        _tabs.Draw(_mod);
    }

    public void Dispose()
    {
        _selector.SelectionChanged -= OnSelectionChange;
        _header.Dispose();
    }

    private bool _valid;
    private Mod  _mod = null!;

    private void OnSelectionChange(Mod? old, Mod? mod, in ModFileSystemSelector.ModState _)
    {
        if (mod == null || _selector.Selected == null)
        {
            _editWindow.IsOpen = false;
            _valid             = false;
        }
        else
        {
            if (_editWindow.IsOpen)
                _editWindow.ChangeMod(mod);
            _valid = true;
            _mod   = mod;
            _header.UpdateModData(_mod);
            _tabs.Settings.Reset();
            _tabs.Edit.Reset();
        }
    }
}
