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
        private const int ActorsPerFrame      = 8;

        private readonly  DalamudPluginInterface                                          _pi;
        internal readonly HashSet< PlayerWatcher >                                        RegisteredWatchers = new();
        private readonly  Dictionary< string, (CharEquipment, HashSet< PlayerWatcher >) > _equip             = new();
        private           int                                                             _frameTicker;
        private           IntPtr                                                          _lastGPoseAddress = IntPtr.Zero;

        internal PlayerWatchBase( DalamudPluginInterface pi )
        {
            _pi = pi;
            EnableActorWatch();
        }

        internal void RegisterWatcher( PlayerWatcher watcher )
        {
            RegisteredWatchers.Add( watcher );
        }

        internal void UnregisterWatcher( PlayerWatcher watcher )
        {
            if( RegisteredWatchers.Remove( watcher ) )
            {
                foreach( var items in _equip.Values )
                {
                    items.Item2.Remove( watcher );
                }
            }
        }

        internal CharEquipment UpdateActorWithoutEvent( Actor actor )
        {
            var equipment = new CharEquipment( actor );
            if( _equip.ContainsKey( actor.Name ) )
            {
                _equip[ actor.Name ] = ( equipment, _equip[ actor.Name ].Item2 );
            }

            return equipment;
        }

        internal void AddPlayerToWatch( string playerName, PlayerWatcher watcher )
        {
            if( _equip.TryGetValue( playerName, out var items ) )
            {
                items.Item2.Add( watcher );
            }
            else
            {
                _equip[ playerName ] = ( new CharEquipment(), new HashSet< PlayerWatcher > { watcher } );
            }
        }

        public void RemovePlayerFromWatch( string playerName, PlayerWatcher watcher )
        {
            if( _equip.TryGetValue( playerName, out var items ) )
            {
                items.Item2.Remove( watcher );
                if( items.Item2.Count == 0 )
                {
                    _equip.Remove( playerName );
                }
            }
        }

        internal void EnableActorWatch()
        {
            _pi.Framework.OnUpdateEvent      += OnFrameworkUpdate;
            _pi.ClientState.TerritoryChanged += OnTerritoryChange;
            _pi.ClientState.OnLogout         += OnLogout;
        }

        internal void DisableActorWatch()
        {
            _pi.Framework.OnUpdateEvent      -= OnFrameworkUpdate;
            _pi.ClientState.TerritoryChanged -= OnTerritoryChange;
            _pi.ClientState.OnLogout         -= OnLogout;
        }

        public void Dispose()
            => DisableActorWatch();

        private void OnTerritoryChange( object _1, ushort _2 )
            => Clear();

        private void OnLogout( object _1, object _2 )
            => Clear();

        internal void Clear()
        {
            foreach( var kvp in _equip )
            {
                kvp.Value.Item1.Clear();
            }

            _frameTicker = 0;
        }

        private static void TriggerEvents( IEnumerable< PlayerWatcher > watchers, Actor actor )
        {
            foreach( var watcher in watchers.Where( w => w.Active ) )
            {
                watcher.Trigger( actor );
            }
        }

        private void OnFrameworkUpdate( object framework )
        {
            var actors     = _pi.ClientState.Actors;
            var gPoseActor = actors[ GPosePlayerActorIdx ];
            if( gPoseActor == null )
            {
                if( _lastGPoseAddress != IntPtr.Zero && actors[ 0 ] != null && _equip.TryGetValue( actors[ 0 ].Name, out var player ) )
                {
                    TriggerEvents( player.Item2, actors[ 0 ] );
                }

                _lastGPoseAddress = IntPtr.Zero;
            }
            else if( gPoseActor.Address != _lastGPoseAddress )
            {
                _lastGPoseAddress = gPoseActor.Address;
                if( _equip.TryGetValue( gPoseActor.Name, out var gPose ) )
                {
                    TriggerEvents( gPose.Item2, gPoseActor );
                }
            }

            for( var i = 0; i < ActorsPerFrame; ++i )
            {
                _frameTicker = _frameTicker < actors.Length - 2
                    ? _frameTicker + 2
                    : 0;

                var actor = _frameTicker == 0 && gPoseActor != null ? gPoseActor : actors[ _frameTicker ];
                if( actor             == null
                 || actor.ObjectKind  != ObjectKind.Player
                 || actor.Name        == null
                 || actor.Name.Length == 0 )
                {
                    continue;
                }

                if( _equip.TryGetValue( actor.Name, out var equip ) && !equip.Item1.CompareAndUpdate( actor ) )
                {
                    TriggerEvents( equip.Item2, actor );
                }
            }
        }
    }
}