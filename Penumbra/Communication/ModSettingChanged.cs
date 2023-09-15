using OtterGui.Classes;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Mods;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever a mod setting is changed.
/// <list type="number">
///     <item>Parameter is the collection in which the setting was changed. </item>
///     <item>Parameter is the type of change. </item>
///     <item>Parameter is the mod the setting was changed for, unless it was a multi-change. </item>
///     <item>Parameter is the old value of the setting before the change as int. </item>
///     <item>Parameter is the index of the changed group if the change type is Setting. </item>
///     <item>Parameter is whether the change was inherited from another collection. </item>
/// </list>
/// </summary>
public sealed class ModSettingChanged : EventWrapper<Action<ModCollection, ModSettingChange, Mod?, int, int, bool>, ModSettingChanged.Priority>
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.OnModSettingChange"/>
        Api = int.MinValue,

        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModSettingChange"/>
        CollectionCacheManager = 0,

        /// <seealso cref="UI.AdvancedWindow.ItemSwapTab.OnSettingChange"/>
        ItemSwapTab = 0,

        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.OnSettingChange"/>
        ModFileSystemSelector = 0,
    }

    public ModSettingChanged()
        : base(nameof(ModSettingChanged))
    { }

    public void Invoke(ModCollection collection, ModSettingChange type, Mod? mod, int oldValue, int groupIdx, bool inherited)
        => Invoke(this, collection, type, mod, oldValue, groupIdx, inherited);
}
