using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.Services;
using Penumbra.Services;
using Penumbra.String.Classes;
using ObjectType = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;
using Penumbra.Interop.Hooks.Objects;

namespace Penumbra.Interop.PathResolving;

// State: 6.35
// GetSlotEqpData seems to be the only function using the EQP table.
// It is only called by CheckSlotsForUnload (called by UpdateModels),
// SetupModelAttributes (called by UpdateModels and OnModelLoadComplete)
// and a unnamed function called by UpdateRender.
// It seems to be enough to change the EQP entries for UpdateModels.

// GetEqdpDataFor[Adults|Children|Other] seem to be the only functions using the EQDP tables.
// They are called by ResolveMdlPath, UpdateModels and SetupConnectorModelAttributes,
// which is called by SetupModelAttributes, which is called by OnModelLoadComplete and UpdateModels.
// It seems to be enough to change EQDP on UpdateModels and ResolveMDLPath.

// EST entries seem to be obtained by "44 8B C9 83 EA ?? 74", which is only called by
// ResolveSKLBPath, ResolveSKPPath, ResolvePHYBPath and indirectly by ResolvePAPPath.

// RSP height entries seem to be obtained by "E8 ?? ?? ?? ?? 48 8B 8E ?? ?? ?? ?? 44 8B CF"
// RSP tail entries seem to be obtained by "E8 ?? ?? ?? ?? 0F 28 F0 48 8B 05"
// RSP bust size entries seem to be obtained by  "E8 ?? ?? ?? ?? F2 0F 10 44 24 ?? 8B 44 24 ?? F2 0F 11 45 ?? 89 45 ?? 83 FF"
// they all are called by many functions, but the most relevant seem to be Human.SetupFromCharacterData, which is only called by CharacterBase.Create,
// ChangeCustomize and RspSetupCharacter, which is hooked here, as well as Character.CalculateHeight.

// GMP Entries seem to be only used by "48 8B ?? 53 55 57 48 83 ?? ?? 48 8B", which has a DrawObject as its first parameter.
public sealed unsafe class MetaState : IDisposable
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    private readonly CollectionResolver  _collectionResolver;
    private readonly ResourceLoader      _resources;
    private readonly CharacterUtility    _characterUtility;
    private readonly CreateCharacterBase _createCharacterBase;

    public ResolveData CustomizeChangeCollection = ResolveData.Invalid;
    public ResolveData EqpCollection             = ResolveData.Invalid;

    private ResolveData         _lastCreatedCollection          = ResolveData.Invalid;
    private DisposableContainer _characterBaseCreateMetaChanges = DisposableContainer.Empty;

    public MetaState(CommunicatorService communicator, CollectionResolver collectionResolver,
        ResourceLoader resources, CreateCharacterBase createCharacterBase, CharacterUtility characterUtility, Configuration config)
    {
        _communicator        = communicator;
        _collectionResolver  = collectionResolver;
        _resources           = resources;
        _createCharacterBase = createCharacterBase;
        _characterUtility    = characterUtility;
        _config              = config;
        _createCharacterBase.Subscribe(OnCreatingCharacterBase, CreateCharacterBase.Priority.MetaState);
        _createCharacterBase.Subscribe(OnCharacterBaseCreated,  CreateCharacterBase.PostEvent.Priority.MetaState);
    }

    public bool HandleDecalFile(ResourceType type, Utf8GamePath gamePath, out ResolveData resolveData)
    {
        if (type == ResourceType.Tex
         && (_lastCreatedCollection.Valid || CustomizeChangeCollection.Valid)
         && gamePath.Path.Substring("chara/common/texture/".Length).StartsWith("decal"u8))
        {
            resolveData = _lastCreatedCollection.Valid ? _lastCreatedCollection : CustomizeChangeCollection;
            return true;
        }

        resolveData = ResolveData.Invalid;
        return false;
    }

    public DisposableContainer ResolveEqdpData(ModCollection collection, GenderRace race, bool equipment, bool accessory)
        => (equipment, accessory) switch
        {
            (true, true) => new DisposableContainer(race.Dependencies().SelectMany(r => new[]
            {
                collection.TemporarilySetEqdpFile(_characterUtility, r, false),
                collection.TemporarilySetEqdpFile(_characterUtility, r, true),
            })),
            (true, false) => new DisposableContainer(race.Dependencies()
                .Select(r => collection.TemporarilySetEqdpFile(_characterUtility, r, false))),
            (false, true) => new DisposableContainer(race.Dependencies()
                .Select(r => collection.TemporarilySetEqdpFile(_characterUtility, r, true))),
            _ => DisposableContainer.Empty,
        };

    public MetaList.MetaReverter ResolveEqpData(ModCollection collection)
        => collection.TemporarilySetEqpFile(_characterUtility);

    public MetaList.MetaReverter ResolveGmpData(ModCollection collection)
        => collection.TemporarilySetGmpFile(_characterUtility);

    public MetaList.MetaReverter ResolveRspData(ModCollection collection)
        => collection.TemporarilySetCmpFile(_characterUtility);

    public DecalReverter ResolveDecal(ResolveData resolve, bool which)
        => new(_config, _characterUtility, _resources, resolve, which);

    public static GenderRace GetHumanGenderRace(nint human)
        => (GenderRace)((Human*)human)->RaceSexId;

    public static GenderRace GetDrawObjectGenderRace(nint drawObject)
    {
        var draw = (DrawObject*)drawObject;
        if (draw->Object.GetObjectType() != ObjectType.CharacterBase)
            return GenderRace.Unknown;

        var c = (CharacterBase*)drawObject;
        return c->GetModelType() == CharacterBase.ModelType.Human
            ? GetHumanGenderRace(drawObject)
            : GenderRace.Unknown;
    }

    public void Dispose()
    {
        _createCharacterBase.Unsubscribe(OnCreatingCharacterBase);
        _createCharacterBase.Unsubscribe(OnCharacterBaseCreated);
    }

    private void OnCreatingCharacterBase(ModelCharaId* modelCharaId, CustomizeArray* customize, CharacterArmor* equipData)
    {
        _lastCreatedCollection = _collectionResolver.IdentifyLastGameObjectCollection(true);
        if (_lastCreatedCollection.Valid && _lastCreatedCollection.AssociatedGameObject != nint.Zero)
            _communicator.CreatingCharacterBase.Invoke(_lastCreatedCollection.AssociatedGameObject,
                _lastCreatedCollection.ModCollection.Id, (nint)modelCharaId, (nint)customize, (nint)equipData);

        var decal = new DecalReverter(_config, _characterUtility, _resources, _lastCreatedCollection,
            UsesDecal(*(uint*)modelCharaId, (nint)customize));
        var cmp = _lastCreatedCollection.ModCollection.TemporarilySetCmpFile(_characterUtility);
        _characterBaseCreateMetaChanges.Dispose(); // Should always be empty.
        _characterBaseCreateMetaChanges = new DisposableContainer(decal, cmp);
    }

    private void OnCharacterBaseCreated(ModelCharaId _1, CustomizeArray* _2, CharacterArmor* _3, CharacterBase* drawObject)
    {
        _characterBaseCreateMetaChanges.Dispose();
        _characterBaseCreateMetaChanges = DisposableContainer.Empty;
        if (_lastCreatedCollection.Valid && _lastCreatedCollection.AssociatedGameObject != nint.Zero && drawObject != null)
            _communicator.CreatedCharacterBase.Invoke(_lastCreatedCollection.AssociatedGameObject,
                _lastCreatedCollection.ModCollection, (nint)drawObject);
        _lastCreatedCollection = ResolveData.Invalid;
    }

    /// <summary>
    /// Check the customize array for the FaceCustomization byte and the last bit of that.
    /// Also check for humans.
    /// </summary>
    private static bool UsesDecal(uint modelId, nint customizeData)
        => modelId == 0 && ((byte*)customizeData)[12] > 0x7F;
}
