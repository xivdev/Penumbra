using System;
using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.Mods;

namespace Penumbra.Communication;

/// <summary>
/// Trigger to select a tab and mod in the Config Window.
/// <list type="number">
///     <item>Parameter is the selected tab. </item>
///     <item>Parameter is the selected mod, if any. </item>
/// </list>
/// </summary>
public sealed class SelectTab : EventWrapper<Action<TabType, Mod?>, SelectTab.Priority>
{
    public enum Priority
    {
        /// <seealso cref="UI.Tabs.ConfigTabBar.OnSelectTab"/>
        ConfigTabBar = 0,
    }

    public SelectTab()
        : base(nameof(SelectTab))
    { }

    public void Invoke(TabType tab = TabType.None, Mod? mod = null)
        => Invoke(this, tab, mod);
}
