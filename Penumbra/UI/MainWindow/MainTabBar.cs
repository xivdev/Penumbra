using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.ModsTab;
using Penumbra.UI.Tabs;
using Penumbra.UI.Tabs.Debug;
using Watcher = Penumbra.UI.ResourceWatcher.ResourceWatcher;

namespace Penumbra.UI.MainWindow;

public sealed class MainTabBar : TabBar<TabType>, IDisposable
{
    private readonly ModFileSystem   _modFileSystem;
    private readonly EphemeralConfig _config;
    private readonly SelectTab       _selectTab;

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
        CommunicatorService communicator, 
        ModFileSystem modFileSystem)
        : base(nameof(MainTabBar), log, settings, collections, mods, changedItems, effectiveChanges, onScreen,
            resources, watcher, debug, messages, management)
    {
        _config        = config;
        _modFileSystem = modFileSystem;
        _selectTab     = communicator.SelectTab;

        _selectTab.Subscribe(OnSelectTab, SelectTab.Priority.MainTabBar);
        TabSelected.Subscribe(OnTabSelected, 0);
    }

    private void OnSelectTab(in SelectTab.Arguments arguments)
    {
        NextTab = arguments.Tab;
        if (arguments.Mod?.Node is { } node)
            _modFileSystem.Selection.Select(node, true);
    }

    public void Dispose()
    {
        _selectTab.Unsubscribe(OnSelectTab);
    }

    private void OnTabSelected(in TabType type)
    {
        if (_config.SelectedTab == type)
            return;

        _config.SelectedTab = type;
        _config.Save();
    }
}
