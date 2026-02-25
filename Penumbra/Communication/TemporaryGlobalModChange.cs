using Luna;
using Penumbra.Mods;

namespace Penumbra.Communication;

/// <summary> Triggered whenever a temporary mod for all collections is changed. </summary>
public sealed class TemporaryGlobalModChange(Logger log)
    : EventBase<TemporaryGlobalModChange.Arguments, TemporaryGlobalModChange.Priority>(nameof(TemporaryGlobalModChange), log)
{
    public enum Priority
    {
        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnGlobalModChange"/>
        CollectionCacheManager = 0,

        /// <seealso cref="Collections.Manager.TempCollectionManager.OnGlobalModChange"/>
        TempCollectionManager = 0,
    }

    /// <summary> The arguments for a TemporaryGlobalModChange event. </summary>
    /// <param name="Mod"> The changed mod. </param>
    /// <param name="NewlyCreated"> The changed mod. </param>
    /// <param name="Deleted"> The changed mod. </param>
    public readonly record struct Arguments(TemporaryMod Mod, bool NewlyCreated, bool Deleted);
}
