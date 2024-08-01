using OtterGui.Services;
using Penumbra.CrashHandler.Buffers;
using Penumbra.GameData;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Structs;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Called when some action timelines update. </summary>
public sealed unsafe class ScheduleClipUpdate : FastHook<ScheduleClipUpdate.Delegate>
{
    private readonly GameState           _state;
    private readonly CollectionResolver  _collectionResolver;
    private readonly ObjectManager       _objects;
    private readonly CrashHandlerService _crashHandler;

    public ScheduleClipUpdate(HookManager hooks, GameState state, CollectionResolver collectionResolver, ObjectManager objects,
        CrashHandlerService crashHandler)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _objects            = objects;
        _crashHandler       = crashHandler;
        Task                = hooks.CreateHook<Delegate>("Schedule Clip Update", Sigs.ScheduleClipUpdate, Detour, !HookOverrides.Instance.Animation.ScheduleClipUpdate);
    }

    public delegate void Delegate(ClipScheduler* x);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(ClipScheduler* clipScheduler)
    {
        Penumbra.Log.Excessive($"[Schedule Clip Update] Invoked on {(nint)clipScheduler:X}.");
        var newData = LoadTimelineResources.GetDataFromTimeline(_objects, _collectionResolver, clipScheduler->SchedulerTimeline);
        var last    = _state.SetAnimationData(newData);
        _crashHandler.LogAnimation(newData.AssociatedGameObject, newData.ModCollection, AnimationInvocationType.ScheduleClipUpdate);
        Task.Result.Original(clipScheduler);
        _state.RestoreAnimationData(last);
    }
}
