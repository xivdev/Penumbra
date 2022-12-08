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

    public bool TryGetCollection( ActorIdentifier identifier, [NotNullWhen( true )] out ModCollection? collection )
    {
        if( Count == 0 )
        {
            collection = null;
            return false;
        }

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
            case IdentifierType.Npc:     return _individuals.TryGetValue( identifier, out collection );
            case IdentifierType.Special: return CheckWorlds( ConvertSpecialIdentifier( identifier ), out collection );
        }

        collection = null;
        return false;
    }

    public ActorIdentifier ConvertSpecialIdentifier( ActorIdentifier identifier )
    {
        if( identifier.Type != IdentifierType.Special )
        {
            return identifier;
        }

        switch( identifier.Special )
        {
            case SpecialActor.CharacterScreen when Penumbra.Config.UseCharacterCollectionInMainWindow:
            case SpecialActor.FittingRoom when Penumbra.Config.UseCharacterCollectionInTryOn:
            case SpecialActor.DyePreview when Penumbra.Config.UseCharacterCollectionInTryOn:
            case SpecialActor.Portrait when Penumbra.Config.UseCharacterCollectionsInCards:
                return _actorManager.GetCurrentPlayer();
            case SpecialActor.ExamineScreen:
            {
                identifier = _actorManager.GetInspectPlayer();
                if( identifier.IsValid )
                {
                    return identifier;
                }

                identifier = _actorManager.GetCardPlayer();
                if( identifier.IsValid )
                {
                    return identifier;
                }

                return _actorManager.GetGlamourPlayer();
            }
            default: return identifier;
        }
    }

    public bool TryGetCollection( GameObject? gameObject, out ModCollection? collection )
        => TryGetCollection( _actorManager.FromObject( gameObject, true, false ), out collection );

    public unsafe bool TryGetCollection( FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* gameObject, out ModCollection? collection )
        => TryGetCollection( _actorManager.FromObject( gameObject, out _, true, false ), out collection );

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