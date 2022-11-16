using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Internal.Notifications;
using Penumbra.GameData.Actors;
using Penumbra.String;
using Penumbra.Util;

namespace Penumbra.Collections;

public partial class IndividualCollections
{
    public const int Version = 1;

    internal void Migrate0To1( Dictionary< string, ModCollection > old )
    {
        static bool FindDataId( string name, IReadOnlyDictionary< uint, string > data, out uint dataId )
        {
            var kvp = data.FirstOrDefault( kvp => kvp.Value.Equals( name, StringComparison.OrdinalIgnoreCase ),
                new KeyValuePair< uint, string >( uint.MaxValue, string.Empty ) );
            dataId = kvp.Key;
            return kvp.Value.Length > 0;
        }

        foreach( var (name, collection) in old )
        {
            var kind      = ObjectKind.None;
            var lowerName = name.ToLowerInvariant();
            // Prefer matching NPC names, fewer false positives than preferring players.
            if( FindDataId( lowerName, _manager.Companions, out var dataId ) )
            {
                kind = ObjectKind.Companion;
            }
            else if( FindDataId( lowerName, _manager.Mounts, out dataId ) )
            {
                kind = ObjectKind.MountType;
            }
            else if( FindDataId( lowerName, _manager.BNpcs, out dataId ) )
            {
                kind = ObjectKind.BattleNpc;
            }
            else if( FindDataId( lowerName, _manager.ENpcs, out dataId ) )
            {
                kind = ObjectKind.EventNpc;
            }

            var identifier = _manager.CreateNpc( kind, dataId );
            if( identifier.IsValid )
            {
                // If the name corresponds to a valid npc, add it as a group. If this fails, notify users.
                var group = GetGroup( identifier );
                var ids   = string.Join( ", ", group.Select( i => i.DataId.ToString() ) );
                if( Add( $"{_manager.ToName( kind, dataId )} ({kind.ToName()})", group, collection ) )
                {
                    Penumbra.Log.Information( $"Migrated {name} ({kind.ToName()}) to NPC Identifiers [{ids}]." );
                }
                else
                {
                    ChatUtil.NotificationMessage(
                        $"Could not migrate {name} ({collection.AnonymizedName}) which was assumed to be a {kind.ToName()} with IDs [{ids}], please look through your individual collections.",
                        "Migration Failure", NotificationType.Error );
                }
            }
            // If it is not a valid NPC name, check if it can be a player name.
            else if( ActorManager.VerifyPlayerName( name ) )
            {
                identifier = _manager.CreatePlayer( ByteString.FromStringUnsafe( name, false ), ushort.MaxValue );
                var shortName = string.Join( " ", name.Split().Select( n => $"{n[ 0 ]}." ) );
                // Try to migrate the player name without logging full names.
                if( Add( $"{name} ({_manager.ToWorldName( identifier.HomeWorld )})", new[] { identifier }, collection ) )
                {
                    Penumbra.Log.Information( $"Migrated {shortName} ({collection.AnonymizedName}) to Player Identifier." );
                }
                else
                {
                    ChatUtil.NotificationMessage( $"Could not migrate {shortName} ({collection.AnonymizedName}), please look through your individual collections.",
                        "Migration Failure", NotificationType.Error );
                }
            }
            else
            {
                ChatUtil.NotificationMessage(
                    $"Could not migrate {name} ({collection.AnonymizedName}), which can not be a player name nor is it a known NPC name, please look through your individual collections.",
                    "Migration Failure", NotificationType.Error );
            }
        }
    }
}