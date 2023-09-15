using OtterGui.Classes;
using Penumbra.Collections;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever a collections inheritances change.
/// <list type="number">
///     <item>Parameter is the collection whose ancestors were changed. </item>
///     <item>Parameter is whether the change was itself inherited, i.e. if it happened in a direct parent (false) or a more removed ancestor (true). </item>
/// </list>
/// </summary>
public sealed class CollectionInheritanceChanged : EventWrapper<Action<ModCollection, bool>, CollectionInheritanceChanged.Priority>
{
    public enum Priority
    {
        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnCollectionInheritanceChange"/>
        CollectionCacheManager = 0,

        /// <seealso cref="UI.AdvancedWindow.ItemSwapTab.OnInheritanceChange"/>
        ItemSwapTab = 0,

        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.OnInheritanceChange"/>
        ModFileSystemSelector = 0,
    }

    public CollectionInheritanceChanged()
        : base(nameof(CollectionInheritanceChanged))
    { }

    public void Invoke(ModCollection collection, bool inherited)
        => Invoke(this, collection, inherited);
}
