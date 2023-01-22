using System;
using System.Collections;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Collections;
using Penumbra.GameData.Actors;

namespace Penumbra.Interop.Resolver;

public unsafe class IdentifiedCollectionCache : IDisposable, IEnumerable< (IntPtr Address, ActorIdentifier Identifier, ModCollection Collection) >
{
    private readonly GameEventManager                                       _events;
    private readonly Dictionary< IntPtr, (ActorIdentifier, ModCollection) > _cache   = new(317);
    private          bool                                                   _dirty   = false;
    private          bool                                                   _enabled = false;

    public IdentifiedCollectionCache(GameEventManager events)
    {
        _events = events;
    }

    public void Enable()
    {
        if( _enabled )
        {
            return;
        }

        Penumbra.CollectionManager.CollectionChanged += CollectionChangeClear;
        Penumbra.TempMods.CollectionChanged          += CollectionChangeClear;
        Dalamud.ClientState.TerritoryChanged         += TerritoryClear;
        _events.CharacterDestructor                  += OnCharacterDestruct;
        _enabled                                     =  true;
    }

    public void Disable()
    {
        if( !_enabled )
        {
            return;
        }

        Penumbra.CollectionManager.CollectionChanged -= CollectionChangeClear;
        Penumbra.TempMods.CollectionChanged          -= CollectionChangeClear;
        Dalamud.ClientState.TerritoryChanged         -= TerritoryClear;
        _events.CharacterDestructor                  -= OnCharacterDestruct;
        _enabled                                     =  false;
    }

    public ResolveData Set( ModCollection collection, ActorIdentifier identifier, GameObject* data )
    {
        if( _dirty )
        {
            _dirty = false;
            _cache.Clear();
        }

        _cache[ ( IntPtr )data ] = ( identifier, collection );
        return collection.ToResolveData( data );
    }

    public bool TryGetValue( GameObject* gameObject, out ResolveData resolve )
    {
        if( _dirty )
        {
            _dirty = false;
            _cache.Clear();
        }
        else if( _cache.TryGetValue( ( IntPtr )gameObject, out var p ) )
        {
            resolve = p.Item2.ToResolveData( gameObject );
            return true;
        }

        resolve = default;
        return false;
    }

    public void Dispose()
    {
        Disable();
        GC.SuppressFinalize( this );
    }

    public IEnumerator< (IntPtr Address, ActorIdentifier Identifier, ModCollection Collection) > GetEnumerator()
    {
        foreach( var (address, (identifier, collection)) in _cache )
        {
            if( _dirty )
            {
                yield break;
            }

            yield return ( address, identifier, collection );
        }
    }

    ~IdentifiedCollectionCache()
        => Dispose();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private void CollectionChangeClear( CollectionType type, ModCollection? _1, ModCollection? _2, string _3 )
    {
        if( type is not (CollectionType.Current or CollectionType.Interface or CollectionType.Inactive) )
        {
            _dirty = _cache.Count > 0;
        }
    }

    private void TerritoryClear( object? _1, ushort _2 )
        => _dirty = _cache.Count > 0;

    private void OnCharacterDestruct( Character* character )
        => _cache.Remove( ( IntPtr )character );
}