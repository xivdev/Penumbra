using System;
using Dalamud.Game.ClientState.Actors.Types;
using Penumbra.GameData.Structs;

namespace Penumbra.PlayerWatch
{
    public delegate void ActorChange( Actor actor );

    public interface IPlayerWatcherBase : IDisposable
    {
        public int Version { get; }
        public bool Valid { get; }
    }

    public interface IPlayerWatcher : IPlayerWatcherBase
    {
        public event ActorChange? ActorChanged;
        public bool Active { get; }

        public void Enable();
        public void Disable();
        public void SetStatus( bool enabled );

        public void          AddPlayerToWatch( string name );
        public void          RemovePlayerFromWatch( string playerName );
        public CharEquipment UpdateActorWithoutEvent( Actor actor );
    }
}