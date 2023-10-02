using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Api.Enums;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.Services;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.Util;
using ObjectType = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;
using static Penumbra.GameData.Enums.GenderRace;

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
public unsafe class MetaState : IDisposable
{
    [Signature(Sigs.HumanVTable, ScanType = ScanType.StaticAddress)]
    private readonly nint* _humanVTable = null!;

    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    private readonly PerformanceTracker  _performance;
    private readonly CollectionResolver  _collectionResolver;
    private readonly ResourceLoader      _resources;
    private readonly GameEventManager    _gameEventManager;
    private readonly CharacterUtility    _characterUtility;

    private ResolveData         _lastCreatedCollection          = ResolveData.Invalid;
    private ResolveData         _customizeChangeCollection      = ResolveData.Invalid;
    private DisposableContainer _characterBaseCreateMetaChanges = DisposableContainer.Empty;

    public MetaState(PerformanceTracker performance, CommunicatorService communicator, CollectionResolver collectionResolver,
        ResourceLoader resources, GameEventManager gameEventManager, CharacterUtility characterUtility, Configuration config,
        IGameInteropProvider interop)
    {
        _performance        = performance;
        _communicator       = communicator;
        _collectionResolver = collectionResolver;
        _resources          = resources;
        _gameEventManager   = gameEventManager;
        _characterUtility   = characterUtility;
        _config             = config;
        interop.InitializeFromAttributes(this);
        _calculateHeightHook =
            interop.HookFromAddress<CalculateHeightDelegate>((nint)Character.MemberFunctionPointers.CalculateHeight, CalculateHeightDetour);
        _onModelLoadCompleteHook = interop.HookFromAddress<OnModelLoadCompleteDelegate>(_humanVTable[58], OnModelLoadCompleteDetour);
        _getEqpIndirectHook.Enable();
        _updateModelsHook.Enable();
        _onModelLoadCompleteHook.Enable();
        _setupVisorHook.Enable();
        _rspSetupCharacterHook.Enable();
        _changeCustomize.Enable();
        _calculateHeightHook.Enable();
        _gameEventManager.CreatingCharacterBase += OnCreatingCharacterBase;
        _gameEventManager.CharacterBaseCreated  += OnCharacterBaseCreated;
    }

    public bool HandleDecalFile(ResourceType type, Utf8GamePath gamePath, out ResolveData resolveData)
    {
        if (type == ResourceType.Tex
         && (_lastCreatedCollection.Valid || _customizeChangeCollection.Valid)
         && gamePath.Path.Substring("chara/common/texture/".Length).StartsWith("decal"u8))
        {
            resolveData = _lastCreatedCollection.Valid ? _lastCreatedCollection : _customizeChangeCollection;
            return true;
        }

        resolveData = ResolveData.Invalid;
        return false;
    }

    public DisposableContainer ResolveEqdpData(ModCollection collection, GenderRace race, bool equipment, bool accessory)
    {
        var races = race.Dependencies();
        if (races.Length == 0)
            return DisposableContainer.Empty;

        var equipmentEnumerable = equipment
            ? races.Select(r => collection.TemporarilySetEqdpFile(_characterUtility, r, false))
            : Array.Empty<IDisposable?>().AsEnumerable();
        var accessoryEnumerable = accessory
            ? races.Select(r => collection.TemporarilySetEqdpFile(_characterUtility, r, true))
            : Array.Empty<IDisposable?>().AsEnumerable();
        return new DisposableContainer(equipmentEnumerable.Concat(accessoryEnumerable));
    }

    public static GenderRace GetHumanGenderRace(nint human)
        => (GenderRace)((Human*)human)->RaceSexId;

    public void Dispose()
    {
        _getEqpIndirectHook.Dispose();
        _updateModelsHook.Dispose();
        _onModelLoadCompleteHook.Dispose();
        _setupVisorHook.Dispose();
        _rspSetupCharacterHook.Dispose();
        _changeCustomize.Dispose();
        _calculateHeightHook.Dispose();
        _gameEventManager.CreatingCharacterBase -= OnCreatingCharacterBase;
        _gameEventManager.CharacterBaseCreated  -= OnCharacterBaseCreated;
    }

    private void OnCreatingCharacterBase(nint modelCharaId, nint customize, nint equipData)
    {
        _lastCreatedCollection = _collectionResolver.IdentifyLastGameObjectCollection(true);
        if (_lastCreatedCollection.Valid && _lastCreatedCollection.AssociatedGameObject != nint.Zero)
            _communicator.CreatingCharacterBase.Invoke(_lastCreatedCollection.AssociatedGameObject,
                _lastCreatedCollection.ModCollection.Name, modelCharaId, customize, equipData);

        var decal = new DecalReverter(_config, _characterUtility, _resources, _lastCreatedCollection,
            UsesDecal(*(uint*)modelCharaId, customize));
        var cmp = _lastCreatedCollection.ModCollection.TemporarilySetCmpFile(_characterUtility);
        _characterBaseCreateMetaChanges.Dispose(); // Should always be empty.
        _characterBaseCreateMetaChanges = new DisposableContainer(decal, cmp);
    }

    private void OnCharacterBaseCreated(uint _1, nint _2, nint _3, nint drawObject)
    {
        _characterBaseCreateMetaChanges.Dispose();
        _characterBaseCreateMetaChanges = DisposableContainer.Empty;
        if (_lastCreatedCollection.Valid && _lastCreatedCollection.AssociatedGameObject != nint.Zero && drawObject != nint.Zero)
            _communicator.CreatedCharacterBase.Invoke(_lastCreatedCollection.AssociatedGameObject,
                _lastCreatedCollection.ModCollection, drawObject);
        _lastCreatedCollection = ResolveData.Invalid;
    }

    private delegate void                              OnModelLoadCompleteDelegate(nint drawObject);
    private readonly Hook<OnModelLoadCompleteDelegate> _onModelLoadCompleteHook;

    private void OnModelLoadCompleteDetour(nint drawObject)
    {
        var       collection = _collectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        using var eqp        = collection.ModCollection.TemporarilySetEqpFile(_characterUtility);
        using var eqdp       = ResolveEqdpData(collection.ModCollection, GetDrawObjectGenderRace(drawObject), true, true);
        _onModelLoadCompleteHook.Original.Invoke(drawObject);
    }

    private delegate void UpdateModelDelegate(nint drawObject);

    [Signature(Sigs.UpdateModel, DetourName = nameof(UpdateModelsDetour))]
    private readonly Hook<UpdateModelDelegate> _updateModelsHook = null!;

    private void UpdateModelsDetour(nint drawObject)
    {
        // Shortcut because this is called all the time.
        // Same thing is checked at the beginning of the original function.
        if (*(int*)(drawObject + Offsets.UpdateModelSkip) == 0)
            return;

        using var performance = _performance.Measure(PerformanceType.UpdateModels);

        var       collection = _collectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        using var eqp        = collection.ModCollection.TemporarilySetEqpFile(_characterUtility);
        using var eqdp       = ResolveEqdpData(collection.ModCollection, GetDrawObjectGenderRace(drawObject), true, true);
        _updateModelsHook.Original.Invoke(drawObject);
    }

    private static GenderRace GetDrawObjectGenderRace(nint drawObject)
    {
        var draw = (DrawObject*)drawObject;
        if (draw->Object.GetObjectType() != ObjectType.CharacterBase)
            return Unknown;

        var c = (CharacterBase*)drawObject;
        return c->GetModelType() == CharacterBase.ModelType.Human
            ? GetHumanGenderRace(drawObject)
            : Unknown;
    }

    [Signature(Sigs.GetEqpIndirect, DetourName = nameof(GetEqpIndirectDetour))]
    private readonly Hook<OnModelLoadCompleteDelegate> _getEqpIndirectHook = null!;

    private void GetEqpIndirectDetour(nint drawObject)
    {
        // Shortcut because this is also called all the time.
        // Same thing is checked at the beginning of the original function.
        if ((*(byte*)(drawObject + Offsets.GetEqpIndirectSkip1) & 1) == 0 || *(ulong*)(drawObject + Offsets.GetEqpIndirectSkip2) == 0)
            return;

        using var performance = _performance.Measure(PerformanceType.GetEqp);
        var       resolveData = _collectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        using var eqp         = resolveData.ModCollection.TemporarilySetEqpFile(_characterUtility);
        _getEqpIndirectHook.Original(drawObject);
    }


    // GMP. This gets called every time when changing visor state, and it accesses the gmp file itself,
    // but it only applies a changed gmp file after a redraw for some reason.
    private delegate byte SetupVisorDelegate(nint drawObject, ushort modelId, byte visorState);

    [Signature(Sigs.SetupVisor, DetourName = nameof(SetupVisorDetour))]
    private readonly Hook<SetupVisorDelegate> _setupVisorHook = null!;

    private byte SetupVisorDetour(nint drawObject, ushort modelId, byte visorState)
    {
        using var performance = _performance.Measure(PerformanceType.SetupVisor);
        var       resolveData = _collectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        using var gmp         = resolveData.ModCollection.TemporarilySetGmpFile(_characterUtility);
        return _setupVisorHook.Original(drawObject, modelId, visorState);
    }

    // RSP
    private delegate void RspSetupCharacterDelegate(nint drawObject, nint unk2, float unk3, nint unk4, byte unk5);

    [Signature(Sigs.RspSetupCharacter, DetourName = nameof(RspSetupCharacterDetour))]
    private readonly Hook<RspSetupCharacterDelegate> _rspSetupCharacterHook = null!;

    private void RspSetupCharacterDetour(nint drawObject, nint unk2, float unk3, nint unk4, byte unk5)
    {
        if (_customizeChangeCollection.Valid)
        {
            _rspSetupCharacterHook.Original(drawObject, unk2, unk3, unk4, unk5);
        }
        else
        {
            using var performance = _performance.Measure(PerformanceType.SetupCharacter);
            var       resolveData = _collectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
            using var cmp         = resolveData.ModCollection.TemporarilySetCmpFile(_characterUtility);
            _rspSetupCharacterHook.Original(drawObject, unk2, unk3, unk4, unk5);
        }
    }

    private delegate ulong CalculateHeightDelegate(Character* character);

    private readonly Hook<CalculateHeightDelegate> _calculateHeightHook = null!;

    private ulong CalculateHeightDetour(Character* character)
    {
        var       resolveData = _collectionResolver.IdentifyCollection((GameObject*)character, true);
        using var cmp         = resolveData.ModCollection.TemporarilySetCmpFile(_characterUtility);
        return _calculateHeightHook.Original(character);
    }

    private delegate bool ChangeCustomizeDelegate(nint human, nint data, byte skipEquipment);

    [Signature(Sigs.ChangeCustomize, DetourName = nameof(ChangeCustomizeDetour))]
    private readonly Hook<ChangeCustomizeDelegate> _changeCustomize = null!;

    private bool ChangeCustomizeDetour(nint human, nint data, byte skipEquipment)
    {
        using var performance = _performance.Measure(PerformanceType.ChangeCustomize);
        _customizeChangeCollection = _collectionResolver.IdentifyCollection((DrawObject*)human, true);
        using var cmp    = _customizeChangeCollection.ModCollection.TemporarilySetCmpFile(_characterUtility);
        using var decals = new DecalReverter(_config, _characterUtility, _resources, _customizeChangeCollection, true);
        using var decal2 = new DecalReverter(_config, _characterUtility, _resources, _customizeChangeCollection, false);
        var       ret    = _changeCustomize.Original(human, data, skipEquipment);
        _customizeChangeCollection = ResolveData.Invalid;
        return ret;
    }

    /// <summary>
    /// Check the customize array for the FaceCustomization byte and the last bit of that.
    /// Also check for humans.
    /// </summary>
    public static bool UsesDecal(uint modelId, nint customizeData)
        => modelId == 0 && ((byte*)customizeData)[12] > 0x7F;
}
