using System;
using Dalamud.Interface.Internal.Notifications;
using Penumbra.Services;
using Penumbra.Util;

namespace Penumbra.Collections.Manager;

public class InheritanceManager : IDisposable
{
    private readonly CollectionStorage   _storage;
    private readonly CommunicatorService _communicator;
    private readonly SaveService         _saveService;

    public InheritanceManager(CollectionStorage storage, SaveService saveService, CommunicatorService communicator)
    {
        _storage      = storage;
        _saveService  = saveService;
        _communicator = communicator;

        ApplyInheritances();
        _communicator.CollectionChange.Subscribe(OnCollectionChange);
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
    }

    /// <summary>
    /// Inheritances can not be setup before all collections are read,
    /// so this happens after reading the collections in the constructor, consuming the stored strings.
    /// </summary>
    private void ApplyInheritances()
    {
        foreach (var (collection, inheritances, changes) in _storage.ConsumeInheritanceNames())
        {
            var localChanges = changes;
            foreach (var subCollection in inheritances)
            {
                if (collection.AddInheritance(subCollection, false))
                    continue;

                localChanges = true;
                Penumbra.ChatService.NotificationMessage($"{collection.Name} can not inherit from {subCollection.Name}, removed.", "Warning",
                    NotificationType.Warning);
            }

            if (localChanges)
                _saveService.ImmediateSave(collection);
        }
    }

    private void OnCollectionChange(CollectionType collectionType, ModCollection? old, ModCollection? newCollection, string _3)
    {
        if (collectionType is not CollectionType.Inactive || old == null)
            return;

        foreach (var inheritance in old.Inheritance)
            old.ClearSubscriptions(inheritance);

        foreach (var c in _storage)
        {
            var inheritedIdx = c._inheritance.IndexOf(old);
            if (inheritedIdx >= 0)
                c.RemoveInheritance(inheritedIdx);
        }
    }
}
