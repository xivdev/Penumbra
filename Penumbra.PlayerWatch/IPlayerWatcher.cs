using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.GameData.Structs;

namespace Penumbra.PlayerWatch;

public delegate void PlayerChange( Character actor );

public interface IPlayerWatcherBase : IDisposable
{
    public int Version { get; }
    public bool Valid { get; }
}

public interface IPlayerWatcher : IPlayerWatcherBase
{
    public event PlayerChange? PlayerChanged;
    public bool Active { get; }

    public void Enable();
    public void Disable();
    public void SetStatus( bool enabled );

    public void               AddPlayerToWatch( string playerName );
    public void               RemovePlayerFromWatch( string playerName );
    public CharacterEquipment UpdatePlayerWithoutEvent( Character actor );

    public IEnumerable< (string, (ulong, CharacterEquipment)[]) > WatchedPlayers();
}