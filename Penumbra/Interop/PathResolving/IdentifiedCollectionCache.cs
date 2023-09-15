using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.Interop.Services;
using Penumbra.Services;

namespace Penumbra.Interop.PathResolving;

public unsafe class IdentifiedCollectionCache : IDisposable, IEnumerable<(nint Address, ActorIdentifier Identifier, ModCollection Collection)>
{
    private readonly CommunicatorService                                _communicator;
    private readonly GameEventManager                                   _events;
    private readonly IClientState                                       _clientState;
    private readonly Dictionary<nint, (ActorIdentifier, ModCollection)> _cache = new(317);
    private          bool                                               _dirty;

    public IdentifiedCollectionCache(IClientState clientState, CommunicatorService communicator, GameEventManager events)
    {
        _clientState  = clientState;
        _communicator = communicator;
        _events       = events;

        _communicator.CollectionChange.Subscribe(CollectionChangeClear, CollectionChange.Priority.IdentifiedCollectionCache);
        _clientState.TerritoryChanged        += TerritoryClear;
        _events.CharacterDestructor          += OnCharacterDestruct;
    }

    public ResolveData Set(ModCollection collection, ActorIdentifier identifier, GameObject* data)
    {
        if (_dirty)
        {
            _dirty = false;
            _cache.Clear();
        }

        _cache[(nint)data] = (identifier, collection);
        return collection.ToResolveData(data);
    }

    public bool TryGetValue(GameObject* gameObject, out ResolveData resolve)
    {
        if (_dirty)
        {
            _dirty = false;
            _cache.Clear();
        }
        else if (_cache.TryGetValue((nint)gameObject, out var p))
        {
            resolve = p.Item2.ToResolveData(gameObject);
            return true;
        }

        resolve = default;
        return false;
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(CollectionChangeClear);
        _clientState.TerritoryChanged        -= TerritoryClear;
        _events.CharacterDestructor          -= OnCharacterDestruct;
    }

    public IEnumerator<(nint Address, ActorIdentifier Identifier, ModCollection Collection)> GetEnumerator()
    {
        foreach (var (address, (identifier, collection)) in _cache)
        {
            if (_dirty)
                yield break;

            yield return (address, identifier, collection);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private void CollectionChangeClear(CollectionType type, ModCollection? _1, ModCollection? _2, string _3)
    {
        if (type is not (CollectionType.Current or CollectionType.Interface or CollectionType.Inactive))
            _dirty = _cache.Count > 0;
    }

    private void TerritoryClear(object? _1, ushort _2)
        => _dirty = _cache.Count > 0;

    private void OnCharacterDestruct(Character* character)
        => _cache.Remove((nint)character);
}
