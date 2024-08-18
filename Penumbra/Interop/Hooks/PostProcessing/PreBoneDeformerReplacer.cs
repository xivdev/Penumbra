using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.SafeHandles;
using Penumbra.String;
using Penumbra.String.Classes;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;

namespace Penumbra.Interop.Hooks.PostProcessing;

public sealed unsafe class PreBoneDeformerReplacer : IDisposable, IRequiredService
{
    public static readonly Utf8GamePath PreBoneDeformerPath =
        Utf8GamePath.FromSpan("chara/xls/boneDeformer/human.pbd"u8, MetaDataComputation.All, out var p) ? p : Utf8GamePath.Empty;

    // Approximate name guess.
    private delegate void* CharacterBaseCreateDeformerDelegate(CharacterBase* drawObject, uint slotIndex);

    private readonly Hook<CharacterBaseCreateDeformerDelegate> _humanCreateDeformerHook;

    private readonly CharacterUtility      _utility;
    private readonly CollectionResolver    _collectionResolver;
    private readonly ResourceLoader        _resourceLoader;
    private readonly IFramework            _framework;
    private readonly HumanSetupScalingHook _humanSetupScalingHook;

    public PreBoneDeformerReplacer(CharacterUtility utility, CollectionResolver collectionResolver, ResourceLoader resourceLoader,
        HookManager hooks, IFramework framework, CharacterBaseVTables vTables, HumanSetupScalingHook humanSetupScalingHook)
    {
        _utility                                 =  utility;
        _collectionResolver                      =  collectionResolver;
        _resourceLoader                          =  resourceLoader;
        _framework                               =  framework;
        _humanSetupScalingHook                   =  humanSetupScalingHook;
        _humanSetupScalingHook.SetupReplacements += SetupHssReplacements;
        _humanCreateDeformerHook = hooks.CreateHook<CharacterBaseCreateDeformerDelegate>("HumanCreateDeformer", vTables.HumanVTable[101],
            CreateDeformer, !HookOverrides.Instance.PostProcessing.HumanCreateDeformer).Result;
    }

    public void Dispose()
    {
        _humanCreateDeformerHook.Dispose();
        _humanSetupScalingHook.SetupReplacements -= SetupHssReplacements;
    }

    private SafeResourceHandle GetPreBoneDeformerForCharacter(CharacterBase* drawObject)
    {
        var resolveData = _collectionResolver.IdentifyCollection(&drawObject->DrawObject, true);
        if (resolveData.ModCollection._cache is not { } cache)
            return _resourceLoader.LoadResolvedSafeResource(ResourceCategory.Chara, ResourceType.Pbd, PreBoneDeformerPath.Path, resolveData);

        return cache.CustomResources.Get(ResourceCategory.Chara, ResourceType.Pbd, PreBoneDeformerPath, resolveData);
    }

    private void SetupHssReplacements(CharacterBase* drawObject, uint slotIndex, Span<HumanSetupScalingHook.Replacement> replacements,
        ref int numReplacements, ref IDisposable? pbdDisposable, ref object? shpkLock)
    {
        if (!_framework.IsInFrameworkUpdateThread)
            Penumbra.Log.Warning(
                $"{nameof(PreBoneDeformerReplacer)}.{nameof(SetupHssReplacements)}(0x{(nint)drawObject:X}, {slotIndex}) called out of framework thread");

        var preBoneDeformer = GetPreBoneDeformerForCharacter(drawObject);
        try
        {
            pbdDisposable = preBoneDeformer;
            replacements[numReplacements++] = new HumanSetupScalingHook.Replacement((nint)(&_utility.Address->HumanPbdResource),
                (nint)preBoneDeformer.ResourceHandle,
                _utility.DefaultHumanPbdResource);
        }
        catch
        {
            preBoneDeformer.Dispose();
            throw;
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
