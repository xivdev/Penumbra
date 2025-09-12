using Luna;
using Penumbra.Api;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.Mods;
using Penumbra.Services;
using Penumbra.String;

namespace Penumbra.Collections.Manager;

public class TempCollectionManager : IDisposable, Luna.IService
{
    public          int                   GlobalChangeCounter { get; private set; }
    public readonly IndividualCollections Collections;

    private readonly CommunicatorService             _communicator;
    private readonly CollectionStorage               _storage;
    private readonly ActorManager                    _actors;
    private readonly Dictionary<Guid, ModCollection> _customCollections = [];

    public TempCollectionManager(Configuration config, CommunicatorService communicator, ActorManager actors, CollectionStorage storage)
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

    private void OnGlobalModChange(in TemporaryGlobalModChange.Arguments arguments)
        => TempModManager.OnGlobalModChange(_customCollections.Values, arguments.Mod, arguments.NewlyCreated, arguments.Deleted);

    public int Count
        => _customCollections.Count;

    public IEnumerable<ModCollection> Values
        => _customCollections.Values;

    public bool CollectionByName(string name, [NotNullWhen(true)] out ModCollection? collection)
        => _customCollections.Values.FindFirst(c => string.Equals(name, c.Identity.Name, StringComparison.OrdinalIgnoreCase), out collection);

    public bool CollectionById(Guid id, [NotNullWhen(true)] out ModCollection? collection)
        => _customCollections.TryGetValue(id, out collection);

    public Guid CreateTemporaryCollection(string name)
    {
        if (GlobalChangeCounter == int.MaxValue)
            GlobalChangeCounter = 0;
        var collection = _storage.CreateTemporary(name, ~Count, GlobalChangeCounter++);
        Penumbra.Log.Debug($"Creating temporary collection {collection.Identity.Name} with {collection.Identity.Id}.");
        if (_customCollections.TryAdd(collection.Identity.Id, collection))
        {
            // Temporary collection created.
            _communicator.CollectionChange.Invoke(new CollectionChange.Arguments(CollectionType.Temporary, null, collection, string.Empty));
            return collection.Identity.Id;
        }

        return Guid.Empty;
    }

    public bool RemoveTemporaryCollection(Guid collectionId)
    {
        if (!_customCollections.Remove(collectionId, out var collection))
        {
            Penumbra.Log.Debug($"Tried to delete temporary collection {collectionId}, but did not exist.");
            return false;
        }

        _storage.Delete(collection);
        Penumbra.Log.Debug($"Deleted temporary collection {collection.Identity.Id}.");
        GlobalChangeCounter += Math.Max(collection.Counters.Change + 1 - GlobalChangeCounter, 0);
        for (var i = 0; i < Collections.Count; ++i)
        {
            if (Collections[i].Collection != collection)
                continue;

            // Temporary collection assignment removed.
            _communicator.CollectionChange.Invoke(new CollectionChange.Arguments(CollectionType.Temporary, collection, null, Collections[i].DisplayName));
            Penumbra.Log.Verbose($"Unassigned temporary collection {collection.Identity.Id} from {Collections[i].DisplayName}.");
            Collections.Delete(i--);
        }

        return true;
    }

    public bool AddIdentifier(ModCollection collection, params ActorIdentifier[] identifiers)
    {
        if (!Collections.Add(identifiers, collection))
            return false;

        // Temporary collection assignment added.
        Penumbra.Log.Verbose($"Assigned temporary collection {collection.Identity.AnonymizedName} to {Collections.Last().DisplayName}.");
        _communicator.CollectionChange.Invoke(new CollectionChange.Arguments(CollectionType.Temporary, null, collection, Collections.Last().DisplayName));
        return true;
    }

    public bool AddIdentifier(Guid collectionId, params ActorIdentifier[] identifiers)
    {
        if (!_customCollections.TryGetValue(collectionId, out var collection))
            return false;

        return AddIdentifier(collection, identifiers);
    }

    public bool AddIdentifier(Guid collectionId, string characterName, ushort worldId = ushort.MaxValue)
    {
        if (!ByteString.FromString(characterName, out var byteString))
            return false;

        var identifier = _actors.CreatePlayer(byteString, worldId);
        if (!identifier.IsValid)
            return false;

        return AddIdentifier(collectionId, identifier);
    }

    internal bool RemoveByCharacterName(string characterName, ushort worldId = ushort.MaxValue)
    {
        if (!ByteString.FromString(characterName, out var byteString))
            return false;

        var identifier = _actors.CreatePlayer(byteString, worldId);
        return Collections.TryGetValue(identifier, out var collection) && RemoveTemporaryCollection(collection.Identity.Id);
    }
}
