using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Penumbra.GameData.Structs;

namespace Penumbra.PlayerWatch;

internal readonly struct WatchedPlayer
{
    public readonly Dictionary< ulong, CharacterEquipment > FoundActors;
    public readonly HashSet< PlayerWatcher >                RegisteredWatchers;

    public WatchedPlayer( PlayerWatcher watcher )
    {
        FoundActors        = new Dictionary< ulong, CharacterEquipment >( 4 );
        RegisteredWatchers = new HashSet< PlayerWatcher > { watcher };
    }
}

internal class PlayerWatchBase : IDisposable
{
    public const  int GPosePlayerIdx  = 201;
    public const  int GPoseTableEnd   = GPosePlayerIdx + 40;
    private const int ObjectsPerFrame = 32;

    private readonly  Framework                           _framework;
    private readonly  ClientState                         _clientState;
    private readonly  ObjectTable                         _objects;
    internal readonly HashSet< PlayerWatcher >            RegisteredWatchers = new();
    internal readonly Dictionary< string, WatchedPlayer > Equip              = new();
    internal          HashSet< ulong >                    SeenActors;
    private           int                                 _frameTicker;
    private           bool                                _inGPose;
    private           bool                                _enabled;
    private           bool                                _cancel;

    internal PlayerWatchBase( Framework framework, ClientState clientState, ObjectTable objects )
    {
        _framework   = framework;
        _clientState = clientState;
        _objects     = objects;
        SeenActors   = new HashSet< ulong >( _objects.Length );
    }

    internal void RegisterWatcher( PlayerWatcher watcher )
    {
        RegisteredWatchers.Add( watcher );
        if( watcher.Active )
        {
            EnablePlayerWatch();
        }
    }

    internal void UnregisterWatcher( PlayerWatcher watcher )
    {
        if( RegisteredWatchers.Remove( watcher ) )
        {
            foreach( var (key, value) in Equip.ToArray() )
            {
                if( value.RegisteredWatchers.Remove( watcher ) && value.RegisteredWatchers.Count == 0 )
                {
                    Equip.Remove( key );
                }
            }
        }

        CheckActiveStatus();
    }

    internal void CheckActiveStatus()
    {
        if( RegisteredWatchers.Any( w => w.Active ) )
        {
            EnablePlayerWatch();
        }
        else
        {
            DisablePlayerWatch();
        }
    }

    private static ulong GetId( GameObject actor )
        => actor.ObjectId | ( ( ulong )actor.OwnerId << 32 );

    internal CharacterEquipment UpdatePlayerWithoutEvent( Character actor )
    {
        var name      = actor.Name.ToString();
        var equipment = new CharacterEquipment( actor );
        if( Equip.TryGetValue( name, out var watched ) )
        {
            watched.FoundActors[ GetId( actor ) ] = equipment;
        }

        return equipment;
    }

    internal void AddPlayerToWatch( string playerName, PlayerWatcher watcher )
    {
        if( Equip.TryGetValue( playerName, out var items ) )
        {
            items.RegisteredWatchers.Add( watcher );
        }
        else
        {
            Equip[ playerName ] = new WatchedPlayer( watcher );
        }
    }

    public void RemovePlayerFromWatch( string playerName, PlayerWatcher watcher )
    {
        if( Equip.TryGetValue( playerName, out var items ) )
        {
            if( items.RegisteredWatchers.Remove( watcher ) && items.RegisteredWatchers.Count == 0 )
            {
                Equip.Remove( playerName );
            }
        }
    }

    internal void EnablePlayerWatch()
    {
        if( !_enabled )
        {
            _enabled                      =  true;
            _framework.Update             += OnFrameworkUpdate;
            _clientState.TerritoryChanged += OnTerritoryChange;
            _clientState.Logout           += OnLogout;
        }
    }

    internal void DisablePlayerWatch()
    {
        if( _enabled )
        {
            _enabled                      =  false;
            _framework.Update             -= OnFrameworkUpdate;
            _clientState.TerritoryChanged -= OnTerritoryChange;
            _clientState.Logout           -= OnLogout;
        }
    }

    public void Dispose()
        => DisablePlayerWatch();

    private void OnTerritoryChange( object? _1, ushort _2 )
        => Clear();

    private void OnLogout( object? _1, object? _2 )
        => Clear();

    internal void Clear()
    {
        PluginLog.Debug( "Clearing PlayerWatcher Store." );
        _cancel = true;
        foreach( var kvp in Equip )
        {
            kvp.Value.FoundActors.Clear();
        }

        _frameTicker = 0;
    }

    private static void TriggerEvents( IEnumerable< PlayerWatcher > watchers, Character player )
    {
        PluginLog.Debug( "Triggering events for {PlayerName} at {Address}.", player.Name, player.Address );
        foreach( var watcher in watchers.Where( w => w.Active ) )
        {
            watcher.Trigger( player );
        }
    }

    internal void TriggerGPose()
    {
        for( var i = GPosePlayerIdx; i < GPoseTableEnd; ++i )
        {
            var player = _objects[ i ];
            if( player == null )
            {
                return;
            }

            if( Equip.TryGetValue( player.Name.ToString(), out var watcher ) )
            {
                TriggerEvents( watcher.RegisteredWatchers, ( Character )player );
            }
        }
    }

    private Character? CheckGPoseObject( GameObject player )
    {
        if( !_inGPose )
        {
            return CharacterFactory.Convert( player );
        }

        for( var i = GPosePlayerIdx; i < GPoseTableEnd; ++i )
        {
            var a = _objects[ i ];
            if( a == null )
            {
                return CharacterFactory.Convert( player );
            }

            if( a.Name == player.Name )
            {
                return CharacterFactory.Convert( a );
            }
        }

        return CharacterFactory.Convert( player )!;
    }

    private bool TryGetPlayer( GameObject gameObject, out WatchedPlayer watch )
    {
        watch = default;
        var name = gameObject.Name.ToString();
        return name.Length != 0 && Equip.TryGetValue( name, out watch );
    }

    private static bool InvalidObjectKind( ObjectKind kind )
    {
        return kind switch
        {
            ObjectKind.BattleNpc => false,
            ObjectKind.EventNpc  => false,
            ObjectKind.Player    => false,
            ObjectKind.Retainer  => false,
            _                    => true,
        };
    }

    private GameObject? GetNextObject()
    {
        if( _frameTicker == GPosePlayerIdx - 1 )
        {
            _frameTicker = GPoseTableEnd;
        }
        else if( _frameTicker == _objects.Length - 1 )
        {
            _frameTicker = 0;
            foreach( var (_, equip) in Equip.Values.SelectMany( d => d.FoundActors.Where( p => !SeenActors.Contains( p.Key ) ) ) )
            {
                equip.Clear();
            }

            SeenActors.Clear();
        }
        else
        {
            ++_frameTicker;
        }

        return _objects[ _frameTicker ];
    }

    private void OnFrameworkUpdate( object framework )
    {
        var newInGPose = _objects[ GPosePlayerIdx ] != null;

        if( newInGPose != _inGPose )
        {
            if( newInGPose )
            {
                TriggerGPose();
            }
            else
            {
                Clear();
            }

            _inGPose = newInGPose;
        }

        for( var i = 0; i < ObjectsPerFrame; ++i )
        {
            var actor = GetNextObject();
            if( actor == null
            || InvalidObjectKind( actor.ObjectKind )
            || !TryGetPlayer( actor, out var watch ) )
            {
                continue;
            }

            var character = CheckGPoseObject( actor );
            if( _cancel )
            {
                _cancel = false;
                return;
            }

            if( character == null || character.ModelType() != 0 )
            {
                continue;
            }

            var id = GetId( character );
            SeenActors.Add( id );

#if DEBUG
            PluginLog.Verbose( "Comparing Gear for {PlayerName:l} ({Id}) at 0x{Address:X}...", character.Name, id, character.Address.ToInt64() );
#endif

            if( !watch.FoundActors.TryGetValue( id, out var equip ) )
            {
                equip                   = new CharacterEquipment( character );
                watch.FoundActors[ id ] = equip;
                TriggerEvents( watch.RegisteredWatchers, character );
            }
            else if( !equip.CompareAndUpdate( character ) )
            {
                TriggerEvents( watch.RegisteredWatchers, character );
            }

            break; // Only one comparison per frame.
        }
    }
}