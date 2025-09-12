using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;

namespace Penumbra.Communication;

/// <summary> Triggered whenever collection setup is changed. </summary>
public sealed class CollectionChange(Logger log)
    : EventBase<CollectionChange.Arguments, CollectionChange.Priority>(nameof(CollectionChange), log)
{
    public enum Priority
    {
        /// <seealso cref="Api.DalamudSubstitutionProvider.OnCollectionChange"/>
        DalamudSubstitutionProvider = -3,

        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnCollectionChange"/>
        CollectionCacheManager = -2,

        /// <seealso cref="Collections.Manager.ActiveCollections.OnCollectionChange"/>
        ActiveCollections = -1,

        /// <seealso cref="Api.TempModManager.OnCollectionChange"/>
        TempModManager = 0,

        /// <seealso cref="Collections.Manager.InheritanceManager.OnCollectionChange" />
        InheritanceManager = 0,

        /// <seealso cref="global::Penumbra.Interop.PathResolving.IdentifiedCollectionCache.CollectionChangeClear" />
        IdentifiedCollectionCache = 0,

        /// <seealso cref="UI.AdvancedWindow.ItemSwapTab.OnCollectionChange" />
        ItemSwapTab = 0,

        /// <seealso cref="UI.CollectionTab.CollectionSelector.OnCollectionChange" />
        CollectionSelector = 0,

        /// <seealso cref="UI.CollectionTab.IndividualAssignmentUi.UpdateIdentifiers"/>
        IndividualAssignmentUi = 0,

        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.OnCollectionChange"/>
        ModFileSystemSelector = 0,

        /// <seealso cref="Mods.ModSelection.OnCollectionChange"/>
        ModSelection = 10,
    }

    /// <summary> The arguments for a collection change event. </summary>
    /// <param name="Type"> The type of the changed collection (<see cref="CollectionType.Inactive"/> or <see cref="CollectionType.Temporary"/> for additions or deletions). </param>
    /// <param name="OldCollection"> The old collection, or null on additions. </param>
    /// <param name="NewCollection"> The new collection, or null on deletions. </param>
    /// <param name="DisplayName"> The display name for Individual collections or an empty string otherwise. </param>
    public readonly record struct Arguments(
        CollectionType Type,
        ModCollection? OldCollection,
        ModCollection? NewCollection,
        string DisplayName);
}
