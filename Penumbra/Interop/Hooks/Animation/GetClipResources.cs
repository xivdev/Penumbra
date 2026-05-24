using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Clip;
using Luna;
using Penumbra.Collections;
using Penumbra.CrashHandler.Buffers;
using Penumbra.GameData;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.Animation;

public sealed unsafe class GetClipResources : FastHook<GetClipResources.Delegate>
{
    private readonly delegate* unmanaged<BaseClip*, Actor> _clipGameObject;
    private readonly GameState                             _state;
    private readonly CollectionResolver                    _collectionResolver;
    private readonly CrashHandlerService                   _crashHandler;

    public GetClipResources(HookManager hooks, GameState state, CollectionResolver collectionResolver, ObjectManager objects,
        CrashHandlerService crashHandler)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _crashHandler       = crashHandler;
        _clipGameObject     = (delegate* unmanaged < BaseClip*, Actor >)hooks.SigScanner.ScanText(Sigs.BaseClipGetGameObject);
        if (_clipGameObject is null)
            throw new Exception($"Could not scan address {Sigs.BaseClipGetGameObject}.");

        Task = hooks.CreateHook<Delegate>("Get Clip Resources", Sigs.BaseClipGetClipResources, Detour,
            !HookOverrides.Instance.Animation.GetClipResources);
    }

    public delegate nint Delegate(BaseClip* clip);


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private nint Detour(BaseClip* clip)
    {
        if (clip is null)
            return Task.Result!.Original(clip);

        var gameObject = _clipGameObject(clip);
        var data       = gameObject.Valid ? _collectionResolver.IdentifyCollection(gameObject.AsObject, true) : ResolveData.Invalid;
        if (!data.Valid)
        {
            var ret2 = Task.Result!.Original(clip);
            Penumbra.Log.Excessive($"[Get Clip Resources] Invoked on {(nint)clip:X} -> {ret2:X}.");
            return ret2;
        }

        var last = _state.SetSoundData(data);
        _crashHandler.LogAnimation(data.AssociatedGameObject, data.ModCollection, AnimationInvocationType.GetClipResources);
        var ret = Task.Result!.Original(clip);
        _state.RestoreAnimationData(last);
        Penumbra.Log.Excessive(
            $"[Get Clip Resources] Invoked on {(nint)clip:X} for {data.AssociatedGameObject:X} ({data.ModCollection}) -> {ret:X}.");
        return ret;
    }
}
