using Dalamud.Plugin.Services;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Called when some action timelines update. </summary>
public sealed unsafe class ScheduleClipUpdate : FastHook<ScheduleClipUpdate.Delegate>
{
    private readonly GameState          _state;
    private readonly CollectionResolver _collectionResolver;
    private readonly IObjectTable       _objects;

    public ScheduleClipUpdate(HookManager hooks, GameState state, CollectionResolver collectionResolver, IObjectTable objects)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _objects            = objects;
        Task                = hooks.CreateHook<Delegate>("Schedule Clip Update", Sigs.ScheduleClipUpdate, Detour, true);
    }

    public delegate void Delegate(ClipScheduler* x);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(ClipScheduler* clipScheduler)
    {
        Penumbra.Log.Excessive($"[Schedule Clip Update] Invoked on {(nint)clipScheduler:X}.");
        var last = _state.SetAnimationData(
            LoadTimelineResources.GetDataFromTimeline(_objects, _collectionResolver, clipScheduler->SchedulerTimeline));
        Task.Result.Original(clipScheduler);
        _state.RestoreAnimationData(last);
    }
}
