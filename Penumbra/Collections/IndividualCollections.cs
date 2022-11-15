using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Actors;
using Penumbra.String;

namespace Penumbra.Collections;

public partial class IndividualCollections
{
    public const int Version = 1;

    internal void Migrate0To1( Dictionary< string, ModCollection > old )
    {
        foreach( var (name, collection) in old )
        {
            if( ActorManager.VerifyPlayerName( name ) )
            {
                var identifier = _manager.CreatePlayer( ByteString.FromStringUnsafe( name, false ), ushort.MaxValue );
                if( Add( name, new[] { identifier }, collection ) )
                {
                    var shortName = string.Join( " ", name.Split().Select( n => $"{n[0]}." ) );
                    Penumbra.Log.Information( $"Migrated {shortName} ({collection.AnonymizedName}) to Player Identifier." );
                    continue;
                }
            }

        }
    }
}

public sealed partial class IndividualCollections : IReadOnlyList< (string DisplayName, ModCollection Collection) >
{
    private readonly ActorManager                                                                                   _manager;
    private readonly SortedList< string, (IReadOnlyList< ActorIdentifier > Identifiers, ModCollection Collection) > _assignments = new();
    private readonly Dictionary< ActorIdentifier, ModCollection >                                                   _individuals = new();

    public IReadOnlyDictionary< string, (IReadOnlyList< ActorIdentifier > Identifiers, ModCollection Collection) > Assignments
        => _assignments;

    public IReadOnlyDictionary< ActorIdentifier, ModCollection > Individuals
        => _individuals;

    public IndividualCollections( ActorManager manager )
        => _manager = manager;

    public bool CanAdd( params ActorIdentifier[] identifiers )
        => identifiers.Length > 0 && identifiers.All( i => i.IsValid && !Individuals.ContainsKey( i ) );

    public bool CanAdd( IdentifierType type, string name, ushort homeWorld, ObjectKind kind, IEnumerable< uint > dataIds, out ActorIdentifier[] identifiers )
    {
        identifiers = Array.Empty< ActorIdentifier >();

        switch( type )
        {
            case IdentifierType.Player:
                if( !ByteString.FromString( name, out var playerName ) )
                {
                    return false;
                }

                var identifier = _manager.CreatePlayer( playerName, homeWorld );
                identifiers = new[] { identifier };
                break;
            case IdentifierType.Owned:
                if( !ByteString.FromString( name, out var ownerName ) )
                {
                    return false;
                }

                identifiers = dataIds.Select( id => _manager.CreateOwned( ownerName, homeWorld, kind, id ) ).ToArray();
                break;
            case IdentifierType.Npc:
                identifiers = dataIds.Select( id => _manager.CreateIndividual( IdentifierType.Npc, ByteString.Empty, ushort.MaxValue, kind, id ) ).ToArray();
                break;
            default:
                identifiers = Array.Empty< ActorIdentifier >();
                break;
        }

        return CanAdd( identifiers );
    }

    public ActorIdentifier[] GetGroup( ActorIdentifier identifier )
    {
        if( !identifier.IsValid )
        {
            return Array.Empty< ActorIdentifier >();
        }

        static ActorIdentifier[] CreateNpcs( ActorManager manager, ActorIdentifier identifier )
        {
            var name = manager.ToName( identifier.Kind, identifier.DataId );
            var table = identifier.Kind switch
            {
                ObjectKind.BattleNpc => manager.BNpcs,
                ObjectKind.EventNpc  => manager.ENpcs,
                ObjectKind.Companion => manager.Companions,
                ObjectKind.MountType => manager.Mounts,
                _                    => throw new NotImplementedException(),
            };
            return table.Where( kvp => kvp.Value == name )
               .Select( kvp => manager.CreateIndividual( identifier.Type, identifier.PlayerName, identifier.HomeWorld, identifier.Kind, kvp.Key ) ).ToArray();
        }

        return identifier.Type switch
        {
            IdentifierType.Player  => new[] { identifier.CreatePermanent() },
            IdentifierType.Special => new[] { identifier },
            IdentifierType.Owned   => CreateNpcs( _manager, identifier.CreatePermanent() ),
            IdentifierType.Npc     => CreateNpcs( _manager, identifier ),
            _                      => Array.Empty< ActorIdentifier >(),
        };
    }

    public bool Add( string displayName, ActorIdentifier[] identifiers, ModCollection collection )
    {
        if( !CanAdd( identifiers ) || _assignments.ContainsKey( displayName ) )
        {
            return false;
        }

        _assignments.Add( displayName, ( identifiers, collection ) );
        foreach( var identifier in identifiers )
        {
            _individuals.Add( identifier, collection );
        }

        return true;
    }

    public bool ChangeCollection( string displayName, ModCollection newCollection )
    {
        var displayIndex = _assignments.IndexOfKey( displayName );
        return ChangeCollection( displayIndex, newCollection );
    }

    public bool ChangeCollection( int displayIndex, ModCollection newCollection )
    {
        if( displayIndex < 0 || displayIndex >= _assignments.Count || _assignments.Values[ displayIndex ].Collection == newCollection )
        {
            return false;
        }

        _assignments.Values[ displayIndex ] = _assignments.Values[ displayIndex ] with { Collection = newCollection };
        foreach( var identifier in _assignments.Values[ displayIndex ].Identifiers )
        {
            _individuals[ identifier ] = newCollection;
        }

        return true;
    }

    public bool Delete( string displayName )
    {
        var displayIndex = _assignments.IndexOfKey( displayName );
        return Delete( displayIndex );
    }

    public bool Delete( int displayIndex )
    {
        if( displayIndex < 0 || displayIndex >= _assignments.Count )
        {
            return false;
        }

        var (identifiers, _) = _assignments.Values[ displayIndex ];
        _assignments.RemoveAt( displayIndex );
        foreach( var identifier in identifiers )
        {
            _individuals.Remove( identifier );
        }

        return true;
    }

    public IEnumerator< (string DisplayName, ModCollection Collection) > GetEnumerator()
        => _assignments.Select( kvp => ( kvp.Key, kvp.Value.Collection ) ).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _assignments.Count;

    public (string DisplayName, ModCollection Collection) this[ int index ]
        => ( _assignments.Keys[ index ], _assignments.Values[ index ].Collection );

    public bool TryGetCollection( ActorIdentifier identifier, out ModCollection? collection )
    {
        collection = null;
        if( !identifier.IsValid )
        {
            return false;
        }

        if( _individuals.TryGetValue( identifier, out collection ) )
        {
            return true;
        }

        if( identifier.Type is not (IdentifierType.Player or IdentifierType.Owned) )
        {
            return false;
        }

        identifier = _manager.CreateIndividual( identifier.Type, identifier.PlayerName, ushort.MaxValue, identifier.Kind, identifier.DataId );
        if( identifier.IsValid && _individuals.TryGetValue( identifier, out collection ) )
        {
            return true;
        }

        return false;
    }

    public bool TryGetCollection( GameObject? gameObject, out ModCollection? collection )
        => TryGetCollection( _manager.FromObject( gameObject ), out collection );

    public unsafe bool TryGetCollection( FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* gameObject, out ModCollection? collection )
        => TryGetCollection( _manager.FromObject( gameObject ), out collection );
}