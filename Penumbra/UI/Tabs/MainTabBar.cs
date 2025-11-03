using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Services;
using Penumbra.UI.Tabs.Debug;
using Watcher = Penumbra.UI.ResourceWatcher.ResourceWatcher;

namespace Penumbra.UI.Tabs;

public sealed class MainTabBar : TabBar<TabType>, IDisposable
{
    public readonly  ModsTab         Mods;
    private readonly EphemeralConfig _config;
    private readonly SelectTab       _selectTab;

    public MainTabBar(Logger log,
        SettingsTab settings,
        ModsTab mods,
        CollectionsTab collections,
        ChangedItemsTab changedItems,
        EffectiveTab effectiveChanges,
        DebugTab debug,
        ResourceTab resources,
        Watcher watcher,
        OnScreenTab onScreen,
        MessagesTab messages, EphemeralConfig config, CommunicatorService communicator)
        : base(nameof(MainTabBar), log, settings, collections, mods, changedItems, effectiveChanges, onScreen,
            resources, watcher, debug, messages)
    {
        Mods       = mods;
        _config    = config;
        _selectTab = communicator.SelectTab;

        _selectTab.Subscribe(OnSelectTab, SelectTab.Priority.MainTabBar);
        TabSelected.Subscribe(OnTabSelected, 0);
    }

    private void OnSelectTab(in SelectTab.Arguments arguments)
    {
        NextTab = arguments.Tab;
        if (arguments.Mod is not null)
            Mods.SelectMod = arguments.Mod;
    }

    public void Dispose()
    {
        _selectTab.Unsubscribe(OnSelectTab);
    }

    private void OnTabSelected(in TabType type)
    {
        _config.SelectedTab = type;
        _config.Save();
    }
}
