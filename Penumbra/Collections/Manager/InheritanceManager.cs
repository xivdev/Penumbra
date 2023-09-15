using Dalamud.Interface.Internal.Notifications;
using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Communication;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.CollectionTab;
using Penumbra.Util;

namespace Penumbra.Collections.Manager;

/// <summary>
/// ModCollections can inherit from an arbitrary number of other collections.
/// This is transitive, so a collection A inheriting from B also inherits from everything B inherits.
/// Circular dependencies are resolved by distinctness.
/// </summary>
public class InheritanceManager : IDisposable
{
    public enum ValidInheritance
    {
        Valid,

        /// <summary> Can not inherit from self </summary>
        Self,

        /// <summary> Can not inherit from the empty collection </summary>
        Empty,

        /// <summary> Already inherited from </summary>
        Contained,

        /// <summary> Inheritance would lead to a circle. </summary>
        Circle,
    }

    private readonly CollectionStorage   _storage;
    private readonly CommunicatorService _communicator;
    private readonly SaveService         _saveService;
    private readonly ModStorage          _modStorage;

    public InheritanceManager(CollectionStorage storage, SaveService saveService, CommunicatorService communicator, ModStorage modStorage)
    {
        _storage      = storage;
        _saveService  = saveService;
        _communicator = communicator;
        _modStorage   = modStorage;

        ApplyInheritances();
        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.InheritanceManager);
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
    }

    /// <summary> Check whether a collection can be inherited from. </summary>
    public static ValidInheritance CheckValidInheritance(ModCollection potentialInheritor, ModCollection? potentialParent)
    {
        if (potentialParent == null || ReferenceEquals(potentialParent, ModCollection.Empty))
            return ValidInheritance.Empty;

        if (ReferenceEquals(potentialParent, potentialInheritor))
            return ValidInheritance.Self;

        if (potentialInheritor.DirectlyInheritsFrom.Contains(potentialParent))
            return ValidInheritance.Contained;

        if (ModCollection.InheritedCollections(potentialParent).Any(c => ReferenceEquals(c, potentialInheritor)))
            return ValidInheritance.Circle;

        return ValidInheritance.Valid;
    }

    /// <summary>
    /// Add a new collection to the inheritance list.
    /// We do not check if this collection would be visited before,
    /// only that it is unique in the list itself.
    /// </summary>
    public bool AddInheritance(ModCollection inheritor, ModCollection parent)
        => AddInheritance(inheritor, parent, true);

    /// <summary> Remove an existing inheritance from a collection. </summary>
    public void RemoveInheritance(ModCollection inheritor, int idx)
    {
        var parent = inheritor.DirectlyInheritsFrom[idx];
        ((List<ModCollection>)inheritor.DirectlyInheritsFrom).RemoveAt(idx);
        ((List<ModCollection>)parent.DirectParentOf).Remove(inheritor);
        _saveService.QueueSave(new ModCollectionSave(_modStorage, inheritor));
        _communicator.CollectionInheritanceChanged.Invoke(inheritor, false);
        RecurseInheritanceChanges(inheritor);
        Penumbra.Log.Debug($"Removed {parent.AnonymizedName} from {inheritor.AnonymizedName} inheritances.");
    }

    /// <summary> Order in the inheritance list is relevant. </summary>
    public void MoveInheritance(ModCollection inheritor, int from, int to)
    {
        if (!((List<ModCollection>)inheritor.DirectlyInheritsFrom).Move(from, to))
            return;

        _saveService.QueueSave(new ModCollectionSave(_modStorage, inheritor));
        _communicator.CollectionInheritanceChanged.Invoke(inheritor, false);
        RecurseInheritanceChanges(inheritor);
        Penumbra.Log.Debug($"Moved {inheritor.AnonymizedName}s inheritance {from} to {to}.");
    }

    /// <inheritdoc cref="AddInheritance(ModCollection, ModCollection)"/>
    private bool AddInheritance(ModCollection inheritor, ModCollection parent, bool invokeEvent)
    {
        if (CheckValidInheritance(inheritor, parent) != ValidInheritance.Valid)
            return false;

        ((List<ModCollection>)inheritor.DirectlyInheritsFrom).Add(parent);
        ((List<ModCollection>)parent.DirectParentOf).Add(inheritor);
        if (invokeEvent)
        {
            _saveService.QueueSave(new ModCollectionSave(_modStorage, inheritor));
            _communicator.CollectionInheritanceChanged.Invoke(inheritor, false);
            RecurseInheritanceChanges(inheritor);
        }

        Penumbra.Log.Debug($"Added {parent.AnonymizedName} to {inheritor.AnonymizedName} inheritances.");
        return true;
    }

    /// <summary>
    /// Inheritances can not be setup before all collections are read,
    /// so this happens after reading the collections in the constructor, consuming the stored strings.
    /// </summary>
    private void ApplyInheritances()
    {
        foreach (var collection in _storage)
        {
            if (collection.InheritanceByName == null)
                continue;

            var changes = false;
            foreach (var subCollectionName in collection.InheritanceByName)
            {
                if (_storage.ByName(subCollectionName, out var subCollection))
                {
                    if (AddInheritance(collection, subCollection, false))
                        continue;

                    changes = true;
                    Penumbra.Chat.NotificationMessage($"{collection.Name} can not inherit from {subCollection.Name}, removed.", "Warning",
                        NotificationType.Warning);
                }
                else
                {
                    Penumbra.Chat.NotificationMessage(
                        $"Inherited collection {subCollectionName} for {collection.AnonymizedName} does not exist, it was removed.", "Warning",
                        NotificationType.Warning);
                    changes = true;
                }
            }

            collection.InheritanceByName = null;
            if (changes)
                _saveService.ImmediateSave(new ModCollectionSave(_modStorage, collection));
        }
    }

    private void OnCollectionChange(CollectionType collectionType, ModCollection? old, ModCollection? newCollection, string _3)
    {
        if (collectionType is not CollectionType.Inactive || old == null)
            return;

        foreach (var c in _storage)
        {
            var inheritedIdx = c.DirectlyInheritsFrom.IndexOf(old);
            if (inheritedIdx >= 0)
                RemoveInheritance(c, inheritedIdx);

            ((List<ModCollection>)c.DirectParentOf).Remove(old);
        }
    }

    private void RecurseInheritanceChanges(ModCollection newInheritor)
    {
        foreach (var inheritor in newInheritor.DirectParentOf)
        {
            _communicator.CollectionInheritanceChanged.Invoke(inheritor, true);
            RecurseInheritanceChanges(inheritor);
        }
    }
}
