using Luna;
using Penumbra.Api.Api;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;

namespace Penumbra.Communication;

/// <summary> Triggered whenever an option of a mod is changed inside the mod. </summary>
public sealed class ModOptionChanged(Logger log)
    : EventBase<ModOptionChanged.Arguments, ModOptionChanged.Priority>(nameof(ModOptionChanged), log)
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

    /// <summary> The arguments for a ModOptionChanged event. </summary>
    /// <param name="Type"> The type of option change for the mod. </param>
    /// <param name="Mod"> The changed mod. </param>
    /// <param name="Group"> The changed group inside the mod, if any. </param>
    /// <param name="Option"> The changed option inside the group or null if it does not concern a specific option. </param>
    /// <param name="Container"> The changed data container inside the group or null if it does not concern a specific data container. </param>
    /// <param name="DeletedIndex"> The index of the group or option moved or deleted from. </param>
    public readonly record struct Arguments(
        ModOptionChangeType Type,
        Mod Mod,
        IModGroup? Group,
        IModOption? Option,
        IModDataContainer? Container,
        int DeletedIndex);
}
