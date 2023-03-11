using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using OtterGui.Filesystem;
using Penumbra.GameData.Actors;
using Penumbra.Services;
using Penumbra.String;

namespace Penumbra.Collections;

public sealed partial class IndividualCollections
{
    private readonly ActorManager                                                                                         _actorManager;
    private readonly List< (string DisplayName, IReadOnlyList< ActorIdentifier > Identifiers, ModCollection Collection) > _assignments = new();
    private readonly Dictionary< ActorIdentifier, ModCollection >                                                         _individuals = new();

    public IReadOnlyList< (string DisplayName, IReadOnlyList< ActorIdentifier > Identifiers, ModCollection Collection) > Assignments
        => _assignments;

    public IReadOnlyDictionary< ActorIdentifier, ModCollection > Individuals
        => _individuals;

    // TODO
    public IndividualCollections( ActorService actorManager )
        => _actorManager = actorManager.AwaitedService;

    public IndividualCollections(ActorManager actorManager)
        => _actorManager = actorManager;

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

                identifiers = new[] { _actorManager.CreatePlayer( playerName, homeWorld ) };
                break;
            case IdentifierType.Retainer:
                if( !ByteString.FromString( name, out var retainerName ) )
                {
                    return AddResult.Invalid;
                }

                identifiers = new[] { _actorManager.CreateRetainer( retainerName, 0 ) };
                break;
            case IdentifierType.Owned:
                if( !ByteString.FromString( name, out var ownerName ) )
                {
                    return AddResult.Invalid;
                }

                identifiers = dataIds.Select( id => _actorManager.CreateOwned( ownerName, homeWorld, kind, id ) ).ToArray();
                break;
            case IdentifierType.Npc:
                identifiers = dataIds.Select( id => _actorManager.CreateIndividual( IdentifierType.Npc, ByteString.Empty, ushort.MaxValue, kind, id ) ).ToArray();
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
            var name = manager.Data.ToName( identifier.Kind, identifier.DataId );
            var table = identifier.Kind switch
            {
                ObjectKind.BattleNpc => manager.Data.BNpcs,
                ObjectKind.EventNpc  => manager.Data.ENpcs,
                ObjectKind.Companion => manager.Data.Companions,
                ObjectKind.MountType => manager.Data.Mounts,
                ( ObjectKind )15     => manager.Data.Ornaments,
                _                    => throw new NotImplementedException(),
            };
            return table.Where( kvp => kvp.Value == name )
               .Select( kvp => manager.CreateIndividualUnchecked( identifier.Type, identifier.PlayerName, identifier.HomeWorld, identifier.Kind, kvp.Key ) ).ToArray();
        }

        return identifier.Type switch
        {
            IdentifierType.Player   => new[] { identifier.CreatePermanent() },
            IdentifierType.Special  => new[] { identifier },
            IdentifierType.Retainer => new[] { identifier.CreatePermanent() },
            IdentifierType.Owned    => CreateNpcs( _actorManager, identifier.CreatePermanent() ),
            IdentifierType.Npc      => CreateNpcs( _actorManager, identifier ),
            _                       => Array.Empty< ActorIdentifier >(),
        };
    }

    internal bool Add( ActorIdentifier[] identifiers, ModCollection collection )
    {
        if( identifiers.Length == 0 || !identifiers[ 0 ].IsValid )
        {
            return false;
        }

        var name = DisplayString( identifiers[ 0 ] );
        return Add( name, identifiers, collection );
    }

    private bool Add( string displayName, ActorIdentifier[] identifiers, ModCollection collection )
    {
        if( CanAdd( identifiers ) != AddResult.Valid
        || displayName.Length     == 0
        || _assignments.Any( a => a.DisplayName.Equals( displayName, StringComparison.OrdinalIgnoreCase ) ) )
        {
            return false;
        }

        for( var i = 0; i < identifiers.Length; ++i )
        {
            identifiers[ i ] = identifiers[ i ].CreatePermanent();
            _individuals.Add( identifiers[ i ], collection );
        }

        _assignments.Add( ( displayName, identifiers, collection ) );

        return true;
    }

    internal bool ChangeCollection( ActorIdentifier identifier, ModCollection newCollection )
        => ChangeCollection( DisplayString( identifier ), newCollection );

    internal bool ChangeCollection( string displayName, ModCollection newCollection )
        => ChangeCollection( _assignments.FindIndex( t => t.DisplayName.Equals( displayName, StringComparison.OrdinalIgnoreCase ) ), newCollection );

    internal bool ChangeCollection( int displayIndex, ModCollection newCollection )
    {
        if( displayIndex < 0 || displayIndex >= _assignments.Count || _assignments[ displayIndex ].Collection == newCollection )
        {
            return false;
        }

        _assignments[ displayIndex ] = _assignments[ displayIndex ] with { Collection = newCollection };
        foreach( var identifier in _assignments[ displayIndex ].Identifiers )
        {
            _individuals[ identifier ] = newCollection;
        }

        return true;
    }

    internal bool Delete( ActorIdentifier identifier )
        => Delete( Index( identifier ) );

    internal bool Delete( string displayName )
        => Delete( Index( displayName ) );

    internal bool Delete( int displayIndex )
    {
        if( displayIndex < 0 || displayIndex >= _assignments.Count )
        {
            return false;
        }

        var (name, identifiers, _) = _assignments[ displayIndex ];
        _assignments.RemoveAt( displayIndex );
        foreach( var identifier in identifiers )
        {
            _individuals.Remove( identifier );
        }

        return true;
    }

    internal bool Move( int from, int to )
        => _assignments.Move( from, to );

    internal int Index( string displayName )
        => _assignments.FindIndex( t => t.DisplayName.Equals( displayName, StringComparison.OrdinalIgnoreCase ) );

    internal int Index( ActorIdentifier identifier )
        => identifier.IsValid ? Index( DisplayString( identifier ) ) : -1;

    private string DisplayString( ActorIdentifier identifier )
    {
        return identifier.Type switch
        {
            IdentifierType.Player   => $"{identifier.PlayerName} ({_actorManager.Data.ToWorldName( identifier.HomeWorld )})",
            IdentifierType.Retainer => $"{identifier.PlayerName} (Retainer)",
            IdentifierType.Owned =>
                $"{identifier.PlayerName} ({_actorManager.Data.ToWorldName( identifier.HomeWorld )})'s {_actorManager.Data.ToName( identifier.Kind, identifier.DataId )}",
            IdentifierType.Npc => $"{_actorManager.Data.ToName( identifier.Kind, identifier.DataId )} ({identifier.Kind.ToName()})",
            _                  => string.Empty,
        };
    }
}