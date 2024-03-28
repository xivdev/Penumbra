using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.SafeHandles;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Services;

public sealed unsafe class PreBoneDeformerReplacer : IDisposable, IRequiredService
{
    public static readonly Utf8GamePath PreBoneDeformerPath =
        Utf8GamePath.FromSpan("chara/xls/boneDeformer/human.pbd"u8, out var p) ? p : Utf8GamePath.Empty;

    // Approximate name guesses.
    private delegate void  CharacterBaseSetupScalingDelegate(CharacterBase* drawObject, uint slotIndex);
    private delegate void* CharacterBaseCreateDeformerDelegate(CharacterBase* drawObject, uint slotIndex);

    private readonly Hook<CharacterBaseSetupScalingDelegate>   _humanSetupScalingHook;
    private readonly Hook<CharacterBaseCreateDeformerDelegate> _humanCreateDeformerHook;

    private readonly CharacterUtility   _utility;
    private readonly CollectionResolver _collectionResolver;
    private readonly ResourceLoader     _resourceLoader;
    private readonly IFramework         _framework;

    public PreBoneDeformerReplacer(CharacterUtility utility, CollectionResolver collectionResolver, ResourceLoader resourceLoader,
        IGameInteropProvider interop, IFramework framework, CharacterBaseVTables vTables)
    {
        interop.InitializeFromAttributes(this);
        _utility                 = utility;
        _collectionResolver      = collectionResolver;
        _resourceLoader          = resourceLoader;
        _framework               = framework;
        _humanSetupScalingHook   = interop.HookFromAddress<CharacterBaseSetupScalingDelegate>(vTables.HumanVTable[57], SetupScaling);
        _humanCreateDeformerHook = interop.HookFromAddress<CharacterBaseCreateDeformerDelegate>(vTables.HumanVTable[91], CreateDeformer);
        _humanSetupScalingHook.Enable();
        _humanCreateDeformerHook.Enable();
    }

    public void Dispose()
    {
        _humanCreateDeformerHook.Dispose();
        _humanSetupScalingHook.Dispose();
    }

    private SafeResourceHandle GetPreBoneDeformerForCharacter(CharacterBase* drawObject)
    {
        var resolveData = _collectionResolver.IdentifyCollection(&drawObject->DrawObject, true);
        if (resolveData.ModCollection._cache is not { } cache)
            return _resourceLoader.LoadResolvedSafeResource(ResourceCategory.Chara, ResourceType.Pbd, PreBoneDeformerPath.Path, resolveData);

        return cache.CustomResources.Get(ResourceCategory.Chara, ResourceType.Pbd, PreBoneDeformerPath, resolveData);
    }

    private void SetupScaling(CharacterBase* drawObject, uint slotIndex)
    {
        if (!_framework.IsInFrameworkUpdateThread)
            Penumbra.Log.Warning(
                $"{nameof(PreBoneDeformerReplacer)}.{nameof(SetupScaling)}(0x{(nint)drawObject:X}, {slotIndex}) called out of framework thread");

        using var preBoneDeformer = GetPreBoneDeformerForCharacter(drawObject);
        try
        {
            if (!preBoneDeformer.IsInvalid)
                _utility.Address->HumanPbdResource = (Structs.ResourceHandle*)preBoneDeformer.ResourceHandle;
            _humanSetupScalingHook.Original(drawObject, slotIndex);
        }
        finally
        {
            _utility.Address->HumanPbdResource = (Structs.ResourceHandle*)_utility.DefaultHumanPbdResource;
        }
    }

    private void* CreateDeformer(CharacterBase* drawObject, uint slotIndex)
    {
        if (!_framework.IsInFrameworkUpdateThread)
            Penumbra.Log.Warning(
                $"{nameof(PreBoneDeformerReplacer)}.{nameof(CreateDeformer)}(0x{(nint)drawObject:X}, {slotIndex}) called out of framework thread");

        using var preBoneDeformer = GetPreBoneDeformerForCharacter(drawObject);
        try
        {
            if (!preBoneDeformer.IsInvalid)
                _utility.Address->HumanPbdResource = (Structs.ResourceHandle*)preBoneDeformer.ResourceHandle;
            return _humanCreateDeformerHook.Original(drawObject, slotIndex);
        }
        finally
        {
            _utility.Address->HumanPbdResource = (Structs.ResourceHandle*)_utility.DefaultHumanPbdResource;
        }
    }
}
