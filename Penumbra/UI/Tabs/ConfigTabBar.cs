using Dalamud.Bindings.ImGui;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Services;
using Penumbra.UI.Tabs.Debug;
using Watcher = Penumbra.UI.ResourceWatcher.ResourceWatcher;

namespace Penumbra.UI.Tabs;

public class ConfigTabBar : IDisposable, Luna.IUiService
{
    private readonly CommunicatorService _communicator;

    public readonly SettingsTab     Settings;
    public readonly ModsTab         Mods;
    public readonly CollectionsTab  Collections;
    public readonly ChangedItemsTab ChangedItems;
    public readonly EffectiveTab    Effective;
    public readonly DebugTab        Debug;
    public readonly ResourceTab     Resource;
    public readonly Watcher         Watcher;
    public readonly OnScreenTab     OnScreen;
    public readonly MessagesTab     Messages;

    public readonly ITab[] Tabs;

    /// <summary> The tab to select on the next Draw call, if any. </summary>
    public TabType SelectTab = TabType.None;

    public ConfigTabBar(CommunicatorService communicator, SettingsTab settings, ModsTab mods, CollectionsTab collections,
        ChangedItemsTab changedItems, EffectiveTab effective, DebugTab debug, ResourceTab resource, Watcher watcher,
        OnScreenTab onScreen, MessagesTab messages)
    {
        _communicator = communicator;

        Settings     = settings;
        Mods         = mods;
        Collections  = collections;
        ChangedItems = changedItems;
        Effective    = effective;
        Debug        = debug;
        Resource     = resource;
        Watcher      = watcher;
        OnScreen     = onScreen;
        Messages     = messages;
        Tabs =
        [
            Settings,
            Collections,
            Mods,
            ChangedItems,
            Effective,
            OnScreen,
            Debug,
            Resource,
            Watcher,
            Messages,
        ];
        _communicator.SelectTab.Subscribe(OnSelectTab, Communication.SelectTab.Priority.ConfigTabBar);
    }

    public void Dispose()
        => _communicator.SelectTab.Unsubscribe(OnSelectTab);

    public TabType Draw()
    {
        if (TabBar.Draw(string.Empty, ImGuiTabBarFlags.NoTooltip, ToLabel(SelectTab), out var currentLabel, () => { }, Tabs))
            SelectTab = TabType.None;

        return FromLabel(currentLabel);
    }

    private ReadOnlySpan<byte> ToLabel(TabType type)
        => type switch
        {
            TabType.Settings         => Settings.Label,
            TabType.Mods             => Mods.Label,
            TabType.Collections      => Collections.Label,
            TabType.ChangedItems     => ChangedItems.Label,
            TabType.EffectiveChanges => Effective.Label,
            TabType.OnScreen         => OnScreen.Label,
            TabType.ResourceWatcher  => Watcher.Label,
            TabType.Debug            => Debug.Label,
            TabType.ResourceManager  => Resource.Label,
            TabType.Messages         => Messages.Label,
            _                        => ReadOnlySpan<byte>.Empty,
        };

    private TabType FromLabel(ReadOnlySpan<byte> label)
    {
        // @formatter:off
        if (label == Mods.Label)         return TabType.Mods;
        if (label == Collections.Label)  return TabType.Collections;
        if (label == Settings.Label)     return TabType.Settings;
        if (label == ChangedItems.Label) return TabType.ChangedItems;
        if (label == Effective.Label)    return TabType.EffectiveChanges;
        if (label == OnScreen.Label)     return TabType.OnScreen;
        if (label == Messages.Label)     return TabType.Messages;
        if (label == Watcher.Label)      return TabType.ResourceWatcher;
        if (label == Debug.Label)        return TabType.Debug;
        if (label == Resource.Label)     return TabType.ResourceManager;
        // @formatter:on
        return TabType.None;
    }

    private void OnSelectTab(in SelectTab.Arguments arguments)
    {
        SelectTab = arguments.Tab;
        if (arguments.Mod is not null)
            Mods.SelectMod = arguments.Mod;
    }
}
