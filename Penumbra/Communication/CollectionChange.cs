using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Collections.Manager;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever collection setup is changed.
/// <list type="number">
///     <item>Parameter is the type of the changed collection. (Inactive or Temporary for additions or deletions)</item>
///     <item>Parameter is the old collection, or null on additions.</item>
///     <item>Parameter is the new collection, or null on deletions.</item>
///     <item>Parameter is the display name for Individual collections or an empty string otherwise.</item>
/// </list> </summary>
public sealed class CollectionChange : EventWrapper<Action<CollectionType, ModCollection?, ModCollection?, string>, CollectionChange.Priority>
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

        /// <seealso cref="Interop.PathResolving.IdentifiedCollectionCache.CollectionChangeClear" />
        IdentifiedCollectionCache = 0,

        /// <seealso cref="UI.AdvancedWindow.ItemSwapTab.OnCollectionChange" />
        ItemSwapTab = 0,

        /// <seealso cref="UI.CollectionTab.CollectionSelector.OnCollectionChange" />
        CollectionSelector = 0,

        /// <seealso cref="UI.CollectionTab.IndividualAssignmentUi.UpdateIdentifiers"/>
        IndividualAssignmentUi = 0,

        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.OnCollectionChange"/>
        ModFileSystemSelector = 0,

    }

    public CollectionChange()
        : base(nameof(CollectionChange))
    { }

    public void Invoke(CollectionType collectionType, ModCollection? oldCollection, ModCollection? newCollection, string displayName)
        => Invoke(this, collectionType, oldCollection, newCollection, displayName);
}
