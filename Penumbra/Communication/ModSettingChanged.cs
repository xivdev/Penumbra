using OtterGui.Classes;
using Penumbra.Api.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.Mods.Settings;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever a mod setting is changed.
/// <list type="number">
///     <item>Parameter is the collection in which the setting was changed. </item>
///     <item>Parameter is the type of change. </item>
///     <item>Parameter is the mod the setting was changed for, unless it was a multi-change. </item>
///     <item>Parameter is the old value of the setting before the change as Setting. </item>
///     <item>Parameter is the index of the changed group if the change type is Setting. </item>
///     <item>Parameter is whether the change was inherited from another collection. </item>
/// </list>
/// </summary>
public sealed class ModSettingChanged()
    : EventWrapper<ModCollection, ModSettingChange, Mod?, Setting, int, bool, ModSettingChanged.Priority>(nameof(ModSettingChanged))
{
    public enum Priority
    {
        /// <seealso cref="ModSettingsApi.OnModSettingChange"/>
        Api = int.MinValue,

        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModSettingChange"/>
        CollectionCacheManager = 0,

        /// <seealso cref="UI.AdvancedWindow.ItemSwapTab.OnSettingChange"/>
        ItemSwapTab = 0,

        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.OnSettingChange"/>
        ModFileSystemSelector = 0,

        /// <seealso cref="Mods.ModSelection.OnSettingChange"/>
        ModSelection = 10,
    }
}
