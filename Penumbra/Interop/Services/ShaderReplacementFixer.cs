using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Communication;
using Penumbra.GameData;
using Penumbra.Interop.Hooks.Resources;
using Penumbra.Services;
using CSModelRenderer = FFXIVClientStructs.FFXIV.Client.Graphics.Render.ModelRenderer;

namespace Penumbra.Interop.Services;

public sealed unsafe class ShaderReplacementFixer : IDisposable, IRequiredService
{
    public static ReadOnlySpan<byte> SkinShpkName
        => "skin.shpk"u8;

    public static ReadOnlySpan<byte> CharacterGlassShpkName
        => "characterglass.shpk"u8;

    private delegate nint CharacterBaseOnRenderMaterialDelegate(CharacterBase* drawObject, CSModelRenderer.OnRenderMaterialParams* param);

    private delegate nint ModelRendererOnRenderMaterialDelegate(CSModelRenderer* modelRenderer, ushort* outFlags,
        CSModelRenderer.OnRenderModelParams* param, Material* material, uint materialIndex);

    private readonly Hook<CharacterBaseOnRenderMaterialDelegate> _humanOnRenderMaterialHook;

    [Signature(Sigs.ModelRendererOnRenderMaterial, DetourName = nameof(ModelRendererOnRenderMaterialDetour))]
    private readonly Hook<ModelRendererOnRenderMaterialDelegate> _modelRendererOnRenderMaterialHook = null!;

    private readonly ResourceHandleDestructor _resourceHandleDestructor;
    private readonly CommunicatorService      _communicator;
    private readonly CharacterUtility         _utility;
    private readonly ModelRenderer            _modelRenderer;

    // MaterialResourceHandle set
    private readonly ConcurrentSet<nint> _moddedSkinShpkMaterials           = new();
    private readonly ConcurrentSet<nint> _moddedCharacterGlassShpkMaterials = new();

    private readonly object _skinLock           = new();
    private readonly object _characterGlassLock = new();

    // ConcurrentDictionary.Count uses a lock in its current implementation.
    private int   _moddedSkinShpkCount;
    private int   _moddedCharacterGlassShpkCount;
    private ulong _skinSlowPathCallDelta;
    private ulong _characterGlassSlowPathCallDelta;

    public bool Enabled { get; internal set; } = true;

    public int ModdedSkinShpkCount
        => _moddedSkinShpkCount;

    public int ModdedCharacterGlassShpkCount
        => _moddedCharacterGlassShpkCount;

    public ShaderReplacementFixer(ResourceHandleDestructor resourceHandleDestructor, CharacterUtility utility, ModelRenderer modelRenderer,
        CommunicatorService communicator, IGameInteropProvider interop, CharacterBaseVTables vTables)
    {
        interop.InitializeFromAttributes(this);
        _resourceHandleDestructor = resourceHandleDestructor;
        _utility                  = utility;
        _modelRenderer            = modelRenderer;
        _communicator             = communicator;
        _humanOnRenderMaterialHook =
            interop.HookFromAddress<CharacterBaseOnRenderMaterialDelegate>(vTables.HumanVTable[62], OnRenderHumanMaterial);
        _communicator.MtrlShpkLoaded.Subscribe(OnMtrlShpkLoaded, MtrlShpkLoaded.Priority.ShaderReplacementFixer);
        _resourceHandleDestructor.Subscribe(OnResourceHandleDestructor, ResourceHandleDestructor.Priority.ShaderReplacementFixer);
        _humanOnRenderMaterialHook.Enable();
        _modelRendererOnRenderMaterialHook.Enable();
    }

    public void Dispose()
    {
        _modelRendererOnRenderMaterialHook.Dispose();
        _humanOnRenderMaterialHook.Dispose();
        _communicator.MtrlShpkLoaded.Unsubscribe(OnMtrlShpkLoaded);
        _resourceHandleDestructor.Unsubscribe(OnResourceHandleDestructor);
        _moddedCharacterGlassShpkMaterials.Clear();
        _moddedSkinShpkMaterials.Clear();
        _moddedCharacterGlassShpkCount = 0;
        _moddedSkinShpkCount           = 0;
    }

    public (ulong Skin, ulong CharacterGlass) GetAndResetSlowPathCallDeltas()
        => (Interlocked.Exchange(ref _skinSlowPathCallDelta, 0), Interlocked.Exchange(ref _characterGlassSlowPathCallDelta, 0));

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

        var shpkName = mtrl->ShpkNameSpan;

        if (SkinShpkName.SequenceEqual(shpkName) && (nint)shpk != _utility.DefaultSkinShpkResource)
            if (_moddedSkinShpkMaterials.TryAdd(mtrlResourceHandle))
                Interlocked.Increment(ref _moddedSkinShpkCount);

        if (CharacterGlassShpkName.SequenceEqual(shpkName) && shpk != _modelRenderer.DefaultCharacterGlassShaderPackage)
            if (_moddedCharacterGlassShpkMaterials.TryAdd(mtrlResourceHandle))
                Interlocked.Increment(ref _moddedCharacterGlassShpkCount);
    }

    private void OnResourceHandleDestructor(Structs.ResourceHandle* handle)
    {
        if (_moddedSkinShpkMaterials.TryRemove((nint)handle))
            Interlocked.Decrement(ref _moddedSkinShpkCount);

        if (_moddedCharacterGlassShpkMaterials.TryRemove((nint)handle))
            Interlocked.Decrement(ref _moddedCharacterGlassShpkCount);
    }

    private nint OnRenderHumanMaterial(CharacterBase* human, CSModelRenderer.OnRenderMaterialParams* param)
    {
        // If we don't have any on-screen instances of modded skin.shpk, we don't need the slow path at all.
        if (!Enabled || _moddedSkinShpkCount == 0)
            return _humanOnRenderMaterialHook.Original(human, param);

        var material     = param->Model->Materials[param->MaterialIndex];
        var mtrlResource = material->MaterialResourceHandle;
        if (!IsMaterialWithShpk(mtrlResource, SkinShpkName))
            return _humanOnRenderMaterialHook.Original(human, param);

        Interlocked.Increment(ref _skinSlowPathCallDelta);

        // Performance considerations:
        // - This function is called from several threads simultaneously, hence the need for synchronization in the swapping path ;
        // - Function is called each frame for each material on screen, after culling, i. e. up to thousands of times a frame in crowded areas ;
        // - Swapping path is taken up to hundreds of times a frame.
        // At the time of writing, the lock doesn't seem to have a noticeable impact in either framerate or CPU usage, but the swapping path shall still be avoided as much as possible.
        lock (_skinLock)
        {
            try
            {
                _utility.Address->SkinShpkResource = (Structs.ResourceHandle*)mtrlResource->ShaderPackageResourceHandle;
                return _humanOnRenderMaterialHook.Original(human, param);
            }
            finally
            {
                _utility.Address->SkinShpkResource = (Structs.ResourceHandle*)_utility.DefaultSkinShpkResource;
            }
        }
    }

    private nint ModelRendererOnRenderMaterialDetour(CSModelRenderer* modelRenderer, ushort* outFlags,
        CSModelRenderer.OnRenderModelParams* param, Material* material, uint materialIndex)
    {
        // If we don't have any on-screen instances of modded characterglass.shpk, we don't need the slow path at all.
        if (!Enabled || _moddedCharacterGlassShpkCount == 0)
            return _modelRendererOnRenderMaterialHook.Original(modelRenderer, outFlags, param, material, materialIndex);

        var mtrlResource = material->MaterialResourceHandle;
        if (!IsMaterialWithShpk(mtrlResource, CharacterGlassShpkName))
            return _modelRendererOnRenderMaterialHook.Original(modelRenderer, outFlags, param, material, materialIndex);

        Interlocked.Increment(ref _characterGlassSlowPathCallDelta);

        // Same performance considerations as above.
        lock (_characterGlassLock)
        {
            try
            {
                *_modelRenderer.CharacterGlassShaderPackage = mtrlResource->ShaderPackageResourceHandle;
                return _modelRendererOnRenderMaterialHook.Original(modelRenderer, outFlags, param, material, materialIndex);
            }
            finally
            {
                *_modelRenderer.CharacterGlassShaderPackage = _modelRenderer.DefaultCharacterGlassShaderPackage;
            }
        }
    }
}
