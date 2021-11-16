using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.GameData.Structs;

namespace Penumbra.PlayerWatch;

public class PlayerWatcher : IPlayerWatcher
{
    public int Version
        => 3;

    private static PlayerWatchBase? _playerWatch;

    public event PlayerChange? PlayerChanged;

    public bool Active { get; set; } = true;

    public bool Valid
        => _playerWatch != null;

    internal PlayerWatcher( Framework framework, ClientState clientState, ObjectTable objects )
    {
        _playerWatch ??= new PlayerWatchBase( framework, clientState, objects );
        _playerWatch.RegisterWatcher( this );
    }

    public void Enable()
        => SetStatus( true );

    public void Disable()
        => SetStatus( false );

    public void SetStatus( bool enabled )
    {
        Active = enabled && Valid;
        _playerWatch?.CheckActiveStatus();
    }

    internal void Trigger( Character actor )
        => PlayerChanged?.Invoke( actor );

    public void Dispose()
    {
        if( _playerWatch == null )
        {
            return;
        }

        Active        = false;
        PlayerChanged = null;
        _playerWatch.UnregisterWatcher( this );
        if( _playerWatch.RegisteredWatchers.Count == 0 )
        {
            _playerWatch.Dispose();
            _playerWatch = null;
        }
    }

    private void CheckValidity()
    {
        if( !Valid )
        {
            throw new Exception( $"PlayerWatch was already disposed." );
        }
    }

    public void AddPlayerToWatch( string name )
    {
        CheckValidity();
        _playerWatch!.AddPlayerToWatch( name, this );
    }

    public void RemovePlayerFromWatch( string playerName )
    {
        CheckValidity();
        _playerWatch!.RemovePlayerFromWatch( playerName, this );
    }

    public CharacterEquipment UpdatePlayerWithoutEvent( Character actor )
    {
        CheckValidity();
        return _playerWatch!.UpdatePlayerWithoutEvent( actor );
    }

    public IEnumerable< (string, (ulong, CharacterEquipment)[]) > WatchedPlayers()
    {
        CheckValidity();
        return _playerWatch!.Equip
           .Where( kvp => kvp.Value.RegisteredWatchers.Contains( this ) )
           .Select( kvp => ( kvp.Key, kvp.Value.FoundActors.Select( kvp2 => ( kvp2.Key, kvp2.Value ) ).ToArray() ) );
    }
}

public static class PlayerWatchFactory
{
    public static IPlayerWatcher Create( Framework framework, ClientState clientState, ObjectTable objects )
        => new PlayerWatcher( framework, clientState, objects );
}