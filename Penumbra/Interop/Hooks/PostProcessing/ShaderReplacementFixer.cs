using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Communication;
using Penumbra.GameData;
using Penumbra.Interop.Hooks.Resources;
using Penumbra.Services;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;
using CSModelRenderer = FFXIVClientStructs.FFXIV.Client.Graphics.Render.ModelRenderer;
using ModelRenderer = Penumbra.Interop.Services.ModelRenderer;

namespace Penumbra.Interop.Hooks.PostProcessing;

public sealed unsafe class ShaderReplacementFixer : IDisposable, IRequiredService
{
    public static ReadOnlySpan<byte> SkinShpkName
        => "skin.shpk"u8;

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

    private readonly Hook<CharacterBaseOnRenderMaterialDelegate> _humanOnRenderMaterialHook;

    private readonly Hook<ModelRendererOnRenderMaterialDelegate> _modelRendererOnRenderMaterialHook;

    private readonly ResourceHandleDestructor _resourceHandleDestructor;
    private readonly CommunicatorService      _communicator;
    private readonly CharacterUtility         _utility;
    private readonly ModelRenderer            _modelRenderer;

    private readonly ModdedShaderPackageState _skinState;
    private readonly ModdedShaderPackageState _irisState;
    private readonly ModdedShaderPackageState _characterGlassState;
    private readonly ModdedShaderPackageState _characterTransparencyState;
    private readonly ModdedShaderPackageState _characterTattooState;
    private readonly ModdedShaderPackageState _characterOcclusionState;
    private readonly ModdedShaderPackageState _hairMaskState;

    public bool Enabled { get; internal set; } = true;

    public uint ModdedSkinShpkCount
        => _skinState.MaterialCount;

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
        CommunicatorService communicator, HookManager hooks, CharacterBaseVTables vTables)
    {
        _resourceHandleDestructor = resourceHandleDestructor;
        _utility                  = utility;
        _modelRenderer            = modelRenderer;
        _communicator             = communicator;

        _skinState = new(
            () => (ShaderPackageResourceHandle**)&_utility.Address->SkinShpkResource,
            () => (ShaderPackageResourceHandle*)_utility.DefaultSkinShpkResource);
        _irisState                  = new(() => _modelRenderer.IrisShaderPackage,                  () => _modelRenderer.DefaultIrisShaderPackage);
        _characterGlassState        = new(() => _modelRenderer.CharacterGlassShaderPackage,        () => _modelRenderer.DefaultCharacterGlassShaderPackage);
        _characterTransparencyState = new(() => _modelRenderer.CharacterTransparencyShaderPackage, () => _modelRenderer.DefaultCharacterTransparencyShaderPackage);
        _characterTattooState       = new(() => _modelRenderer.CharacterTattooShaderPackage,       () => _modelRenderer.DefaultCharacterTattooShaderPackage);
        _characterOcclusionState    = new(() => _modelRenderer.CharacterOcclusionShaderPackage,    () => _modelRenderer.DefaultCharacterOcclusionShaderPackage);
        _hairMaskState              = new(() => _modelRenderer.HairMaskShaderPackage,              () => _modelRenderer.DefaultHairMaskShaderPackage);

        _humanOnRenderMaterialHook = hooks.CreateHook<CharacterBaseOnRenderMaterialDelegate>("Human.OnRenderMaterial", vTables.HumanVTable[64],
            OnRenderHumanMaterial, HookSettings.PostProcessingHooks).Result;
        _modelRendererOnRenderMaterialHook = hooks.CreateHook<ModelRendererOnRenderMaterialDelegate>("ModelRenderer.OnRenderMaterial",
            Sigs.ModelRendererOnRenderMaterial, ModelRendererOnRenderMaterialDetour, HookSettings.PostProcessingHooks).Result;
        _communicator.MtrlShpkLoaded.Subscribe(OnMtrlShpkLoaded, MtrlShpkLoaded.Priority.ShaderReplacementFixer);
        _resourceHandleDestructor.Subscribe(OnResourceHandleDestructor, ResourceHandleDestructor.Priority.ShaderReplacementFixer);
    }

    public void Dispose()
    {
        _modelRendererOnRenderMaterialHook.Dispose();
        _humanOnRenderMaterialHook.Dispose();
        _communicator.MtrlShpkLoaded.Unsubscribe(OnMtrlShpkLoaded);
        _resourceHandleDestructor.Unsubscribe(OnResourceHandleDestructor);
        _hairMaskState.ClearMaterials();
        _characterOcclusionState.ClearMaterials();
        _characterTattooState.ClearMaterials();
        _characterTransparencyState.ClearMaterials();
        _characterGlassState.ClearMaterials();
        _irisState.ClearMaterials();
        _skinState.ClearMaterials();
    }

    public (ulong Skin, ulong Iris, ulong CharacterGlass, ulong CharacterTransparency, ulong CharacterTattoo, ulong CharacterOcclusion, ulong HairMask) GetAndResetSlowPathCallDeltas()
        => (_skinState.GetAndResetSlowPathCallDelta(),
            _irisState.GetAndResetSlowPathCallDelta(),
            _characterGlassState.GetAndResetSlowPathCallDelta(),
            _characterTransparencyState.GetAndResetSlowPathCallDelta(),
            _characterTattooState.GetAndResetSlowPathCallDelta(),
            _characterOcclusionState.GetAndResetSlowPathCallDelta(),
            _hairMaskState.GetAndResetSlowPathCallDelta());

    private static bool IsMaterialWithShpk(MaterialResourceHandle* mtrlResource, ReadOnlySpan<byte> shpkName)
    {
        if (mtrlResource == null)
            return false;

        return shpkName.SequenceEqual(mtrlResource->ShpkNameSpan);
    }

    private void OnMtrlShpkLoaded(nint mtrlResourceHandle, nint gameObject)
    {
        var mtrl = (MaterialResourceHandle*)mtrlResourceHandle;
        var shpk = mtrl->ShaderPackageResourceHandle;
        if (shpk == null)
            return;

        var shpkName  = mtrl->ShpkNameSpan;
        var shpkState = GetStateForHuman(shpkName) ?? GetStateForModelRenderer(shpkName);

        if (shpkState != null && shpk != shpkState.DefaultShaderPackage)
            shpkState.TryAddMaterial(mtrlResourceHandle);
    }

    private void OnResourceHandleDestructor(Structs.ResourceHandle* handle)
    {
        _skinState.TryRemoveMaterial(handle);
        _irisState.TryRemoveMaterial(handle);
        _characterGlassState.TryRemoveMaterial(handle);
        _characterTransparencyState.TryRemoveMaterial(handle);
        _characterTattooState.TryRemoveMaterial(handle);
        _characterOcclusionState.TryRemoveMaterial(handle);
        _hairMaskState.TryRemoveMaterial(handle);
    }

    private ModdedShaderPackageState? GetStateForHuman(MaterialResourceHandle* mtrlResource)
        => mtrlResource == null ? null : GetStateForHuman(mtrlResource->ShpkNameSpan);

    private ModdedShaderPackageState? GetStateForHuman(ReadOnlySpan<byte> shpkName)
    {
        if (SkinShpkName.SequenceEqual(shpkName))
            return _skinState;

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private uint GetTotalMaterialCountForHuman()
        => _skinState.MaterialCount;

    private ModdedShaderPackageState? GetStateForModelRenderer(MaterialResourceHandle* mtrlResource)
        => mtrlResource == null ? null : GetStateForModelRenderer(mtrlResource->ShpkNameSpan);

    private ModdedShaderPackageState? GetStateForModelRenderer(ReadOnlySpan<byte> shpkName)
    {
        if (IrisShpkName.SequenceEqual(shpkName))
            return _irisState;

        if (CharacterGlassShpkName.SequenceEqual(shpkName))
            return _characterGlassState;

        if (CharacterTransparencyShpkName.SequenceEqual(shpkName))
            return _characterTransparencyState;

        if (CharacterTattooShpkName.SequenceEqual(shpkName))
            return _characterTattooState;

        if (CharacterOcclusionShpkName.SequenceEqual(shpkName))
            return _characterOcclusionState;

        if (HairMaskShpkName.SequenceEqual(shpkName))
            return _hairMaskState;

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private uint GetTotalMaterialCountForModelRenderer()
        => _irisState.MaterialCount + _characterGlassState.MaterialCount + _characterTransparencyState.MaterialCount + _characterTattooState.MaterialCount + _characterOcclusionState.MaterialCount + _hairMaskState.MaterialCount;

    private nint OnRenderHumanMaterial(CharacterBase* human, CSModelRenderer.OnRenderMaterialParams* param)
    {
        // If we don't have any on-screen instances of modded skin.shpk, we don't need the slow path at all.
        if (!Enabled || GetTotalMaterialCountForHuman() == 0)
            return _humanOnRenderMaterialHook.Original(human, param);

        var material     = param->Model->Materials[param->MaterialIndex];
        var mtrlResource = material->MaterialResourceHandle;
        var shpkState    = GetStateForHuman(mtrlResource);
        if (shpkState == null)
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
        // If we don't have any on-screen instances of modded characterglass.shpk, we don't need the slow path at all.
        if (!Enabled || GetTotalMaterialCountForModelRenderer() == 0)
            return _modelRendererOnRenderMaterialHook.Original(modelRenderer, outFlags, param, material, materialIndex);

        var mtrlResource = material->MaterialResourceHandle;
        var shpkState    = GetStateForModelRenderer(mtrlResource);
        if (shpkState == null)
            return _modelRendererOnRenderMaterialHook.Original(modelRenderer, outFlags, param, material, materialIndex);

        shpkState.IncrementSlowPathCallDelta();

        // Same performance considerations as above.
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

    private sealed class ModdedShaderPackageState(ShaderPackageReferenceGetter referenceGetter, DefaultShaderPackageGetter defaultGetter)
    {
        // MaterialResourceHandle set
        private readonly ConcurrentSet<nint> _materials = new();

        // ConcurrentDictionary.Count uses a lock in its current implementation.
        private uint _materialCount = 0;

        private ulong _slowPathCallDelta = 0;

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
