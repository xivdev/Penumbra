using System;
using ImGuiNET;
using OtterGui.Widgets;
using Penumbra.Api.Enums;

namespace Penumbra.UI.Tabs;

public class ConfigTabBar
{
    public readonly SettingsTab     Settings;
    public readonly ModsTab         Mods;
    public readonly CollectionsTab  Collections;
    public readonly ChangedItemsTab ChangedItems;
    public readonly EffectiveTab    Effective;
    public readonly DebugTab        Debug;
    public readonly ResourceTab     Resource;
    public readonly ResourceWatcher Watcher;
    public readonly OnScreenTab     OnScreenTab;

    public readonly ITab[] Tabs;

    /// <summary> The tab to select on the next Draw call, if any. </summary>
    public TabType SelectTab = TabType.None;

    public ConfigTabBar(SettingsTab settings, ModsTab mods, CollectionsTab collections, ChangedItemsTab changedItems, EffectiveTab effective,
        DebugTab debug, ResourceTab resource, ResourceWatcher watcher, OnScreenTab onScreenTab)
    {
        Settings     = settings;
        Mods         = mods;
        Collections  = collections;
        ChangedItems = changedItems;
        Effective    = effective;
        Debug        = debug;
        Resource     = resource;
        Watcher      = watcher;
        OnScreenTab  = onScreenTab;
        Tabs = new ITab[]
        {
            Settings,
            Mods,
            Collections,
            ChangedItems,
            Effective,
            OnScreenTab,
            Debug,
            Resource,
            Watcher,
        };
    }

    public void Draw()
    {
        if (TabBar.Draw(string.Empty, ImGuiTabBarFlags.NoTooltip, ToLabel(SelectTab), out _, () => { }, Tabs))
            SelectTab = TabType.None;
    }

    private ReadOnlySpan<byte> ToLabel(TabType type)
        => type switch
        {
            TabType.Settings         => Settings.Label,
            TabType.Mods             => Mods.Label,
            TabType.Collections      => Collections.Label,
            TabType.ChangedItems     => ChangedItems.Label,
            TabType.EffectiveChanges => Effective.Label,
            TabType.OnScreen         => OnScreenTab.Label,
            TabType.ResourceWatcher  => Watcher.Label,
            TabType.Debug            => Debug.Label,
            TabType.ResourceManager  => Resource.Label,
            _                        => ReadOnlySpan<byte>.Empty,
        };
}
