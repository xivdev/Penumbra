using Dalamud.Interface.ImGuiNotification;
using OtterGui.Classes;
using OtterGui.Extensions;
using OtterGui.Services;
using Penumbra.Communication;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Collections.Manager;

/// <summary>
/// ModCollections can inherit from an arbitrary number of other collections.
/// This is transitive, so a collection A inheriting from B also inherits from everything B inherits.
/// Circular dependencies are resolved by distinctness.
/// </summary>
public class InheritanceManager : IDisposable, IService
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

        if (potentialInheritor.Inheritance.DirectlyInheritsFrom.Contains(potentialParent))
            return ValidInheritance.Contained;

        if (potentialParent.Inheritance.FlatHierarchy.Any(c => ReferenceEquals(c, potentialInheritor)))
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
        var parent = inheritor.Inheritance.RemoveInheritanceAt(inheritor, idx);
        _saveService.QueueSave(new ModCollectionSave(_modStorage, inheritor));
        _communicator.CollectionInheritanceChanged.Invoke(inheritor, false);
        RecurseInheritanceChanges(inheritor, true);
        Penumbra.Log.Debug($"Removed {parent.Identity.AnonymizedName} from {inheritor.Identity.AnonymizedName} inheritances.");
    }

    /// <summary> Order in the inheritance list is relevant. </summary>
    public void MoveInheritance(ModCollection inheritor, int from, int to)
    {
        if (!inheritor.Inheritance.MoveInheritance(inheritor, from, to))
            return;

        _saveService.QueueSave(new ModCollectionSave(_modStorage, inheritor));
        _communicator.CollectionInheritanceChanged.Invoke(inheritor, false);
        RecurseInheritanceChanges(inheritor, true);
        Penumbra.Log.Debug($"Moved {inheritor.Identity.AnonymizedName}s inheritance {from} to {to}.");
    }

    /// <inheritdoc cref="AddInheritance(ModCollection, ModCollection)"/>
    private bool AddInheritance(ModCollection inheritor, ModCollection parent, bool invokeEvent)
    {
        if (CheckValidInheritance(inheritor, parent) != ValidInheritance.Valid)
            return false;

        inheritor.Inheritance.AddInheritance(inheritor, parent);
        if (invokeEvent)
        {
            _saveService.QueueSave(new ModCollectionSave(_modStorage, inheritor));
            _communicator.CollectionInheritanceChanged.Invoke(inheritor, false);
        }

        RecurseInheritanceChanges(inheritor, invokeEvent);

        Penumbra.Log.Debug($"Added {parent.Identity.AnonymizedName} to {inheritor.Identity.AnonymizedName} inheritances.");
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
            if (collection.Inheritance.ConsumeNames() is not { } byName)
                continue;

            var changes = false;
            foreach (var subCollectionName in byName)
            {
                if (Guid.TryParse(subCollectionName, out var guid) && _storage.ById(guid, out var subCollection))
                {
                    if (AddInheritance(collection, subCollection, false))
                        continue;

                    changes = true;
                    Penumbra.Messager.NotificationMessage(
                        $"{collection.Identity.Name} can not inherit from {subCollection.Identity.Name}, removed.",
                        NotificationType.Warning);
                }
                else if (_storage.ByName(subCollectionName, out subCollection))
                {
                    changes = true;
                    Penumbra.Log.Information($"Migrating inheritance for {collection.Identity.AnonymizedName} from name to GUID.");
                    if (AddInheritance(collection, subCollection, false))
                        continue;

                    Penumbra.Messager.NotificationMessage(
                        $"{collection.Identity.Name} can not inherit from {subCollection.Identity.Name}, removed.",
                        NotificationType.Warning);
                }
                else
                {
                    Penumbra.Messager.NotificationMessage(
                        $"Inherited collection {subCollectionName} for {collection.Identity.AnonymizedName} does not exist, it was removed.",
                        NotificationType.Warning);
                    changes = true;
                }
            }

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
            var inheritedIdx = c.Inheritance.DirectlyInheritsFrom.IndexOf(old);
            if (inheritedIdx >= 0)
                RemoveInheritance(c, inheritedIdx);

            c.Inheritance.RemoveChild(old);
        }
    }

    private void RecurseInheritanceChanges(ModCollection newInheritor, bool invokeEvent)
    {
        foreach (var inheritor in newInheritor.Inheritance.DirectlyInheritedBy)
        {
            ModCollectionInheritance.UpdateFlattenedInheritance(inheritor);
            RecurseInheritanceChanges(inheritor, invokeEvent);
            if (invokeEvent)
                _communicator.CollectionInheritanceChanged.Invoke(inheritor, true);
        }
    }
}
