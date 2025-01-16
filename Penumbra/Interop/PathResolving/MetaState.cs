using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Api.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Services;
using Penumbra.Services;
using Penumbra.String.Classes;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;
using Penumbra.Interop.Hooks.Objects;
using Penumbra.Interop.Hooks.ResourceLoading;

namespace Penumbra.Interop.PathResolving;

// State: 6.35
// GetSlotEqpData seems to be the only function using the EQP table.
// It is only called by CheckSlotsForUnload (called by UpdateModels),
// SetupModelAttributes (called by UpdateModels and OnModelLoadComplete)
// and an unnamed function called by UpdateRender.
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

// GMP Entries seem to be only used by "48 8B ?? 53 55 57 48 83 ?? ?? 48 8B", which is SetupVisor.
public sealed unsafe class MetaState : IDisposable, IService
{
    public readonly  Configuration       Config;
    private readonly CommunicatorService _communicator;
    private readonly CollectionResolver  _collectionResolver;
    private readonly ResourceLoader      _resources;
    private readonly CharacterUtility    _characterUtility;
    private readonly CreateCharacterBase _createCharacterBase;

    public          ResolveData        CustomizeChangeCollection = ResolveData.Invalid;
    public readonly Stack<ResolveData> EqpCollection             = [];
    public readonly Stack<ResolveData> EqdpCollection            = [];
    public readonly Stack<ResolveData> EstCollection             = [];
    public readonly Stack<ResolveData> RspCollection             = [];
    public readonly Stack<ResolveData> AtchCollection            = [];

    public readonly Stack<(ResolveData Collection, PrimaryId Id)> GmpCollection = [];


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
        Config               = config;
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

    public DecalReverter ResolveDecal(ResolveData resolve, bool which)
        => new(Config, _characterUtility, _resources, resolve, which);

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
                _lastCreatedCollection.ModCollection.Identity.Id, (nint)modelCharaId, (nint)customize, (nint)equipData);

        var decal = new DecalReverter(Config, _characterUtility, _resources, _lastCreatedCollection,
            UsesDecal(*(uint*)modelCharaId, (nint)customize));
        RspCollection.Push(_lastCreatedCollection);
        _characterBaseCreateMetaChanges.Dispose(); // Should always be empty.
        _characterBaseCreateMetaChanges = new DisposableContainer(decal);
    }

    private void OnCharacterBaseCreated(ModelCharaId _1, CustomizeArray* _2, CharacterArmor* _3, CharacterBase* drawObject)
    {
        _characterBaseCreateMetaChanges.Dispose();
        _characterBaseCreateMetaChanges = DisposableContainer.Empty;
        if (_lastCreatedCollection.Valid && _lastCreatedCollection.AssociatedGameObject != nint.Zero && drawObject != null)
            _communicator.CreatedCharacterBase.Invoke(_lastCreatedCollection.AssociatedGameObject,
                _lastCreatedCollection.ModCollection, (nint)drawObject);
        RspCollection.Pop();
        _lastCreatedCollection = ResolveData.Invalid;
    }

    /// <summary>
    /// Check the customize array for the FaceCustomization byte and the last bit of that.
    /// Also check for humans.
    /// </summary>
    private static bool UsesDecal(uint modelId, nint customizeData)
        => modelId == 0 && ((byte*)customizeData)[12] > 0x7F;
}
