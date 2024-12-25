using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Resource;
using JetBrains.Annotations;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.Structs;
using Penumbra.String;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Load a cached TMB resource from SchedulerResourceManagement. </summary>
public sealed unsafe class GetCachedScheduleResource : FastHook<GetCachedScheduleResource.Delegate>
{
    private readonly GameState _state;

    public GetCachedScheduleResource(HookManager hooks, GameState state)
    {
        _state = state;
        Task = hooks.CreateHook<Delegate>("Get Cached Schedule Resource", Sigs.GetCachedScheduleResource, Detour,
            !HookOverrides.Instance.Animation.GetCachedScheduleResource);
    }

    public delegate SchedulerResource* Delegate(SchedulerResourceManagement* a, ScheduleResourceLoadData* b, byte useMap);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private SchedulerResource* Detour(SchedulerResourceManagement* a, ScheduleResourceLoadData* b, byte c)
    {
        if (_state.SkipTmbCache.Value)
        {
            Penumbra.Log.Verbose(
                $"[GetCachedScheduleResource] Called with 0x{(ulong)a:X}, {b->Id}, {new CiByteString(b->Path, MetaDataComputation.None)}, {c} from LoadActionTmb with forced skipping of cache, returning NULL.");
            return null;
        }

        var ret = Task.Result.Original(a, b, c);
        Penumbra.Log.Excessive(
            $"[GetCachedScheduleResource] Called with 0x{(ulong)a:X}, {b->Id}, {new CiByteString(b->Path, MetaDataComputation.None)}, {c}, returning 0x{(ulong)ret:X} ({(ret != null && Resource(ret) != null ? Resource(ret)->FileName().ToString() : "No Path")}).");
        return ret;
    }

    public struct ScheduleResourceLoadData
    {
        [UsedImplicitly]
        public byte* Path;

        [UsedImplicitly]
        public uint Id;
    }


    // #TODO: remove when fixed in CS.
    public static ResourceHandle* Resource(SchedulerResource* r)
        => ((ResourceHandle**)r)[3];
}
