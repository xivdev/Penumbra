using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.String;

namespace Penumbra.Collections.Manager;

public sealed partial class IndividualCollections : IReadOnlyList<(string DisplayName, ModCollection Collection)>
{
    public IEnumerator<(string DisplayName, ModCollection Collection)> GetEnumerator()
        => _assignments.Select(t => (t.DisplayName, t.Collection)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _assignments.Count;

    public (string DisplayName, ModCollection Collection) this[int index]
        => (_assignments[index].DisplayName, _assignments[index].Collection);

    public bool TryGetCollection(ActorIdentifier identifier, [NotNullWhen(true)] out ModCollection? collection)
    {
        if (Count == 0)
        {
            collection = null;
            return false;
        }

        switch (identifier.Type)
        {
            case IdentifierType.Player: return CheckWorlds(identifier, out collection);
            case IdentifierType.Retainer:
            {
                if (_individuals.TryGetValue(identifier, out collection))
                    return true;

                if (identifier.Retainer is not ActorIdentifier.RetainerType.Mannequin && _config.UseOwnerNameForCharacterCollection)
                    return CheckWorlds(_actors.GetCurrentPlayer(), out collection);

                break;
            }
            case IdentifierType.Owned:
            {
                if (CheckWorlds(identifier, out collection!))
                    return true;

                // Handle generic NPC
                var npcIdentifier = _actors.CreateIndividualUnchecked(IdentifierType.Npc, ByteString.Empty,
                    ushort.MaxValue, identifier.Kind, identifier.DataId);
                if (npcIdentifier.IsValid && _individuals.TryGetValue(npcIdentifier, out collection))
                    return true;

                // Handle Ownership.
                if (!_config.UseOwnerNameForCharacterCollection)
                    return false;

                identifier = _actors.CreateIndividualUnchecked(IdentifierType.Player, identifier.PlayerName,
                    identifier.HomeWorld.Id, ObjectKind.None, uint.MaxValue);
                return CheckWorlds(identifier, out collection);
            }
            case IdentifierType.Npc:     return _individuals.TryGetValue(identifier, out collection);
            case IdentifierType.Special: return CheckWorlds(ConvertSpecialIdentifier(identifier).Item1, out collection);
        }

        collection = null;
        return false;
    }

    public enum SpecialResult
    {
        PartyBanner,
        PvPBanner,
        Mahjong,
        CharacterScreen,
        FittingRoom,
        DyePreview,
        Portrait,
        Inspect,
        Card,
        Glamour,
        Invalid,
    }

    public (ActorIdentifier, SpecialResult) ConvertSpecialIdentifier(ActorIdentifier identifier)
    {
        if (identifier.Type != IdentifierType.Special)
            return (identifier, SpecialResult.Invalid);

        if (_actors.ResolvePartyBannerPlayer(identifier.Special, out var id))
            return _config.UseCharacterCollectionsInCards ? (id, SpecialResult.PartyBanner) : (identifier, SpecialResult.Invalid);

        if (_actors.ResolvePvPBannerPlayer(identifier.Special, out id))
            return _config.UseCharacterCollectionsInCards ? (id, SpecialResult.PvPBanner) : (identifier, SpecialResult.Invalid);

        if (_actors.ResolveMahjongPlayer(identifier.Special, out id))
            return _config.UseCharacterCollectionsInCards ? (id, SpecialResult.Mahjong) : (identifier, SpecialResult.Invalid);

        switch (identifier.Special)
        {
            case ScreenActor.CharacterScreen when _config.UseCharacterCollectionInMainWindow:
                return (_actors.GetCurrentPlayer(), SpecialResult.CharacterScreen);
            case ScreenActor.FittingRoom when _config.UseCharacterCollectionInTryOn:
                return (_actors.GetCurrentPlayer(), SpecialResult.FittingRoom);
            case ScreenActor.DyePreview when _config.UseCharacterCollectionInTryOn:
                return (_actors.GetCurrentPlayer(), SpecialResult.DyePreview);
            case ScreenActor.Portrait when _config.UseCharacterCollectionsInCards:
                return (_actors.GetCurrentPlayer(), SpecialResult.Portrait);
            case ScreenActor.ExamineScreen:
            {
                identifier = _actors.GetInspectPlayer();
                if (identifier.IsValid)
                    return (_config.UseCharacterCollectionInInspect ? identifier : ActorIdentifier.Invalid, SpecialResult.Inspect);

                identifier = _actors.GetCardPlayer();
                if (identifier.IsValid)
                    return (_config.UseCharacterCollectionInInspect ? identifier : ActorIdentifier.Invalid, SpecialResult.Card);

                return _config.UseCharacterCollectionInTryOn
                    ? (_actors.GetGlamourPlayer(), SpecialResult.Glamour)
                    : (identifier, SpecialResult.Invalid);
            }
            default: return (identifier, SpecialResult.Invalid);
        }
    }

    public bool TryGetCollection(IGameObject? gameObject, out ModCollection? collection)
        => TryGetCollection(_actors.FromObject(gameObject, true, false, false), out collection);

    public unsafe bool TryGetCollection(FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* gameObject, out ModCollection? collection)
        => TryGetCollection(_actors.FromObject(gameObject, out _, true, false, false), out collection);

    private bool CheckWorlds(ActorIdentifier identifier, out ModCollection? collection)
    {
        if (!identifier.IsValid)
        {
            collection = null;
            return false;
        }

        if (_individuals.TryGetValue(identifier, out collection))
            return true;

        identifier = _actors.CreateIndividualUnchecked(identifier.Type, identifier.PlayerName, ushort.MaxValue,
            identifier.Kind,
            identifier.DataId);
        if (identifier.IsValid && _individuals.TryGetValue(identifier, out collection))
            return true;

        collection = null;
        return false;
    }
}
