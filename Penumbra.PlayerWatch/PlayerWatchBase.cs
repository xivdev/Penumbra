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

namespace Penumbra.PlayerWatch
{
    internal class PlayerWatchBase : IDisposable
    {
        public const  int GPosePlayerIdx  = 201;
        public const  int GPoseTableEnd   = GPosePlayerIdx + 48;
        private const int ObjectsPerFrame = 8;

        private readonly  Framework                                                            _framework;
        private readonly  ClientState                                                          _clientState;
        private readonly  ObjectTable                                                          _objects;
        internal readonly HashSet< PlayerWatcher >                                             RegisteredWatchers = new();
        internal readonly Dictionary< string, (CharacterEquipment, HashSet< PlayerWatcher >) > Equip              = new();
        private           int                                                                  _frameTicker;
        private           bool                                                                 _inGPose;
        private           bool                                                                 _enabled;
        private           bool                                                                 _cancel;

        internal PlayerWatchBase( Framework framework, ClientState clientState, ObjectTable objects )
        {
            _framework   = framework;
            _clientState = clientState;
            _objects     = objects;
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
                foreach( var items in Equip.Values )
                {
                    items.Item2.Remove( watcher );
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

        internal CharacterEquipment UpdatePlayerWithoutEvent( Character actor )
        {
            var equipment = new CharacterEquipment( actor );
            if( Equip.ContainsKey( actor.Name.ToString() ) )
            {
                Equip[ actor.Name.ToString() ] = ( equipment, Equip[ actor.Name.ToString() ].Item2 );
            }

            return equipment;
        }

        internal void AddPlayerToWatch( string playerName, PlayerWatcher watcher )
        {
            if( Equip.TryGetValue( playerName, out var items ) )
            {
                items.Item2.Add( watcher );
            }
            else
            {
                Equip[ playerName ] = ( new CharacterEquipment(), new HashSet< PlayerWatcher > { watcher } );
            }
        }

        public void RemovePlayerFromWatch( string playerName, PlayerWatcher watcher )
        {
            if( Equip.TryGetValue( playerName, out var items ) )
            {
                items.Item2.Remove( watcher );
                if( items.Item2.Count == 0 )
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
                kvp.Value.Item1.Clear();
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
                    TriggerEvents( watcher.Item2, ( Character )player );
                }
            }
        }

        private Character CheckGPoseObject( GameObject player )
        {
            if( !_inGPose )
            {
                return ( Character )player;
            }

            for( var i = GPosePlayerIdx; i < GPoseTableEnd; ++i )
            {
                var a = _objects[ i ];
                if( a == null )
                {
                    return ( Character )player;
                }

                if( a.Name == player.Name )
                {
                    return ( Character )a;
                }
            }

            return ( Character )player;
        }

        private bool TryGetPlayer( GameObject gameObject, out (CharacterEquipment, HashSet< PlayerWatcher >) equip )
        {
            equip = default;
            var name = gameObject.Name.ToString();
            return name.Length != 0 && Equip.TryGetValue( name, out equip );
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
                _frameTicker = _frameTicker < GPosePlayerIdx - 2
                    ? _frameTicker + 2
                    : 0;

                var actor = _objects[ _frameTicker ];
                if( actor            == null
                 || actor.ObjectKind != ObjectKind.Player
                 || !TryGetPlayer( actor, out var equip ) )
                {
                    continue;
                }

                var character = CheckGPoseObject( actor );

                if( _cancel )
                {
                    _cancel = false;
                    return;
                }

                PluginLog.Verbose( "Comparing Gear for {PlayerName} at {Address}...", character.Name, character.Address );
                if( !equip.Item1.CompareAndUpdate( character ) )
                {
                    TriggerEvents( equip.Item2, character );
                }
            }
        }
    }
}