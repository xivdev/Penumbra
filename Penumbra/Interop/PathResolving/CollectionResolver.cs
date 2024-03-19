using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Util;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Penumbra.Interop.PathResolving;

public sealed unsafe class CollectionResolver(
    PerformanceTracker performance,
    IdentifiedCollectionCache cache,
    IClientState clientState,
    IGameGui gameGui,
    ActorManager actors,
    CutsceneService cutscenes,
    Configuration config,
    CollectionManager collectionManager,
    TempCollectionManager tempCollections,
    DrawObjectState drawObjectState,
    HumanModelList humanModels)
    : IService
{
    /// <summary>
    /// Get the collection applying to the current player character
    /// or the 'Yourself' or 'Default' collection if no player exists.
    /// </summary>
    public ModCollection PlayerCollection()
    {
        using var performance1 = performance.Measure(PerformanceType.IdentifyCollection);
        var       gameObject   = (GameObject*)(clientState.LocalPlayer?.Address ?? nint.Zero);
        if (gameObject == null)
            return collectionManager.Active.ByType(CollectionType.Yourself)
             ?? collectionManager.Active.Default;

        var player = actors.GetCurrentPlayer();
        var _      = false;
        return CollectionByIdentifier(player)
         ?? CheckYourself(player, gameObject)
         ?? CollectionByAttributes(gameObject, ref _)
         ?? collectionManager.Active.Default;
    }

    /// <summary> Identify the correct collection for a game object. </summary>
    public ResolveData IdentifyCollection(GameObject* gameObject, bool useCache)
    {
        using var t = performance.Measure(PerformanceType.IdentifyCollection);

        if (gameObject == null)
            return collectionManager.Active.Default.ToResolveData();

        try
        {
            if (useCache && cache.TryGetValue(gameObject, out var data))
                return data;

            if (LoginScreen(gameObject, out data))
                return data;

            if (Aesthetician(gameObject, out data))
                return data;

            return DefaultState(gameObject);
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Error identifying collection:\n{ex}");
            return collectionManager.Active.Default.ToResolveData(gameObject);
        }
    }

    /// <summary> Identify the correct collection for the last created game object. </summary>
    public ResolveData IdentifyLastGameObjectCollection(bool useCache)
        => IdentifyCollection((GameObject*)drawObjectState.LastGameObject, useCache);

    /// <summary> Identify the correct collection for a draw object. </summary>
    public ResolveData IdentifyCollection(DrawObject* drawObject, bool useCache)
    {
        var obj = (GameObject*)(drawObjectState.TryGetValue((nint)drawObject, out var gameObject)
            ? gameObject.Item1
            : drawObjectState.LastGameObject);
        return IdentifyCollection(obj, useCache);
    }

    /// <summary> Return whether the given ModelChara id refers to a human-type model. </summary>
    public bool IsModelHuman(uint modelCharaId)
        => humanModels.IsHuman(modelCharaId);

    /// <summary> Return whether the given character has a human model. </summary>
    public bool IsModelHuman(Character* character)
        => character != null && IsModelHuman((uint)character->CharacterData.ModelCharaId);

    /// <summary>
    /// Used if on the Login screen. Names are populated after actors are drawn,
    /// so it is not possible to fetch names from the ui list.
    /// Actors are also not named. So use Yourself > Players > Racial > Default.
    /// </summary>
    private bool LoginScreen(GameObject* gameObject, out ResolveData ret)
    {
        // Also check for empty names because sometimes named other characters
        // might be loaded before being officially logged in.
        if (clientState.IsLoggedIn || gameObject->Name[0] != '\0')
        {
            ret = ResolveData.Invalid;
            return false;
        }

        var notYetReady = false;
        var collection = collectionManager.Active.ByType(CollectionType.Yourself)
         ?? CollectionByAttributes(gameObject, ref notYetReady)
         ?? collectionManager.Active.Default;
        ret = notYetReady ? collection.ToResolveData(gameObject) : cache.Set(collection, ActorIdentifier.Invalid, gameObject);
        return true;
    }

    /// <summary> Used if at the aesthetician. The relevant actor is yourself, so use player collection when possible. </summary>
    private bool Aesthetician(GameObject* gameObject, out ResolveData ret)
    {
        if (gameGui.GetAddonByName("ScreenLog") != IntPtr.Zero)
        {
            ret = ResolveData.Invalid;
            return false;
        }

        var player      = actors.GetCurrentPlayer();
        var notYetReady = false;
        var collection = (player.IsValid ? CollectionByIdentifier(player) : null)
         ?? collectionManager.Active.ByType(CollectionType.Yourself)
         ?? CollectionByAttributes(gameObject, ref notYetReady)
         ?? collectionManager.Active.Default;
        ret = notYetReady ? collection.ToResolveData(gameObject) : cache.Set(collection, ActorIdentifier.Invalid, gameObject);
        return true;
    }

    /// <summary>
    /// Used when no special state is active.
    /// Use individual identifiers first, then Yourself, then group attributes, then ownership settings and last base.
    /// </summary>
    private ResolveData DefaultState(GameObject* gameObject)
    {
        var identifier = actors.FromObject(gameObject, out var owner, true, false, false);
        if (identifier.Type is IdentifierType.Special)
        {
            (identifier, var type) = collectionManager.Active.Individuals.ConvertSpecialIdentifier(identifier);
            if (config.UseNoModsInInspect && type == IndividualCollections.SpecialResult.Inspect)
                return cache.Set(ModCollection.Empty, identifier, gameObject);
        }

        var notYetReady = false;
        var collection = CollectionByIdentifier(identifier)
         ?? CheckYourself(identifier, gameObject)
         ?? CollectionByAttributes(gameObject, ref notYetReady)
         ?? CheckOwnedCollection(identifier, owner, ref notYetReady)
         ?? collectionManager.Active.Default;

        return notYetReady ? collection.ToResolveData(gameObject) : cache.Set(collection, identifier, gameObject);
    }

    /// <summary> Check both temporary and permanent character collections. Temporary first. </summary>
    private ModCollection? CollectionByIdentifier(ActorIdentifier identifier)
        => tempCollections.Collections.TryGetCollection(identifier, out var collection)
         || collectionManager.Active.Individuals.TryGetCollection(identifier, out collection)
                ? collection
                : null;

    /// <summary> Check for the Yourself collection. </summary>
    private ModCollection? CheckYourself(ActorIdentifier identifier, Actor actor)
    {
        if (actor.Index == 0
         || cutscenes.GetParentIndex(actor.Index.Index) == 0
         || identifier.Equals(actors.GetCurrentPlayer()))
            return collectionManager.Active.ByType(CollectionType.Yourself);

        return null;
    }

    /// <summary> Check special collections given the actor. Returns notYetReady if the customize array is not filled. </summary>
    private ModCollection? CollectionByAttributes(Actor actor, ref bool notYetReady)
    {
        if (!actor.IsCharacter)
            return null;

        // Only handle human models.
        
        if (!IsModelHuman((uint)actor.AsCharacter->CharacterData.ModelCharaId))
            return null;

        if (actor.Customize->Data[0] == 0)
        {
            notYetReady = true;
            return null;
        }

        var bodyType = actor.Customize->Data[2];
        var collection = bodyType switch
        {
            3 => collectionManager.Active.ByType(CollectionType.NonPlayerElderly),
            4 => collectionManager.Active.ByType(CollectionType.NonPlayerChild),
            _ => null,
        };
        if (collection != null)
            return collection;

        var race   = (SubRace)actor.Customize->Data[4];
        var gender = (Gender)(actor.Customize->Data[1] + 1);
        var isNpc  = !actor.IsPlayer;

        var type = CollectionTypeExtensions.FromParts(race, gender, isNpc);
        collection =   collectionManager.Active.ByType(type);
        collection ??= collectionManager.Active.ByType(CollectionTypeExtensions.FromParts(gender, isNpc));
        return collection;
    }

    /// <summary> Get the collection applying to the owner if it is available. </summary>
    private ModCollection? CheckOwnedCollection(ActorIdentifier identifier, Actor owner, ref bool notYetReady)
    {
        if (identifier.Type != IdentifierType.Owned || !config.UseOwnerNameForCharacterCollection || !owner.Valid)
            return null;

        var id = actors.CreateIndividualUnchecked(IdentifierType.Player, identifier.PlayerName, identifier.HomeWorld.Id,
            ObjectKind.None,
            uint.MaxValue);
        return CheckYourself(id, owner) ?? CollectionByAttributes(owner, ref notYetReady);
    }
}
