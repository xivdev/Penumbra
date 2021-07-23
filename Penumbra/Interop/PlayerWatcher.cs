using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using Penumbra.Game;

namespace Penumbra.Interop
{
    public class PlayerWatcher : IDisposable
    {
        private const int ActorsPerFrame = 8;

        private readonly DalamudPluginInterface              _pi;
        private readonly Dictionary< string, CharEquipment > _equip = new();
        private          int                                 _frameTicker;
        private          IntPtr                              _lastGPoseAddress = IntPtr.Zero;

        public PlayerWatcher( DalamudPluginInterface pi )
            => _pi = pi;

        public delegate void ActorChange( Actor which );
        public event ActorChange? ActorChanged;

        public void AddPlayerToWatch( string playerName )
        {
            if( !_equip.ContainsKey( playerName ) )
            {
                _equip[ playerName ] = new CharEquipment();
            }
        }

        public void RemovePlayerFromWatch( string playerName )
        {
            _equip.Remove( playerName );
        }

        public void SetActorWatch( bool on )
        {
            if( on )
            {
                EnableActorWatch();
            }
            else
            {
                DisableActorWatch();
            }
        }

        public void EnableActorWatch()
        {
            _pi.Framework.OnUpdateEvent      += OnFrameworkUpdate;
            _pi.ClientState.TerritoryChanged += OnTerritoryChange;
            _pi.ClientState.OnLogout         += OnLogout;
        }

        public void DisableActorWatch()
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

        public void Clear()
        {
            foreach( var kvp in _equip )
            {
                kvp.Value.Clear();
            }

            _frameTicker = 0;
        }

        private void OnFrameworkUpdate( object framework )
        {
            var actors     = _pi.ClientState.Actors;
            var gPoseActor = actors[ ActorRefresher.GPosePlayerActorIdx ];
            if( gPoseActor == null )
            {
                if( _lastGPoseAddress != IntPtr.Zero && actors[ 0 ] != null && _equip.ContainsKey( actors[ 0 ].Name ) )
                {
                    ActorChanged?.Invoke( actors[ 0 ] );
                }

                _lastGPoseAddress = IntPtr.Zero;
            }
            else if( gPoseActor.Address != _lastGPoseAddress )
            {
                _lastGPoseAddress = gPoseActor.Address;
                if( _equip.ContainsKey( gPoseActor.Name ) )
                {
                    ActorChanged?.Invoke( gPoseActor );
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

                if( _equip.TryGetValue( actor.Name, out var equip ) && !equip.CompareAndUpdate( actor ) )
                {
                    ActorChanged?.Invoke( actor );
                }
            }
        }
    }
}