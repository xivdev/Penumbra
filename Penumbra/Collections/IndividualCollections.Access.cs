using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.GameData.Actors;

namespace Penumbra.Collections;

public sealed partial class IndividualCollections : IReadOnlyList< (string DisplayName, ModCollection Collection) >
{
    public IEnumerator< (string DisplayName, ModCollection Collection) > GetEnumerator()
        => _assignments.Select( kvp => ( kvp.Key, kvp.Value.Collection ) ).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _assignments.Count;

    public (string DisplayName, ModCollection Collection) this[ int index ]
        => ( _assignments.Keys[ index ], _assignments.Values[ index ].Collection );

    public bool TryGetCollection( ActorIdentifier identifier, [NotNullWhen( true )] out ModCollection? collection )
    {
        switch( identifier.Type )
        {
            case IdentifierType.Player: return CheckWorlds( identifier, out collection );
            case IdentifierType.Owned:
            {
                if( CheckWorlds( identifier, out collection! ) )
                {
                    return true;
                }

                // Handle generic NPC
                var npcIdentifier = _manager.CreateNpc( identifier.Kind, identifier.DataId );
                if( npcIdentifier.IsValid && _individuals.TryGetValue( identifier, out collection ) )
                {
                    return true;
                }

                // Handle Ownership.
                if( Penumbra.Config.UseOwnerNameForCharacterCollection )
                {
                    identifier = _manager.CreatePlayer( identifier.PlayerName, identifier.HomeWorld );
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
                        return CheckWorlds( _manager.GetCurrentPlayer(), out collection );
                    case SpecialActor.ExamineScreen:
                    {
                        if( CheckWorlds( _manager.GetInspectPlayer(), out collection! ) )
                        {
                            return true;
                        }

                        if( CheckWorlds( _manager.GetCardPlayer(), out collection! ) )
                        {
                            return true;
                        }

                        if( CheckWorlds( _manager.GetGlamourPlayer(), out collection! ) )
                        {
                            return true;
                        }

                        break;
                    }
                }

                break;
        }

        collection = null;
        return false;
    }

    public bool TryGetCollection( GameObject? gameObject, out ModCollection? collection )
        => TryGetCollection( _manager.FromObject( gameObject ), out collection );

    public unsafe bool TryGetCollection( FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* gameObject, out ModCollection? collection )
        => TryGetCollection( _manager.FromObject( gameObject ), out collection );

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

        identifier = _manager.CreateIndividualUnchecked( identifier.Type, identifier.PlayerName, ushort.MaxValue, identifier.Kind, identifier.DataId );
        if( identifier.IsValid && _individuals.TryGetValue( identifier, out collection ) )
        {
            return true;
        }

        collection = null;
        return false;
    }
}