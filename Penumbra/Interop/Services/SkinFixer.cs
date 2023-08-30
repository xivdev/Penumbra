using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.SafeHandles;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Services;

public unsafe class SkinFixer : IDisposable
{
    public static readonly Utf8GamePath SkinShpkPath =
        Utf8GamePath.FromSpan("shader/sm5/shpk/skin.shpk"u8, out var p) ? p : Utf8GamePath.Empty;

    [Signature(Sigs.HumanVTable, ScanType = ScanType.StaticAddress)]
    private readonly nint* _humanVTable = null!;

    private delegate nint OnRenderMaterialDelegate(nint drawObject, OnRenderMaterialParams* param);

    [StructLayout(LayoutKind.Explicit)]
    private struct OnRenderMaterialParams
    {
        [FieldOffset(0x0)]
        public Model* Model;
        [FieldOffset(0x8)]
        public uint MaterialIndex;
    }

    private readonly Hook<OnRenderMaterialDelegate> _onRenderMaterialHook;

    private readonly CollectionResolver _collectionResolver;
    private readonly GameEventManager   _gameEvents;
    private readonly ResourceLoader     _resources;
    private readonly CharacterUtility   _utility;

    private readonly ConcurrentDictionary<nint /* CharacterBase* */, SafeResourceHandle> _skinShpks = new();

    private readonly object _lock = new();
    
    private bool  _enabled = true;
    private int   _moddedSkinShpkCount = 0;
    private ulong _slowPathCallDelta = 0;
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public int ModdedSkinShpkCount
        => _moddedSkinShpkCount;

    public SkinFixer(CollectionResolver collectionResolver, GameEventManager gameEvents, ResourceLoader resources, CharacterUtility utility, DrawObjectState _)
    {
        SignatureHelper.Initialise(this);
        _collectionResolver   = collectionResolver;
        _gameEvents           = gameEvents;
        _resources            = resources;
        _utility              = utility;
        _onRenderMaterialHook = Hook<OnRenderMaterialDelegate>.FromAddress(_humanVTable[62], OnRenderHumanMaterial);
        _gameEvents.CharacterBaseCreated    += OnCharacterBaseCreated; // The dependency on DrawObjectState shall ensure that this handler is registered after its one.
        _gameEvents.CharacterBaseDestructor += OnCharacterBaseDestructor;
        _onRenderMaterialHook.Enable();
    }

    ~SkinFixer()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        _onRenderMaterialHook.Dispose();
        _gameEvents.CharacterBaseCreated    -= OnCharacterBaseCreated;
        _gameEvents.CharacterBaseDestructor -= OnCharacterBaseDestructor;
        foreach (var skinShpk in _skinShpks.Values)
            skinShpk.Dispose();
        _skinShpks.Clear();
        _moddedSkinShpkCount = 0;
    }

    public ulong GetAndResetSlowPathCallDelta()
        => Interlocked.Exchange(ref _slowPathCallDelta, 0);

    private void OnCharacterBaseCreated(uint modelCharaId, nint customize, nint equipment, nint drawObject)
    {
        if (((CharacterBase*)drawObject)->GetModelType() != CharacterBase.ModelType.Human)
            return;

        Task.Run(delegate
        {
            var skinShpk = SafeResourceHandle.CreateInvalid();
            try
            {
                var data = _collectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
                if (data.Valid)
                {
                    var loadedShpk = _resources.LoadResolvedResource(ResourceCategory.Shader, ResourceType.Shpk, SkinShpkPath.Path, data);
                    skinShpk = new SafeResourceHandle((ResourceHandle*)loadedShpk, incRef: false);
                }
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Error while resolving skin.shpk for human {drawObject:X}: {e}");
            }

            if (!skinShpk.IsInvalid)
            {
                if (_skinShpks.TryAdd(drawObject, skinShpk))
                {
                    if ((nint)skinShpk.ResourceHandle != _utility.DefaultSkinShpkResource)
                        Interlocked.Increment(ref _moddedSkinShpkCount);
                }
                else
                    skinShpk.Dispose();
            }
        });
    }

    private void OnCharacterBaseDestructor(nint characterBase)
    {
        if (_skinShpks.Remove(characterBase, out var skinShpk))
        {
            var handle = skinShpk.ResourceHandle;
            skinShpk.Dispose();
            if ((nint)handle != _utility.DefaultSkinShpkResource)
                Interlocked.Decrement(ref _moddedSkinShpkCount);
        }
    }

    private nint OnRenderHumanMaterial(nint human, OnRenderMaterialParams* param)
    {
        if (!_enabled ||                 // Can be toggled on the debug tab.
            _moddedSkinShpkCount == 0 || // If we don't have any on-screen instances of modded skin.shpk, we don't need the slow path at all.
            !_skinShpks.TryGetValue(human, out var skinShpk))
            return _onRenderMaterialHook!.Original(human, param);

        var material = param->Model->Materials[param->MaterialIndex];
        var shpkResource = ((Structs.MtrlResource*)material->MaterialResourceHandle)->ShpkResourceHandle;
        if ((nint)shpkResource != (nint)skinShpk.ResourceHandle)
            return _onRenderMaterialHook!.Original(human, param);

        Interlocked.Increment(ref _slowPathCallDelta);

        // Performance considerations:
        // - This function is called from several threads simultaneously, hence the need for synchronization in the swapping path ;
        // - Function is called each frame for each material on screen, after culling, i. e. up to thousands of times a frame in crowded areas ;
        // - Swapping path is taken up to hundreds of times a frame.
        // At the time of writing, the lock doesn't seem to have a noticeable impact in either framerate or CPU usage, but the swapping path shall still be avoided as much as possible.
        lock (_lock)
            try
            {
                _utility.Address->SkinShpkResource = (Structs.ResourceHandle*)skinShpk.ResourceHandle;
                return _onRenderMaterialHook!.Original(human, param);
            }
            finally
            {
                _utility.Address->SkinShpkResource = (Structs.ResourceHandle*)_utility.DefaultSkinShpkResource;
            }
    }
}
