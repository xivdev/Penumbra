using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Resource;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.Services;
using Penumbra.String;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Load a Action TMB. </summary>
public sealed unsafe class LoadActionTmb : FastHook<LoadActionTmb.Delegate>
{
    private readonly GameState                          _state;
    private readonly SchedulerResourceManagementService _scheduler;

    public LoadActionTmb(HookManager hooks, GameState state, SchedulerResourceManagementService scheduler)
    {
        _state     = state;
        _scheduler = scheduler;
        Task       = hooks.CreateHook<Delegate>("Load Action TMB", Sigs.LoadActionTmb, Detour, !HookOverrides.Instance.Animation.LoadActionTmb);
    }

    public delegate SchedulerResource* Delegate(SchedulerResourceManagement* scheduler,
        GetCachedScheduleResource.ScheduleResourceLoadData* loadData, nint b, byte c, byte d, byte e);


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private SchedulerResource* Detour(SchedulerResourceManagement* scheduler, GetCachedScheduleResource.ScheduleResourceLoadData* loadData,
        nint b, byte c, byte d, byte e)
    {
        _state.InLoadActionTmb.Value = true;
        SchedulerResource* ret;
        if (ShouldSkipCache(loadData))
        {
            _state.SkipTmbCache.Value = true;
            ret                       = Task.Result.Original(scheduler, loadData, b, c, d, 1);
            Penumbra.Log.Verbose(
                $"[LoadActionTMB] Called with 0x{(ulong)scheduler:X}, {loadData->Id}, {new CiByteString(loadData->Path, MetaDataComputation.None)}, 0x{b:X}, {c}, {d}, {e}, forced no-cache use, returned 0x{(ulong)ret:X} ({(ret != null && GetCachedScheduleResource.Resource(ret) != null ? GetCachedScheduleResource.Resource(ret)->FileName().ToString() : "No Path")}).");
            _state.SkipTmbCache.Value = false;
        }
        else
        {
            ret = Task.Result.Original(scheduler, loadData, b, c, d, e);
            Penumbra.Log.Excessive(
                $"[LoadActionTMB] Called with 0x{(ulong)scheduler:X}, {loadData->Id}, {new CiByteString(loadData->Path)}, 0x{b:X}, {c}, {d}, {e}, returned 0x{(ulong)ret:X} ({(ret != null && GetCachedScheduleResource.Resource(ret) != null ? GetCachedScheduleResource.Resource(ret)->FileName().ToString() : "No Path")}).");
        }

        _state.InLoadActionTmb.Value = false;

        return ret;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool ShouldSkipCache(GetCachedScheduleResource.ScheduleResourceLoadData* loadData)
        => _scheduler.Contains(loadData->Id);
}
