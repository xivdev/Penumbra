using System;
using System.Collections;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Collections;
using Penumbra.GameData.Actors;

namespace Penumbra.Interop.Resolver;

public unsafe class IdentifiedCollectionCache : IDisposable, IEnumerable< (IntPtr Address, ActorIdentifier Identifier, ModCollection Collection) >
{
    private readonly Dictionary< IntPtr, (ActorIdentifier, ModCollection) > _cache   = new(317);
    private          bool                                                   _dirty   = false;
    private          bool                                                   _enabled = false;

    public IdentifiedCollectionCache()
    {
        SignatureHelper.Initialise( this );
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
        _characterDtorHook.Enable();
        _enabled = true;
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
        _characterDtorHook.Disable();
        _enabled = false;
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
        _characterDtorHook.Dispose();
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

    private delegate void CharacterDestructorDelegate( Character* character );

    [Signature( "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8D 05 ?? ?? ?? ?? 48 8B D9 48 89 01 48 8D 05 ?? ?? ?? ?? 48 89 81 ?? ?? ?? ?? 48 8D 05",
        DetourName = nameof( CharacterDestructorDetour ) )]
    private Hook< CharacterDestructorDelegate > _characterDtorHook = null!;

    private void CharacterDestructorDetour( Character* character )
    {
        _cache.Remove( ( IntPtr )character );
        _characterDtorHook.Original( character );
    }
}