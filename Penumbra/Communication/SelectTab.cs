using Luna;
using Penumbra.Api.Enums;
using Penumbra.Mods;

namespace Penumbra.Communication;

/// <summary> Trigger to select a tab and mod in the Config Window. </summary>
public sealed class SelectTab(Logger log) : EventBase<SelectTab.Arguments, SelectTab.Priority>(nameof(SelectTab), log)
{
    public enum Priority
    {
        /// <seealso cref="UI.Tabs.ConfigTabBar.OnSelectTab"/>
        ConfigTabBar = 0,
    }

    /// <summary> The arguments for a SelectTab event. </summary>
    /// <param name="Tab"> The selected tab. </param>
    /// <param name="Mod"> The selected mod, if any. </param>
    public readonly record struct Arguments(TabType Tab, Mod? Mod);
}
