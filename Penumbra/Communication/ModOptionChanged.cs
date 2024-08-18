using OtterGui.Classes;
using Penumbra.Api.Api;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;
using static Penumbra.Communication.ModOptionChanged;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever an option of a mod is changed inside the mod.
/// <list type="number">
///     <item>Parameter is the type option change. </item>
///     <item>Parameter is the changed mod. </item>
///     <item>Parameter is the changed group inside the mod. </item>
///     <item>Parameter is the changed option inside the group or null if it does not concern a specific option. </item>
///     <item>Parameter is the changed data container inside the group or null if it does not concern a specific data container. </item>
///     <item>Parameter is the index of the group or option moved or deleted from. </item>
/// </list> </summary>
public sealed class ModOptionChanged()
    : EventWrapper<ModOptionChangeType, Mod, IModGroup?, IModOption?, IModDataContainer?, int, Priority>(nameof(ModOptionChanged))
{
    public enum Priority
    {
        /// <seealso cref="ModSettingsApi.OnModOptionEdited"/>
        Api = int.MinValue,

        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModOptionChange"/>
        CollectionCacheManager = -100,

        /// <seealso cref="ModCacheManager.OnModOptionChange"/>
        ModCacheManager = 0,

        /// <seealso cref="UI.AdvancedWindow.ItemSwapTab.OnModOptionChange"/>
        ItemSwapTab = 0,

        /// <seealso cref="Collections.Manager.CollectionStorage.OnModOptionChange"/>
        CollectionStorage = 100,
    }
}
