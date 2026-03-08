using Luna;
using Penumbra.Api.Enums;
using Penumbra.Mods;
using Penumbra.UI.ManagementTab;
using Penumbra.UI.ModsTab;

namespace Penumbra.Communication;

public sealed class UiNavigator : IUiService
{
    public event Action<bool>?              ToggleMainWindow;
    public event Action<TabType>?           MainTabBar;
    public event Action<ManagementTabType>? ManagementTabBar;
    public event Action<ModPanelTab>?       ModPanelTabBar;
    public event Action<Mod?>?              ModSelector;

    public void MoveTo(TabType tab)
        => MainTabBar?.Invoke(tab);

    public void MoveTo(ManagementTabType tab)
    {
        MoveTo(TabType.Management);
        ManagementTabBar?.Invoke(tab);
    }

    public void MoveTo(ModPanelTab tab)
    {
        MoveTo(TabType.Mods);
        ModPanelTabBar?.Invoke(tab);
    }

    public void MoveTo(Mod? mod)
    {
        MoveTo(TabType.Mods);
        ModSelector?.Invoke(mod);
    }

    public void SetMainWindow(bool open)
        => ToggleMainWindow?.Invoke(open);

    public void OpenTo(TabType tab)
    {
        SetMainWindow(true);
        MoveTo(tab);
    }

    public void OpenTo(ManagementTabType tab)
    {
        SetMainWindow(true);
        MoveTo(tab);
    }

    public void OpenTo(ModPanelTab tab)
    {
        SetMainWindow(true);
        MoveTo(tab);
    }

    public void OpenTo(Mod? mod)
    {
        SetMainWindow(true);
        MoveTo(mod);
    }
}
