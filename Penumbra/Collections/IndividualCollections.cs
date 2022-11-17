using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Penumbra.GameData.Actors;
using Penumbra.String;

namespace Penumbra.Collections;

public sealed partial class IndividualCollections
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

    public enum AddResult
    {
        Valid,
        AlreadySet,
        Invalid,
    }

    public AddResult CanAdd( params ActorIdentifier[] identifiers )
    {
        if( identifiers.Length == 0 )
        {
            return AddResult.Invalid;
        }

        if( identifiers.Any( i => !i.IsValid ) )
        {
            return AddResult.Invalid;
        }

        if( identifiers.Any( Individuals.ContainsKey ) )
        {
            return AddResult.AlreadySet;
        }

        return AddResult.Valid;
    }

    public AddResult CanAdd( IdentifierType type, string name, ushort homeWorld, ObjectKind kind, IEnumerable< uint > dataIds, out ActorIdentifier[] identifiers )
    {
        identifiers = Array.Empty< ActorIdentifier >();

        switch( type )
        {
            case IdentifierType.Player:
                if( !ByteString.FromString( name, out var playerName ) )
                {
                    return AddResult.Invalid;
                }

                var identifier = _manager.CreatePlayer( playerName, homeWorld );
                identifiers = new[] { identifier };
                break;
            case IdentifierType.Owned:
                if( !ByteString.FromString( name, out var ownerName ) )
                {
                    return AddResult.Invalid;
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
               .Select( kvp => manager.CreateIndividualUnchecked( identifier.Type, identifier.PlayerName, identifier.HomeWorld, identifier.Kind, kvp.Key ) ).ToArray();
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

    public bool Add( ActorIdentifier[] identifiers, ModCollection collection )
    {
        if( identifiers.Length == 0 || !identifiers[ 0 ].IsValid )
        {
            return false;
        }

        var name = identifiers[ 0 ].Type switch
        {
            IdentifierType.Player => $"{identifiers[ 0 ].PlayerName} ({_manager.ToWorldName( identifiers[ 0 ].HomeWorld )})",
            IdentifierType.Owned =>
                $"{identifiers[ 0 ].PlayerName} ({_manager.ToWorldName( identifiers[ 0 ].HomeWorld )})'s {_manager.ToName( identifiers[ 0 ].Kind, identifiers[ 0 ].DataId )}",
            IdentifierType.Npc => $"{_manager.ToName( identifiers[ 0 ].Kind, identifiers[ 0 ].DataId )} ({identifiers[ 0 ].Kind})",
            _                  => string.Empty,
        };
        return Add( name, identifiers, collection );
    }

    private bool Add( string displayName, ActorIdentifier[] identifiers, ModCollection collection )
    {
        if( CanAdd( identifiers ) != AddResult.Valid || displayName.Length == 0 || _assignments.ContainsKey( displayName ) )
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
}