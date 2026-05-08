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
    private readonly delegate* unmanaged<BaseClip*, Actor> _getTargetGameObject;
    private readonly delegate* unmanaged<nint, int, int>   _getSourceObjectIndex;
    private readonly delegate* unmanaged<nint, byte>       _canQuerySourceObjectIndex;
    private readonly GameState                             _state;
    private readonly ObjectManager                         _objects;
    private readonly CollectionResolver                    _collectionResolver;
    private readonly CrashHandlerService                   _crashHandler;

    public GetClipResources(HookManager hooks, GameState state, CollectionResolver collectionResolver, ObjectManager objects,
        CrashHandlerService crashHandler)
    {
        _state               = state;
        _collectionResolver  = collectionResolver;
        _objects             = objects;
        _crashHandler        = crashHandler;
        _getTargetGameObject = (delegate* unmanaged <BaseClip*, Actor>)hooks.SigScanner.ScanText(Sigs.BaseClipGetTargetGameObject);
        if (_getTargetGameObject is null)
            throw new Exception($"Could not scan address {Sigs.BaseClipGetTargetGameObject}.");

        _getSourceObjectIndex = (delegate* unmanaged<nint, int, int>)hooks.SigScanner.ScanText(Sigs.BaseClipGetSourceObjectIndex);
        if (_getSourceObjectIndex is null)
            throw new Exception($"Could not scan address {Sigs.BaseClipGetSourceObjectIndex}.");

        _canQuerySourceObjectIndex = (delegate* unmanaged<nint, byte>)hooks.SigScanner.ScanText(Sigs.BaseClipCanQuerySourceObjectIndex);
        if (_canQuerySourceObjectIndex is null)
            throw new Exception($"Could not scan address {Sigs.BaseClipCanQuerySourceObjectIndex}.");

        Task = hooks.CreateHook<Delegate>("Get Clip Resources", Sigs.BaseClipGetClipResources, Detour,
            !HookOverrides.Instance.Animation.GetClipResources);
    }

    public delegate nint Delegate(BaseClip* clip);


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private nint Detour(BaseClip* clip)
    {
        if (clip is null)
            return Task.Result!.Original(clip);

        Actor gameObject;
        // Try to access the clip data.
        // If this is not null we use the check to see whether we can get the index of the source object.
        var clipIndirection = ((nint*)clip)[5];
        if (clipIndirection != nint.Zero && _canQuerySourceObjectIndex(clipIndirection) > 0)
        {
            // If the source object index can be used, use it, even if the object itself is null.
            // Otherwise, try to use the target.
            var index = _getSourceObjectIndex(clipIndirection, 0);
            if (index >= 0 && index < _objects.Count)
                gameObject = _objects[index];
            else
                gameObject = _getTargetGameObject(clip);
        }
        else
        {
            // If we have no clip indirection or can not use the source, try to use the target directly.
            gameObject = _getTargetGameObject(clip);
        }

        // Only apply valid actors here for identification.
        var data = gameObject.Valid ? _collectionResolver.IdentifyCollection(gameObject.AsObject, true) : ResolveData.Invalid;
        if (!data.Valid)
        {
            var ret2 = Task.Result!.Original(clip);
            Penumbra.Log.Excessive($"[Get Clip Resources] Invoked on {(nint)clip:X} -> {ret2:X}.");
            return ret2;
        }

        var last = _state.SetAnimationData(data);
        _crashHandler.LogAnimation(data.AssociatedGameObject, data.ModCollection, AnimationInvocationType.GetClipResources);
        var ret = Task.Result!.Original(clip);
        _state.RestoreAnimationData(last);
        Penumbra.Log.Excessive(
            $"[Get Clip Resources] Invoked on {(nint)clip:X} for {data.AssociatedGameObject:X} ({data.ModCollection}) -> {ret:X}.");
        return ret;
    }
}
