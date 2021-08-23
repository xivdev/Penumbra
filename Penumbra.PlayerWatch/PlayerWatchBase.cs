using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using Penumbra.GameData.Structs;

namespace Penumbra.PlayerWatch
{
    internal class PlayerWatchBase : IDisposable
    {
        public const  int GPosePlayerActorIdx = 201;
        public const  int GPoseActorEnd       = GPosePlayerActorIdx + 48;
        private const int ActorsPerFrame      = 8;

        private readonly  DalamudPluginInterface                                           _pi;
        internal readonly HashSet< PlayerWatcher >                                         RegisteredWatchers = new();
        internal readonly Dictionary< string, (ActorEquipment, HashSet< PlayerWatcher >) > Equip              = new();
        private           int                                                              _frameTicker;
        private           bool                                                             _inGPose = false;
        private           bool                                                             _enabled = false;
        private           bool                                                             _cancel  = false;

        internal PlayerWatchBase( DalamudPluginInterface pi )
            => _pi = pi;

        internal void RegisterWatcher( PlayerWatcher watcher )
        {
            RegisteredWatchers.Add( watcher );
            if( watcher.Active )
            {
                EnableActorWatch();
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
                EnableActorWatch();
            }
            else
            {
                DisableActorWatch();
            }
        }

        internal ActorEquipment UpdateActorWithoutEvent( Actor actor )
        {
            var equipment = new ActorEquipment( actor );
            if( Equip.ContainsKey( actor.Name ) )
            {
                Equip[ actor.Name ] = ( equipment, Equip[ actor.Name ].Item2 );
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
                Equip[ playerName ] = ( new ActorEquipment(), new HashSet< PlayerWatcher > { watcher } );
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

        internal void EnableActorWatch()
        {
            if( !_enabled )
            {
                _enabled                         =  true;
                _pi.Framework.OnUpdateEvent      += OnFrameworkUpdate;
                _pi.ClientState.TerritoryChanged += OnTerritoryChange;
                _pi.ClientState.OnLogout         += OnLogout;
            }
        }

        internal void DisableActorWatch()
        {
            if( _enabled )
            {
                _enabled                         =  false;
                _pi.Framework.OnUpdateEvent      -= OnFrameworkUpdate;
                _pi.ClientState.TerritoryChanged -= OnTerritoryChange;
                _pi.ClientState.OnLogout         -= OnLogout;
            }
        }

        public void Dispose()
            => DisableActorWatch();

        private void OnTerritoryChange( object _1, ushort _2 )
            => Clear();

        private void OnLogout( object _1, object _2 )
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

        private static void TriggerEvents( IEnumerable< PlayerWatcher > watchers, Actor actor )
        {
            PluginLog.Debug( "Triggering events for {ActorName} at {Address}.", actor.Name, actor.Address );
            foreach( var watcher in watchers.Where( w => w.Active ) )
            {
                watcher.Trigger( actor );
            }
        }

        internal void TriggerGPose()
        {
            for( var i = GPosePlayerActorIdx; i < GPoseActorEnd; ++i )
            {
                var actor = _pi.ClientState.Actors[ i ];
                if( actor == null )
                {
                    return;
                }

                if( Equip.TryGetValue( actor.Name, out var watcher ) )
                {
                    TriggerEvents( watcher.Item2, actor );
                }
            }
        }

        private Actor CheckGPoseActor( Actor actor )
        {
            if( !_inGPose )
            {
                return actor;
            }

            for( var i = GPosePlayerActorIdx; i < GPoseActorEnd; ++i )
            {
                var a = _pi.ClientState.Actors[ i ];
                if( a == null )
                {
                    return actor;
                }

                if( a.Name == actor.Name )
                {
                    return a;
                }
            }

            return actor;
        }

        private void OnFrameworkUpdate( object framework )
        {
            if( _pi.ClientState.LocalPlayer == null )
            {
                return;
            }

            var actors = _pi.ClientState.Actors;

            var newInGPose = actors[ GPosePlayerActorIdx ] != null;

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

            for( var i = 0; i < ActorsPerFrame; ++i )
            {
                if( _pi.ClientState.LocalPlayer == null )
                {
                    return;
                }

                _frameTicker = _frameTicker < actors.Length - 2
                    ? _frameTicker + 2
                    : 0;

                var actor = actors[ _frameTicker ];
                if( actor             == null
                 || actor.ObjectKind  != ObjectKind.Player
                 || actor.Name        == null
                 || actor.Name.Length == 0
                 || !Equip.TryGetValue( actor.Name, out var equip ) )
                {
                    continue;
                }

                actor = CheckGPoseActor( actor );

                if( _cancel )
                {
                    _cancel = false;
                    return;
                }

                PluginLog.Verbose( "Comparing Gear for {ActorName} at {Address}...", actor.Name, actor.Address );
                if( !equip.Item1.CompareAndUpdate( actor ) )
                {
                    TriggerEvents( equip.Item2, actor );
                }
            }
        }
    }
}