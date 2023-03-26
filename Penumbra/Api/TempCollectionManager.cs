using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.Mods;
using Penumbra.Services;
using Penumbra.String;

namespace Penumbra.Api;

public class TempCollectionManager : IDisposable
{
    public          int                   GlobalChangeCounter { get; private set; } = 0;
    public readonly IndividualCollections Collections;

    private readonly CommunicatorService               _communicator;
    private readonly Dictionary<string, ModCollection> _customCollections = new();

    public TempCollectionManager(CommunicatorService communicator, IndividualCollections collections)
    {
        _communicator = communicator;
        Collections   = collections;

        _communicator.TemporaryGlobalModChange.Event += OnGlobalModChange;
    }

    public void Dispose()
    {
        _communicator.TemporaryGlobalModChange.Event -= OnGlobalModChange;
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
        if (Penumbra.CollectionManager.ByName(name, out _))
            return string.Empty;

        if (GlobalChangeCounter == int.MaxValue)
            GlobalChangeCounter = 0;
        var collection = ModCollection.CreateNewTemporary(name, GlobalChangeCounter++);
        if (_customCollections.TryAdd(collection.Name.ToLowerInvariant(), collection))
            return collection.Name;

        collection.ClearCache();
        return string.Empty;
    }

    public bool RemoveTemporaryCollection(string collectionName)
    {
        if (!_customCollections.Remove(collectionName.ToLowerInvariant(), out var collection))
            return false;

        GlobalChangeCounter += Math.Max(collection.ChangeCounter + 1 - GlobalChangeCounter, 0);
        collection.ClearCache();
        for (var i = 0; i < Collections.Count; ++i)
        {
            if (Collections[i].Collection != collection)
                continue;

            _communicator.CollectionChange.Invoke(CollectionType.Temporary, collection, null, Collections[i].DisplayName);
            Collections.Delete(i--);
        }

        return true;
    }

    public bool AddIdentifier(ModCollection collection, params ActorIdentifier[] identifiers)
    {
        if (Collections.Add(identifiers, collection))
        {
            _communicator.CollectionChange.Invoke(CollectionType.Temporary, null, collection, Collections.Last().DisplayName);
            return true;
        }

        return false;
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

        var identifier = Penumbra.Actors.CreatePlayer(byteString, worldId);
        if (!identifier.IsValid)
            return false;

        return AddIdentifier(collectionName, identifier);
    }

    internal bool RemoveByCharacterName(string characterName, ushort worldId = ushort.MaxValue)
    {
        if (!ByteString.FromString(characterName, out var byteString, false))
            return false;

        var identifier = Penumbra.Actors.CreatePlayer(byteString, worldId);
        return Collections.Individuals.TryGetValue(identifier, out var collection) && RemoveTemporaryCollection(collection.Name);
    }
}
