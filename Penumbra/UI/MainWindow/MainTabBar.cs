using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.UI.ModsTab;
using Penumbra.UI.Tabs;
using Penumbra.UI.Tabs.Debug;
using Watcher = Penumbra.UI.ResourceWatcher.ResourceWatcher;

namespace Penumbra.UI.MainWindow;

public sealed class MainTabBar : TabBar<TabType>, IDisposable
{
    private readonly EphemeralConfig _config;
    private readonly UiNavigator     _navigator;

    public MainTabBar(Logger log,
        SettingsTab settings,
        ModTab mods,
        CollectionsTab collections,
        ChangedItemsTab changedItems,
        EffectiveTab effectiveChanges,
        DebugTab debug,
        ResourceTab resources,
        Watcher watcher,
        OnScreenTab onScreen,
        MessagesTab messages,
        ManagementTab.ManagementTab management,
        EphemeralConfig config,
        UiNavigator navigator)
        : base(nameof(MainTabBar), log, settings, collections, mods, changedItems, effectiveChanges, onScreen,
            resources, watcher, debug, messages, management)
    {
        _config        = config;
        _navigator     = navigator;

        _navigator.MainTabBar += OnSelectTab;
        TabSelected.Subscribe(OnTabSelected, 0);
    }

    private void OnSelectTab(TabType tab)
        => NextTab = tab;

    public void Dispose()
        => _navigator.MainTabBar -= OnSelectTab;

    private void OnTabSelected(in TabType type)
    {
        if (_config.SelectedTab == type)
            return;

        _config.SelectedTab = type;
        _config.Save();
    }
}
