using OtterGui.Classes;
using Penumbra.Api;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever an option of a mod is changed inside the mod.
/// <list type="number">
///     <item>Parameter is the type option change. </item>
///     <item>Parameter is the changed mod. </item>
///     <item>Parameter is the index of the changed group inside the mod. </item>
///     <item>Parameter is the index of the changed option inside the group or -1 if it does not concern a specific option. </item>
///     <item>Parameter is the index of the group an option was moved to. </item>
/// </list> </summary>
public sealed class ModOptionChanged()
    : EventWrapper<ModOptionChangeType, Mod, int, int, int, ModOptionChanged.Priority>(nameof(ModOptionChanged))
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.OnModOptionEdited"/>
        Api = int.MinValue,

        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModOptionChange"/>
        CollectionCacheManager = -100,

        /// <seealso cref="Mods.Manager.ModCacheManager.OnModOptionChange"/>
        ModCacheManager = 0,

        /// <seealso cref="UI.AdvancedWindow.ItemSwapTab.OnModOptionChange"/>
        ItemSwapTab = 0,

        /// <seealso cref="Collections.Manager.CollectionStorage.OnModOptionChange"/>
        CollectionStorage = 100,
    }
}
