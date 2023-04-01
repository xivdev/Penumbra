using System;
using System.Collections;
using System.Linq;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using Penumbra.Api;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Services;
using Penumbra.Util;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Penumbra.Interop.PathResolving;

public unsafe class CollectionResolver
{
    private readonly PerformanceTracker        _performance;
    private readonly IdentifiedCollectionCache _cache;
    private readonly BitArray                  _validHumanModels;

    private readonly ClientState     _clientState;
    private readonly GameGui         _gameGui;
    private readonly ActorService    _actors;
    private readonly CutsceneService _cutscenes;

    private readonly Configuration         _config;
    private readonly CollectionManager     _collectionManager;
    private readonly TempCollectionManager _tempCollections;
    private readonly DrawObjectState       _drawObjectState;

    public CollectionResolver(PerformanceTracker performance, IdentifiedCollectionCache cache, ClientState clientState, GameGui gameGui,
        DataManager gameData, ActorService actors, CutsceneService cutscenes, Configuration config, CollectionManager collectionManager,
        TempCollectionManager tempCollections, DrawObjectState drawObjectState)
    {
        _performance       = performance;
        _cache             = cache;
        _clientState       = clientState;
        _gameGui           = gameGui;
        _actors            = actors;
        _cutscenes         = cutscenes;
        _config            = config;
        _collectionManager = collectionManager;
        _tempCollections   = tempCollections;
        _drawObjectState   = drawObjectState;
        _validHumanModels  = GetValidHumanModels(gameData);
    }

    /// <summary>
    /// Get the collection applying to the current player character
    /// or the Yourself or Default collection if no player exists.
    /// </summary>
    public ModCollection PlayerCollection()
    {
        using var performance = _performance.Measure(PerformanceType.IdentifyCollection);
        var       gameObject  = (GameObject*)(_clientState.LocalPlayer?.Address ?? nint.Zero);
        if (gameObject == null)
            return _collectionManager.ByType(CollectionType.Yourself)
             ?? _collectionManager.Default;

        var player = _actors.AwaitedService.GetCurrentPlayer();
        var _      = false;
        return CollectionByIdentifier(player)
         ?? CheckYourself(player, gameObject)
         ?? CollectionByAttributes(gameObject, ref _)
         ?? _collectionManager.Default;
    }

    /// <summary> Identify the correct collection for a game object. </summary>
    public ResolveData IdentifyCollection(GameObject* gameObject, bool useCache)
    {
        using var t = _performance.Measure(PerformanceType.IdentifyCollection);

        if (gameObject == null)
            return _collectionManager.Default.ToResolveData();

        try
        {
            if (useCache && _cache.TryGetValue(gameObject, out var data))
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
            return _collectionManager.Default.ToResolveData(gameObject);
        }
    }

    /// <summary> Identify the correct collection for the last created game object. </summary>
    public ResolveData IdentifyLastGameObjectCollection(bool useCache)
        => IdentifyCollection((GameObject*)_drawObjectState.LastGameObject, useCache);

    /// <summary> Identify the correct collection for a draw object. </summary>
    public ResolveData IdentifyCollection(DrawObject* drawObject, bool useCache)
    {
        var obj = (GameObject*)(_drawObjectState.TryGetValue((nint)drawObject, out var gameObject)
            ? gameObject.Item1
            : _drawObjectState.LastGameObject);
        return IdentifyCollection(obj, useCache);
    }

    /// <summary> Return whether the given ModelChara id refers to a human-type model. </summary>
    public bool IsModelHuman(uint modelCharaId)
        => modelCharaId < _validHumanModels.Length && _validHumanModels[(int)modelCharaId];

    /// <summary> Return whether the given character has a human model. </summary>
    public bool IsModelHuman(Character* character)
        => character != null && IsModelHuman((uint)character->ModelCharaId);

    /// <summary>
    /// Used if on the Login screen. Names are populated after actors are drawn,
    /// so it is not possible to fetch names from the ui list.
    /// Actors are also not named. So use Yourself > Players > Racial > Default.
    /// </summary>
    private bool LoginScreen(GameObject* gameObject, out ResolveData ret)
    {
        // Also check for empty names because sometimes named other characters
        // might be loaded before being officially logged in.
        if (_clientState.IsLoggedIn || gameObject->Name[0] != '\0')
        {
            ret = ResolveData.Invalid;
            return false;
        }

        var notYetReady = false;
        var collection = _collectionManager.ByType(CollectionType.Yourself)
         ?? CollectionByAttributes(gameObject, ref notYetReady)
         ?? _collectionManager.Default;
        ret = notYetReady ? collection.ToResolveData(gameObject) : _cache.Set(collection, ActorIdentifier.Invalid, gameObject);
        return true;
    }

    /// <summary> Used if at the aesthetician. The relevant actor is yourself, so use player collection when possible. </summary>
    private bool Aesthetician(GameObject* gameObject, out ResolveData ret)
    {
        if (_gameGui.GetAddonByName("ScreenLog") != IntPtr.Zero)
        {
            ret = ResolveData.Invalid;
            return false;
        }

        var player      = _actors.AwaitedService.GetCurrentPlayer();
        var notYetReady = false;
        var collection = (player.IsValid ? CollectionByIdentifier(player) : null)
         ?? _collectionManager.ByType(CollectionType.Yourself)
         ?? CollectionByAttributes(gameObject, ref notYetReady)
         ?? _collectionManager.Default;
        ret = notYetReady ? collection.ToResolveData(gameObject) : _cache.Set(collection, ActorIdentifier.Invalid, gameObject);
        return true;
    }

    /// <summary>
    /// Used when no special state is active.
    /// Use individual identifiers first, then Yourself, then group attributes, then ownership settings and last base.
    /// </summary>
    private ResolveData DefaultState(GameObject* gameObject)
    {
        var identifier = _actors.AwaitedService.FromObject(gameObject, out var owner, true, false, false);
        if (identifier.Type is IdentifierType.Special)
        {
            (identifier, var type) = _collectionManager.Individuals.ConvertSpecialIdentifier(identifier);
            if (_config.UseNoModsInInspect && type == IndividualCollections.SpecialResult.Inspect)
                return _cache.Set(ModCollection.Empty, identifier, gameObject);
        }

        var notYetReady = false;
        var collection = CollectionByIdentifier(identifier)
         ?? CheckYourself(identifier, gameObject)
         ?? CollectionByAttributes(gameObject, ref notYetReady)
         ?? CheckOwnedCollection(identifier, owner, ref notYetReady)
         ?? _collectionManager.Default;

        return notYetReady ? collection.ToResolveData(gameObject) : _cache.Set(collection, identifier, gameObject);
    }

    /// <summary> Check both temporary and permanent character collections. Temporary first. </summary>
    private ModCollection? CollectionByIdentifier(ActorIdentifier identifier)
        => _tempCollections.Collections.TryGetCollection(identifier, out var collection)
         || _collectionManager.Individuals.TryGetCollection(identifier, out collection)
                ? collection
                : null;

    /// <summary> Check for the Yourself collection. </summary>
    private ModCollection? CheckYourself(ActorIdentifier identifier, GameObject* actor)
    {
        if (actor->ObjectIndex == 0
         || _cutscenes.GetParentIndex(actor->ObjectIndex) == 0
         || identifier.Equals(_actors.AwaitedService.GetCurrentPlayer()))
            return _collectionManager.ByType(CollectionType.Yourself);

        return null;
    }

    /// <summary> Check special collections given the actor. Returns notYetReady if the customize array is not filled. </summary>
    private ModCollection? CollectionByAttributes(GameObject* actor, ref bool notYetReady)
    {
        if (!actor->IsCharacter())
            return null;

        // Only handle human models.
        var character = (Character*)actor;
        if (!IsModelHuman((uint)character->ModelCharaId))
            return null;

        if (character->CustomizeData[0] == 0)
        {
            notYetReady = true;
            return null;
        }

        var bodyType = character->CustomizeData[2];
        var collection = bodyType switch
        {
            3 => _collectionManager.ByType(CollectionType.NonPlayerElderly),
            4 => _collectionManager.ByType(CollectionType.NonPlayerChild),
            _ => null,
        };
        if (collection != null)
            return collection;

        var race   = (SubRace)character->CustomizeData[4];
        var gender = (Gender)(character->CustomizeData[1] + 1);
        var isNpc  = actor->ObjectKind != (byte)ObjectKind.Player;

        var type = CollectionTypeExtensions.FromParts(race, gender, isNpc);
        collection =   _collectionManager.ByType(type);
        collection ??= _collectionManager.ByType(CollectionTypeExtensions.FromParts(gender, isNpc));
        return collection;
    }

    /// <summary> Get the collection applying to the owner if it is available. </summary>
    private ModCollection? CheckOwnedCollection(ActorIdentifier identifier, GameObject* owner, ref bool notYetReady)
    {
        if (identifier.Type != IdentifierType.Owned || !_config.UseOwnerNameForCharacterCollection || owner == null)
            return null;

        var id = _actors.AwaitedService.CreateIndividualUnchecked(IdentifierType.Player, identifier.PlayerName, identifier.HomeWorld,
            ObjectKind.None,
            uint.MaxValue);
        return CheckYourself(id, owner)
         ?? CollectionByAttributes(owner, ref notYetReady);
    }

    /// <summary>
    /// Go through all ModelChara rows and return a bitfield of those that resolve to human models.
    /// </summary>
    private static BitArray GetValidHumanModels(DataManager gameData)
    {
        var sheet = gameData.GetExcelSheet<ModelChara>()!;
        var ret   = new BitArray((int)sheet.RowCount, false);
        foreach (var (_, idx) in sheet.WithIndex().Where(p => p.Value.Type == (byte)CharacterBase.ModelType.Human))
            ret[idx] = true;

        return ret;
    }
}
