using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Communication;
using Penumbra.GameData;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.Interop.Hooks.Resources;
using Penumbra.Interop.Structs;
using Penumbra.Services;
using Penumbra.String.Classes;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;
using CSModelRenderer = FFXIVClientStructs.FFXIV.Client.Graphics.Render.ModelRenderer;
using ModelRenderer = Penumbra.Interop.Services.ModelRenderer;

namespace Penumbra.Interop.Hooks.PostProcessing;

public sealed unsafe class ShaderReplacementFixer : IDisposable, IRequiredService
{
    public static ReadOnlySpan<byte> SkinShpkName
        => "skin.shpk"u8;

    public static ReadOnlySpan<byte> CharacterStockingsShpkName
        => "characterstockings.shpk"u8;

    public static ReadOnlySpan<byte> CharacterLegacyShpkName
        => "characterlegacy.shpk"u8;

    public static ReadOnlySpan<byte> IrisShpkName
        => "iris.shpk"u8;

    public static ReadOnlySpan<byte> CharacterGlassShpkName
        => "characterglass.shpk"u8;

    public static ReadOnlySpan<byte> CharacterTransparencyShpkName
        => "charactertransparency.shpk"u8;

    public static ReadOnlySpan<byte> CharacterTattooShpkName
        => "charactertattoo.shpk"u8;

    public static ReadOnlySpan<byte> CharacterOcclusionShpkName
        => "characterocclusion.shpk"u8;

    public static ReadOnlySpan<byte> HairMaskShpkName
        => "hairmask.shpk"u8;

    private delegate nint CharacterBaseOnRenderMaterialDelegate(CharacterBase* drawObject, CSModelRenderer.OnRenderMaterialParams* param);

    private delegate nint ModelRendererOnRenderMaterialDelegate(CSModelRenderer* modelRenderer, ushort* outFlags,
        CSModelRenderer.OnRenderModelParams* param, Material* material, uint materialIndex);

    private delegate void ModelRendererUnkFuncDelegate(CSModelRenderer* modelRenderer, ModelRendererStructs.UnkPayload* unkPayload, uint unk2,
        uint unk3, uint unk4, uint unk5);

    private readonly Hook<CharacterBaseOnRenderMaterialDelegate> _humanOnRenderMaterialHook;

    private readonly Hook<ModelRendererOnRenderMaterialDelegate> _modelRendererOnRenderMaterialHook;

    private readonly Hook<ModelRendererUnkFuncDelegate> _modelRendererUnkFuncHook;

    private readonly Hook<MaterialResourceHandle.Delegates.PrepareColorTable> _prepareColorTableHook;

    private readonly ResourceHandleDestructor _resourceHandleDestructor;
    private readonly CommunicatorService      _communicator;
    private readonly HumanSetupScalingHook    _humanSetupScalingHook;

    private readonly ModdedShaderPackageState _skinState;
    private readonly ModdedShaderPackageState _characterStockingsState;
    private readonly ModdedShaderPackageState _characterLegacyState;
    private readonly ModdedShaderPackageState _irisState;
    private readonly ModdedShaderPackageState _characterGlassState;
    private readonly ModdedShaderPackageState _characterTransparencyState;
    private readonly ModdedShaderPackageState _characterTattooState;
    private readonly ModdedShaderPackageState _characterOcclusionState;
    private readonly ModdedShaderPackageState _hairMaskState;

    public bool Enabled { get; internal set; } = true;

    public uint ModdedSkinShpkCount
        => _skinState.MaterialCount;

    public uint ModdedCharacterStockingsShpkCount
        => _characterStockingsState.MaterialCount;

    public uint ModdedCharacterLegacyShpkCount
        => _characterLegacyState.MaterialCount;

    public uint ModdedIrisShpkCount
        => _irisState.MaterialCount;

    public uint ModdedCharacterGlassShpkCount
        => _characterGlassState.MaterialCount;

    public uint ModdedCharacterTransparencyShpkCount
        => _characterTransparencyState.MaterialCount;

    public uint ModdedCharacterTattooShpkCount
        => _characterTattooState.MaterialCount;

    public uint ModdedCharacterOcclusionShpkCount
        => _characterOcclusionState.MaterialCount;

    public uint ModdedHairMaskShpkCount
        => _hairMaskState.MaterialCount;

    public ShaderReplacementFixer(ResourceHandleDestructor resourceHandleDestructor, CharacterUtility utility, ModelRenderer modelRenderer,
        CommunicatorService communicator, HookManager hooks, CharacterBaseVTables vTables, HumanSetupScalingHook humanSetupScalingHook)
    {
        _resourceHandleDestructor = resourceHandleDestructor;
        _communicator             = communicator;
        _humanSetupScalingHook    = humanSetupScalingHook;

        _skinState = new ModdedShaderPackageState(
            () => (ShaderPackageResourceHandle**)&utility.Address->SkinShpkResource,
            () => (ShaderPackageResourceHandle*)utility.DefaultSkinShpkResource);
        _characterStockingsState = new ModdedShaderPackageState(
            () => (ShaderPackageResourceHandle**)&utility.Address->CharacterStockingsShpkResource,
            () => (ShaderPackageResourceHandle*)utility.DefaultCharacterStockingsShpkResource);
        _characterLegacyState = new ModdedShaderPackageState(
            () => (ShaderPackageResourceHandle**)&utility.Address->CharacterLegacyShpkResource,
            () => (ShaderPackageResourceHandle*)utility.DefaultCharacterLegacyShpkResource);
        _irisState = new ModdedShaderPackageState(() => modelRenderer.IrisShaderPackage, () => modelRenderer.DefaultIrisShaderPackage);
        _characterGlassState = new ModdedShaderPackageState(() => modelRenderer.CharacterGlassShaderPackage,
            () => modelRenderer.DefaultCharacterGlassShaderPackage);
        _characterTransparencyState = new ModdedShaderPackageState(() => modelRenderer.CharacterTransparencyShaderPackage,
            () => modelRenderer.DefaultCharacterTransparencyShaderPackage);
        _characterTattooState = new ModdedShaderPackageState(() => modelRenderer.CharacterTattooShaderPackage,
            () => modelRenderer.DefaultCharacterTattooShaderPackage);
        _characterOcclusionState = new ModdedShaderPackageState(() => modelRenderer.CharacterOcclusionShaderPackage,
            () => modelRenderer.DefaultCharacterOcclusionShaderPackage);
        _hairMaskState =
            new ModdedShaderPackageState(() => modelRenderer.HairMaskShaderPackage, () => modelRenderer.DefaultHairMaskShaderPackage);

        _humanSetupScalingHook.SetupReplacements += SetupHssReplacements;
        _humanOnRenderMaterialHook = hooks.CreateHook<CharacterBaseOnRenderMaterialDelegate>("Human.OnRenderMaterial", vTables.HumanVTable[64],
            OnRenderHumanMaterial, !HookOverrides.Instance.PostProcessing.HumanOnRenderMaterial).Result;
        _modelRendererOnRenderMaterialHook = hooks.CreateHook<ModelRendererOnRenderMaterialDelegate>("ModelRenderer.OnRenderMaterial",
            Sigs.ModelRendererOnRenderMaterial, ModelRendererOnRenderMaterialDetour,
            !HookOverrides.Instance.PostProcessing.ModelRendererOnRenderMaterial).Result;
        _modelRendererUnkFuncHook = hooks.CreateHook<ModelRendererUnkFuncDelegate>("ModelRenderer.UnkFunc",
            Sigs.ModelRendererUnkFunc, ModelRendererUnkFuncDetour,
            !HookOverrides.Instance.PostProcessing.ModelRendererUnkFunc).Result;
        _prepareColorTableHook = hooks.CreateHook<MaterialResourceHandle.Delegates.PrepareColorTable>(
            "MaterialResourceHandle.PrepareColorTable",
            Sigs.PrepareColorSet, PrepareColorTableDetour,
            !HookOverrides.Instance.PostProcessing.PrepareColorTable).Result;

        _communicator.MtrlLoaded.Subscribe(OnMtrlLoaded, MtrlLoaded.Priority.ShaderReplacementFixer);
        _resourceHandleDestructor.Subscribe(OnResourceHandleDestructor, ResourceHandleDestructor.Priority.ShaderReplacementFixer);
    }

    public void Dispose()
    {
        _prepareColorTableHook.Dispose();
        _modelRendererUnkFuncHook.Dispose();
        _modelRendererOnRenderMaterialHook.Dispose();
        _humanOnRenderMaterialHook.Dispose();
        _humanSetupScalingHook.SetupReplacements -= SetupHssReplacements;

        _communicator.MtrlLoaded.Unsubscribe(OnMtrlLoaded);
        _resourceHandleDestructor.Unsubscribe(OnResourceHandleDestructor);

        _hairMaskState.ClearMaterials();
        _characterOcclusionState.ClearMaterials();
        _characterTattooState.ClearMaterials();
        _characterTransparencyState.ClearMaterials();
        _characterGlassState.ClearMaterials();
        _irisState.ClearMaterials();
        _characterLegacyState.ClearMaterials();
        _characterStockingsState.ClearMaterials();
        _skinState.ClearMaterials();
    }

    public (ulong Skin, ulong CharacterStockings, ulong CharacterLegacy, ulong Iris, ulong CharacterGlass, ulong CharacterTransparency, ulong
        CharacterTattoo, ulong CharacterOcclusion, ulong HairMask) GetAndResetSlowPathCallDeltas()
        => (_skinState.GetAndResetSlowPathCallDelta(),
            _characterStockingsState.GetAndResetSlowPathCallDelta(),
            _characterLegacyState.GetAndResetSlowPathCallDelta(),
            _irisState.GetAndResetSlowPathCallDelta(),
            _characterGlassState.GetAndResetSlowPathCallDelta(),
            _characterTransparencyState.GetAndResetSlowPathCallDelta(),
            _characterTattooState.GetAndResetSlowPathCallDelta(),
            _characterOcclusionState.GetAndResetSlowPathCallDelta(),
            _hairMaskState.GetAndResetSlowPathCallDelta());

    private void OnMtrlLoaded(nint mtrlResourceHandle, nint gameObject)
    {
        var mtrl = (MaterialResourceHandle*)mtrlResourceHandle;
        var shpk = mtrl->ShaderPackageResourceHandle;
        if (shpk == null)
            return;

        var shpkName = mtrl->ShpkNameSpan;
        var shpkState = GetStateForHumanSetup(shpkName)
         ?? GetStateForHumanRender(shpkName)
         ?? GetStateForModelRendererRender(shpkName)
         ?? GetStateForModelRendererUnk(shpkName) ?? GetStateForColorTable(shpkName);

        if (shpkState != null && shpk != shpkState.DefaultShaderPackage)
            shpkState.TryAddMaterial(mtrlResourceHandle);
    }

    private void OnResourceHandleDestructor(Structs.ResourceHandle* handle)
    {
        _skinState.TryRemoveMaterial(handle);
        _characterStockingsState.TryRemoveMaterial(handle);
        _characterLegacyState.TryRemoveMaterial(handle);
        _irisState.TryRemoveMaterial(handle);
        _characterGlassState.TryRemoveMaterial(handle);
        _characterTransparencyState.TryRemoveMaterial(handle);
        _characterTattooState.TryRemoveMaterial(handle);
        _characterOcclusionState.TryRemoveMaterial(handle);
        _hairMaskState.TryRemoveMaterial(handle);
    }

    private ModdedShaderPackageState? GetStateForHumanSetup(MaterialResourceHandle* mtrlResource)
        => mtrlResource == null ? null : GetStateForHumanSetup(mtrlResource->ShpkNameSpan);

    private ModdedShaderPackageState? GetStateForHumanSetup(ReadOnlySpan<byte> shpkName)
        => CharacterStockingsShpkName.SequenceEqual(shpkName) ? _characterStockingsState : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private uint GetTotalMaterialCountForHumanSetup()
        => _characterStockingsState.MaterialCount;

    private ModdedShaderPackageState? GetStateForHumanRender(MaterialResourceHandle* mtrlResource)
        => mtrlResource == null ? null : GetStateForHumanRender(mtrlResource->ShpkNameSpan);

    private ModdedShaderPackageState? GetStateForHumanRender(ReadOnlySpan<byte> shpkName)
        => SkinShpkName.SequenceEqual(shpkName) ? _skinState : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private uint GetTotalMaterialCountForHumanRender()
        => _skinState.MaterialCount;

    private ModdedShaderPackageState? GetStateForModelRendererRender(MaterialResourceHandle* mtrlResource)
        => mtrlResource == null ? null : GetStateForModelRendererRender(mtrlResource->ShpkNameSpan);

    private ModdedShaderPackageState? GetStateForModelRendererRender(ReadOnlySpan<byte> shpkName)
    {
        if (CharacterGlassShpkName.SequenceEqual(shpkName))
            return _characterGlassState;

        if (CharacterTransparencyShpkName.SequenceEqual(shpkName))
            return _characterTransparencyState;

        if (CharacterTattooShpkName.SequenceEqual(shpkName))
            return _characterTattooState;

        if (HairMaskShpkName.SequenceEqual(shpkName))
            return _hairMaskState;

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private uint GetTotalMaterialCountForModelRendererRender()
        => _characterGlassState.MaterialCount
          + _characterTransparencyState.MaterialCount
          + _characterTattooState.MaterialCount
          + _hairMaskState.MaterialCount;

    private ModdedShaderPackageState? GetStateForModelRendererUnk(MaterialResourceHandle* mtrlResource)
        => mtrlResource == null ? null : GetStateForModelRendererUnk(mtrlResource->ShpkNameSpan);

    private ModdedShaderPackageState? GetStateForModelRendererUnk(ReadOnlySpan<byte> shpkName)
    {
        if (IrisShpkName.SequenceEqual(shpkName))
            return _irisState;

        if (CharacterOcclusionShpkName.SequenceEqual(shpkName))
            return _characterOcclusionState;

        if (CharacterStockingsShpkName.SequenceEqual(shpkName))
            return _characterStockingsState;

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private uint GetTotalMaterialCountForModelRendererUnk()
        => _irisState.MaterialCount
          + _characterOcclusionState.MaterialCount
          + _characterStockingsState.MaterialCount;

    private ModdedShaderPackageState? GetStateForColorTable(ReadOnlySpan<byte> shpkName)
        => CharacterLegacyShpkName.SequenceEqual(shpkName) ? _characterLegacyState : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private uint GetTotalMaterialCountForColorTable()
        => _characterLegacyState.MaterialCount;

    private void SetupHssReplacements(CharacterBase* drawObject, uint slotIndex, Span<HumanSetupScalingHook.Replacement> replacements,
        ref int numReplacements, ref IDisposable? pbdDisposable, ref object? shpkLock)
    {
        // If we don't have any on-screen instances of modded characterstockings.shpk, we don't need the slow path at all.
        if (!Enabled || GetTotalMaterialCountForHumanSetup() == 0)
            return;

        var model = drawObject->Models[slotIndex];
        if (model == null)
            return;

        MaterialResourceHandle*   mtrlResource = null;
        ModdedShaderPackageState? shpkState    = null;
        foreach (var material in model->MaterialsSpan)
        {
            if (material.Value == null)
                continue;

            mtrlResource = material.Value->MaterialResourceHandle;
            shpkState    = GetStateForHumanSetup(mtrlResource);
            // Despite this function being called with what designates a model (and therefore potentially many materials),
            // we currently don't need to handle more than one modded ShPk.
            if (shpkState != null)
                break;
        }

        if (shpkState == null || shpkState.MaterialCount == 0)
            return;

        shpkState.IncrementSlowPathCallDelta();

        // This is less performance-critical than the others, as this is called by the game only on draw object creation and slot update.
        // There are still thread safety concerns as it might be called in other threads by plugins.
        shpkLock = shpkState;
        replacements[numReplacements++] = new HumanSetupScalingHook.Replacement((nint)shpkState.ShaderPackageReference,
            (nint)mtrlResource->ShaderPackageResourceHandle,
            (nint)shpkState.DefaultShaderPackage);
    }

    private nint OnRenderHumanMaterial(CharacterBase* human, CSModelRenderer.OnRenderMaterialParams* param)
    {
        // If we don't have any on-screen instances of modded skin.shpk, we don't need the slow path at all.
        if (!Enabled || GetTotalMaterialCountForHumanRender() == 0)
            return _humanOnRenderMaterialHook.Original(human, param);

        var material     = param->Model->Materials[param->MaterialIndex];
        var mtrlResource = material->MaterialResourceHandle;
        var shpkState    = GetStateForHumanRender(mtrlResource);
        if (shpkState == null || shpkState.MaterialCount == 0)
            return _humanOnRenderMaterialHook.Original(human, param);

        shpkState.IncrementSlowPathCallDelta();

        // Performance considerations:
        // - This function is called from several threads simultaneously, hence the need for synchronization in the swapping path ;
        // - Function is called each frame for each material on screen, after culling, i.e. up to thousands of times a frame in crowded areas ;
        // - Swapping path is taken up to hundreds of times a frame.
        // At the time of writing, the lock doesn't seem to have a noticeable impact in either frame rate or CPU usage, but the swapping path shall still be avoided as much as possible.
        lock (shpkState)
        {
            var shpkReference = shpkState.ShaderPackageReference;
            try
            {
                *shpkReference = mtrlResource->ShaderPackageResourceHandle;
                return _humanOnRenderMaterialHook.Original(human, param);
            }
            finally
            {
                *shpkReference = shpkState.DefaultShaderPackage;
            }
        }
    }

    private nint ModelRendererOnRenderMaterialDetour(CSModelRenderer* modelRenderer, ushort* outFlags,
        CSModelRenderer.OnRenderModelParams* param, Material* material, uint materialIndex)
    {
        // If we don't have any on-screen instances of modded characterglass.shpk or others, we don't need the slow path at all.
        if (!Enabled || GetTotalMaterialCountForModelRendererRender() == 0)
            return _modelRendererOnRenderMaterialHook.Original(modelRenderer, outFlags, param, material, materialIndex);

        var mtrlResource = material->MaterialResourceHandle;
        var shpkState    = GetStateForModelRendererRender(mtrlResource);
        if (shpkState == null || shpkState.MaterialCount == 0)
            return _modelRendererOnRenderMaterialHook.Original(modelRenderer, outFlags, param, material, materialIndex);

        shpkState.IncrementSlowPathCallDelta();

        // Same performance considerations as OnRenderHumanMaterial.
        lock (shpkState)
        {
            var shpkReference = shpkState.ShaderPackageReference;
            try
            {
                *shpkReference = mtrlResource->ShaderPackageResourceHandle;
                return _modelRendererOnRenderMaterialHook.Original(modelRenderer, outFlags, param, material, materialIndex);
            }
            finally
            {
                *shpkReference = shpkState.DefaultShaderPackage;
            }
        }
    }

    private void ModelRendererUnkFuncDetour(CSModelRenderer* modelRenderer, ModelRendererStructs.UnkPayload* unkPayload, uint unk2, uint unk3,
        uint unk4, uint unk5)
    {
        if (!Enabled || GetTotalMaterialCountForModelRendererUnk() == 0)
        {
            _modelRendererUnkFuncHook.Original(modelRenderer, unkPayload, unk2, unk3, unk4, unk5);
            return;
        }

        var mtrlResource = GetMaterialResourceHandle(unkPayload);
        var shpkState    = GetStateForModelRendererUnk(mtrlResource);
        if (shpkState == null || shpkState.MaterialCount == 0)
        {
            _modelRendererUnkFuncHook.Original(modelRenderer, unkPayload, unk2, unk3, unk4, unk5);
            return;
        }

        shpkState.IncrementSlowPathCallDelta();

        // Same performance considerations as OnRenderHumanMaterial.
        lock (shpkState)
        {
            var shpkReference = shpkState.ShaderPackageReference;
            try
            {
                *shpkReference = mtrlResource->ShaderPackageResourceHandle;
                _modelRendererUnkFuncHook.Original(modelRenderer, unkPayload, unk2, unk3, unk4, unk5);
            }
            finally
            {
                *shpkReference = shpkState.DefaultShaderPackage;
            }
        }
    }

    private static MaterialResourceHandle* GetMaterialResourceHandle(ModelRendererStructs.UnkPayload* unkPayload)
    {
        // TODO ClientStructs-ify
        var unkPointer    = *(nint*)((nint)unkPayload->ModelResourceHandle + 0xE8) + unkPayload->UnkIndex * 0x24;
        var materialIndex = *(ushort*)(unkPointer + 8);
        var material      = unkPayload->Params->Model->Materials[materialIndex];
        if (material == null)
            return null;

        var mtrlResource = material->MaterialResourceHandle;
        if (mtrlResource == null)
            return null;

        if (mtrlResource->ShaderPackageResourceHandle == null)
        {
            Penumbra.Log.Warning("ShaderReplacementFixer found a MaterialResourceHandle with no shader package");
            return null;
        }

        if (mtrlResource->ShaderPackageResourceHandle->ShaderPackage != unkPayload->ShaderWrapper->ShaderPackage)
        {
            Penumbra.Log.Warning(
                $"ShaderReplacementFixer found a MaterialResourceHandle (0x{(nint)mtrlResource:X}) with an inconsistent shader package (got 0x{(nint)mtrlResource->ShaderPackageResourceHandle->ShaderPackage:X}, expected 0x{(nint)unkPayload->ShaderWrapper->ShaderPackage:X})");
            return null;
        }

        return mtrlResource;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static int GetDataSetExpectedSize(uint dataFlags)
        => (dataFlags & 4) != 0
            ? ColorTable.Size + ((dataFlags & 8) != 0 ? ColorDyeTable.Size : 0)
            : 0;

    private Texture* PrepareColorTableDetour(MaterialResourceHandle* thisPtr, byte stain0Id, byte stain1Id)
    {
        if (thisPtr->DataSetSize < GetDataSetExpectedSize(thisPtr->DataFlags) && Utf8GamePath.IsRooted(thisPtr->FileName.AsSpan()))
            Penumbra.Log.Warning(
                $"Material at {thisPtr->FileName} has data set of size {thisPtr->DataSetSize} bytes, but should have at least {GetDataSetExpectedSize(thisPtr->DataFlags)} bytes. This may cause crashes due to access violations.");

        // If we don't have any on-screen instances of modded characterlegacy.shpk, we don't need the slow path at all.
        if (!Enabled || GetTotalMaterialCountForColorTable() == 0)
            return _prepareColorTableHook.Original(thisPtr, stain0Id, stain1Id);

        var material = thisPtr->Material;
        if (material == null)
            return _prepareColorTableHook.Original(thisPtr, stain0Id, stain1Id);

        var shpkState = GetStateForColorTable(thisPtr->ShpkNameSpan);
        if (shpkState == null || shpkState.MaterialCount == 0)
            return _prepareColorTableHook.Original(thisPtr, stain0Id, stain1Id);

        shpkState.IncrementSlowPathCallDelta();

        // Same performance considerations as HumanSetupScalingDetour.
        lock (shpkState)
        {
            var shpkReference = shpkState.ShaderPackageReference;
            try
            {
                *shpkReference = thisPtr->ShaderPackageResourceHandle;
                return _prepareColorTableHook.Original(thisPtr, stain0Id, stain1Id);
            }
            finally
            {
                *shpkReference = shpkState.DefaultShaderPackage;
            }
        }
    }

    private sealed class ModdedShaderPackageState(ShaderPackageReferenceGetter referenceGetter, DefaultShaderPackageGetter defaultGetter)
    {
        // MaterialResourceHandle set
        private readonly ConcurrentSet<nint> _materials = new();

        // ConcurrentDictionary.Count uses a lock in its current implementation.
        private uint _materialCount;
        private ulong _slowPathCallDelta;

        public uint MaterialCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => _materialCount;
        }

        public ShaderPackageResourceHandle** ShaderPackageReference
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => referenceGetter();
        }

        public ShaderPackageResourceHandle* DefaultShaderPackage
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => defaultGetter();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void TryAddMaterial(nint mtrlResourceHandle)
        {
            if (_materials.TryAdd(mtrlResourceHandle))
                Interlocked.Increment(ref _materialCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void TryRemoveMaterial(Structs.ResourceHandle* handle)
        {
            if (_materials.TryRemove((nint)handle))
                Interlocked.Decrement(ref _materialCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void ClearMaterials()
        {
            _materials.Clear();
            _materialCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void IncrementSlowPathCallDelta()
            => Interlocked.Increment(ref _slowPathCallDelta);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ulong GetAndResetSlowPathCallDelta()
            => Interlocked.Exchange(ref _slowPathCallDelta, 0);
    }

    private delegate ShaderPackageResourceHandle* DefaultShaderPackageGetter();

    private delegate ShaderPackageResourceHandle** ShaderPackageReferenceGetter();
}
