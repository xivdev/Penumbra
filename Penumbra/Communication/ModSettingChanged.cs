using Luna;
using Penumbra.Api.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;

namespace Penumbra.Communication;

/// <summary> Triggered whenever a mod setting is changed. </summary>
public sealed class ModSettingChanged(Logger log)
    : EventBase<ModSettingChanged.Arguments, ModSettingChanged.Priority>(nameof(ModSettingChanged), log)
{
    public enum Priority
    {
        /// <seealso cref="ModSettingsApi.OnModSettingChange"/>
        Api = int.MinValue,

        /// <seealso cref="Mods.Manager.ModConfigUpdater.OnModSettingChanged"/>
        ModConfigUpdater = -10,

        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModSettingChange"/>
        CollectionCacheManager = 0,

        /// <seealso cref="UI.AdvancedWindow.ItemSwapTab.OnSettingChange"/>
        ItemSwapTab = 0,

        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.OnSettingChange"/>
        ModFileSystemSelector = 0,

        /// <seealso cref="Mods.ModSelection.OnSettingChange"/>
        ModSelection = 10,
    }

    /// <summary> The arguments for a ModSettingChanged event. </summary>
    /// <param name="Type"> The type of change for the mod settings. </param>
    /// <param name="Collection"> The collection in which the settings were changed, unless it was a multi-change. </param>
    /// <param name="Mod"> The changed mod. </param>
    /// <param name="OldValue"> The old value of the setting before the change. </param>
    /// <param name="GroupIndex"> The index of the changed group if the change type is Setting. </param>
    /// <param name="Inherited"> Whether the change was inherited from another collection </param>
    public readonly record struct Arguments(
        ModSettingChange Type,
        ModCollection Collection,
        Mod? Mod,
        Setting OldValue,
        int GroupIndex,
        bool Inherited);
}
