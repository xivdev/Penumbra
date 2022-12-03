using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.GameData.Actors;
using Penumbra.String;

namespace Penumbra.Collections;

public sealed partial class IndividualCollections : IReadOnlyList< (string DisplayName, ModCollection Collection) >
{
    public IEnumerator< (string DisplayName, ModCollection Collection) > GetEnumerator()
        => _assignments.Select( t => ( t.DisplayName, t.Collection ) ).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _assignments.Count;

    public (string DisplayName, ModCollection Collection) this[ int index ]
        => ( _assignments[ index ].DisplayName, _assignments[ index ].Collection );

    public bool TryGetCollection( ActorIdentifier identifier, [NotNullWhen( true )] out ModCollection? collection, out ActorIdentifier specialIdentifier )
    {
        specialIdentifier = ActorIdentifier.Invalid;
        switch( identifier.Type )
        {
            case IdentifierType.Player: return CheckWorlds( identifier, out collection );
            case IdentifierType.Retainer:
            {
                if( _individuals.TryGetValue( identifier, out collection ) )
                {
                    return true;
                }

                if( Penumbra.Config.UseOwnerNameForCharacterCollection )
                {
                    return CheckWorlds( _actorManager.GetCurrentPlayer(), out collection );
                }

                break;
            }
            case IdentifierType.Owned:
            {
                if( CheckWorlds( identifier, out collection! ) )
                {
                    return true;
                }

                // Handle generic NPC
                var npcIdentifier = _actorManager.CreateIndividualUnchecked( IdentifierType.Npc, ByteString.Empty, ushort.MaxValue, identifier.Kind, identifier.DataId );
                if( npcIdentifier.IsValid && _individuals.TryGetValue( npcIdentifier, out collection ) )
                {
                    return true;
                }

                // Handle Ownership.
                if( Penumbra.Config.UseOwnerNameForCharacterCollection )
                {
                    identifier = _actorManager.CreateIndividualUnchecked( IdentifierType.Player, identifier.PlayerName, identifier.HomeWorld, ObjectKind.None, uint.MaxValue );
                    return CheckWorlds( identifier, out collection );
                }

                return false;
            }
            case IdentifierType.Npc: return _individuals.TryGetValue( identifier, out collection );
            case IdentifierType.Special:
                switch( identifier.Special )
                {
                    case SpecialActor.CharacterScreen when Penumbra.Config.UseCharacterCollectionInMainWindow:
                    case SpecialActor.FittingRoom when Penumbra.Config.UseCharacterCollectionInTryOn:
                    case SpecialActor.DyePreview when Penumbra.Config.UseCharacterCollectionInTryOn:
                    case SpecialActor.Portrait when Penumbra.Config.UseCharacterCollectionsInCards:
                        return CheckWorlds( specialIdentifier = _actorManager.GetCurrentPlayer(), out collection );
                    case SpecialActor.ExamineScreen:
                    {
                        return CheckWorlds( specialIdentifier = _actorManager.GetInspectPlayer(), out collection! )
                         || CheckWorlds( specialIdentifier = _actorManager.GetCardPlayer(), out collection! )
                         || CheckWorlds( specialIdentifier = _actorManager.GetGlamourPlayer(), out collection! );
                    }
                }

                break;
        }

        collection = null;
        return false;
    }

    public bool TryGetCollection( GameObject? gameObject, out ModCollection? collection, out ActorIdentifier specialIdentifier )
        => TryGetCollection( _actorManager.FromObject( gameObject, false ), out collection, out specialIdentifier );

    public unsafe bool TryGetCollection( FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* gameObject, out ModCollection? collection, out ActorIdentifier specialIdentifier )
        => TryGetCollection( _actorManager.FromObject( gameObject, false ), out collection, out specialIdentifier );

    private bool CheckWorlds( ActorIdentifier identifier, out ModCollection? collection )
    {
        if( !identifier.IsValid )
        {
            collection = null;
            return false;
        }

        if( _individuals.TryGetValue( identifier, out collection ) )
        {
            return true;
        }

        identifier = _actorManager.CreateIndividualUnchecked( identifier.Type, identifier.PlayerName, ushort.MaxValue, identifier.Kind, identifier.DataId );
        if( identifier.IsValid && _individuals.TryGetValue( identifier, out collection ) )
        {
            return true;
        }

        collection = null;
        return false;
    }
}