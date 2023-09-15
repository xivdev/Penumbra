using System.Diagnostics.CodeAnalysis;
using Penumbra.Api;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.Mods;
using Penumbra.Services;
using Penumbra.String;

namespace Penumbra.Collections.Manager;

public class TempCollectionManager : IDisposable
{
    public          int                   GlobalChangeCounter { get; private set; } = 0;
    public readonly IndividualCollections Collections;

    private readonly CommunicatorService               _communicator;
    private readonly CollectionStorage                 _storage;
    private readonly ActorService                      _actors;
    private readonly Dictionary<string, ModCollection> _customCollections = new();

    public TempCollectionManager(Configuration config, CommunicatorService communicator, ActorService actors, CollectionStorage storage)
    {
        _communicator = communicator;
        _actors       = actors;
        _storage      = storage;
        Collections   = new IndividualCollections(actors, config, true);

        _communicator.TemporaryGlobalModChange.Subscribe(OnGlobalModChange, TemporaryGlobalModChange.Priority.TempCollectionManager);
    }

    public void Dispose()
    {
        _communicator.TemporaryGlobalModChange.Unsubscribe(OnGlobalModChange);
    }

    private void OnGlobalModChange(TemporaryMod mod, bool created, bool removed)
        => TempModManager.OnGlobalModChange(_customCollections.Values, mod, created, removed);

    public int Count
        => _customCollections.Count;

    public IEnumerable<ModCollection> Values
        => _customCollections.Values;

    public bool CollectionByName(string name, [NotNullWhen(true)] out ModCollection? collection)
        => _customCollections.TryGetValue(name.ToLowerInvariant(), out collection);

    public string CreateTemporaryCollection(string name)
    {
        if (_storage.ByName(name, out _))
            return string.Empty;

        if (GlobalChangeCounter == int.MaxValue)
            GlobalChangeCounter = 0;
        var collection = ModCollection.CreateTemporary(name, ~Count, GlobalChangeCounter++);
        Penumbra.Log.Debug($"Creating temporary collection {collection.AnonymizedName}.");
        if (_customCollections.TryAdd(collection.Name.ToLowerInvariant(), collection))
        {
            // Temporary collection created.
            _communicator.CollectionChange.Invoke(CollectionType.Temporary, null, collection, string.Empty);
            return collection.Name;
        }

        return string.Empty;
    }

    public bool RemoveTemporaryCollection(string collectionName)
    {
        if (!_customCollections.Remove(collectionName.ToLowerInvariant(), out var collection))
        {
            Penumbra.Log.Debug($"Tried to delete temporary collection {collectionName.ToLowerInvariant()}, but did not exist.");
            return false;
        }

        Penumbra.Log.Debug($"Deleted temporary collection {collection.AnonymizedName}.");
        GlobalChangeCounter += Math.Max(collection.ChangeCounter + 1 - GlobalChangeCounter, 0);
        for (var i = 0; i < Collections.Count; ++i)
        {
            if (Collections[i].Collection != collection)
                continue;

            // Temporary collection assignment removed.
            _communicator.CollectionChange.Invoke(CollectionType.Temporary, collection, null, Collections[i].DisplayName);
            Penumbra.Log.Verbose($"Unassigned temporary collection {collection.AnonymizedName} from {Collections[i].DisplayName}.");
            Collections.Delete(i--);
        }

        return true;
    }

    public bool AddIdentifier(ModCollection collection, params ActorIdentifier[] identifiers)
    {
        if (!Collections.Add(identifiers, collection))
            return false;

        // Temporary collection assignment added.
        Penumbra.Log.Verbose($"Assigned temporary collection {collection.AnonymizedName} to {Collections.Last().DisplayName}.");
        _communicator.CollectionChange.Invoke(CollectionType.Temporary, null, collection, Collections.Last().DisplayName);
        return true;
    }

    public bool AddIdentifier(string collectionName, params ActorIdentifier[] identifiers)
    {
        if (!_customCollections.TryGetValue(collectionName.ToLowerInvariant(), out var collection))
            return false;

        return AddIdentifier(collection, identifiers);
    }

    public bool AddIdentifier(string collectionName, string characterName, ushort worldId = ushort.MaxValue)
    {
        if (!ByteString.FromString(characterName, out var byteString, false))
            return false;

        var identifier = _actors.AwaitedService.CreatePlayer(byteString, worldId);
        if (!identifier.IsValid)
            return false;

        return AddIdentifier(collectionName, identifier);
    }

    internal bool RemoveByCharacterName(string characterName, ushort worldId = ushort.MaxValue)
    {
        if (!ByteString.FromString(characterName, out var byteString, false))
            return false;

        var identifier = _actors.AwaitedService.CreatePlayer(byteString, worldId);
        return Collections.TryGetValue(identifier, out var collection) && RemoveTemporaryCollection(collection.Name);
    }
}
