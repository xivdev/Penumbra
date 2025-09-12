using Luna;
using Penumbra.Collections;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever a collections inheritances change.
/// <list type="number">
///     <item>Parameter is the collection whose ancestors were changed. </item>
///     <item>Parameter is whether the change was itself inherited, i.e. if it happened in a direct parent (false) or a more removed ancestor (true). </item>
/// </list>
/// </summary>
public sealed class CollectionInheritanceChanged(Logger log)
    : EventBase<CollectionInheritanceChanged.Arguments, CollectionInheritanceChanged.Priority>(nameof(CollectionInheritanceChanged), log)
{
    public enum Priority
    {
        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnCollectionInheritanceChange"/>
        CollectionCacheManager = 0,

        /// <seealso cref="UI.AdvancedWindow.ItemSwapTab.OnInheritanceChange"/>
        ItemSwapTab = 0,

        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.OnInheritanceChange"/>
        ModFileSystemSelector = 0,

        /// <seealso cref="Mods.ModSelection.OnInheritanceChange"/>
        ModSelection = 10,
    }

    /// <summary> The arguments for a collection inheritance changed event. </summary>
    /// <param name="Collection"> The collection whose ancestors were changed. </param>
    /// <param name="Inherited"> Whether the change was itself inherited, i.e. if it happened in a direct parent (false) or a more removed ancestor (true). </param>
    public readonly record struct Arguments(ModCollection Collection, bool Inherited);
}
