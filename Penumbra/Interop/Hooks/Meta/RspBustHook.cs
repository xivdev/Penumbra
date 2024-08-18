using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Interop.Hooks.Meta;

public unsafe class RspBustHook : FastHook<RspBustHook.Delegate>, IDisposable
{
    public delegate float* Delegate(nint cmpResource, float* storage, SubRace race, byte gender, byte bodyType, byte bustSize);

    private readonly MetaState       _metaState;
    private readonly MetaFileManager _metaFileManager;

    public RspBustHook(HookManager hooks, MetaState metaState, MetaFileManager metaFileManager)
    {
        _metaState       = metaState;
        _metaFileManager = metaFileManager;
        Task = hooks.CreateHook<Delegate>("GetRspBust", Sigs.GetRspBust, Detour,
            metaState.Config.EnableMods && !HookOverrides.Instance.Meta.RspBustHook);
        if (!HookOverrides.Instance.Meta.RspBustHook)
            _metaState.Config.ModsEnabled += Toggle;
    }

    private float* Detour(nint cmpResource, float* storage, SubRace clan, byte gender, byte bodyType, byte bustSize)
    {
        if (gender == 0)
        {
            storage[0] = 1f;
            storage[1] = 1f;
            storage[2] = 1f;
            return storage;
        }

        var ret = storage;
        if (bodyType < 2
         && _metaState.RspCollection.TryPeek(out var collection)
         && collection is { Valid: true, ModCollection.MetaCache: { } cache })
        {
            var bustScale = bustSize / 100f;
            var ptr       = CmpFile.GetDefaults(_metaFileManager, clan, RspAttribute.BustMinX);
            storage[0] = GetValue(0, RspAttribute.BustMinX, RspAttribute.BustMaxX);
            storage[1] = GetValue(1, RspAttribute.BustMinY, RspAttribute.BustMaxY);
            storage[2] = GetValue(2, RspAttribute.BustMinZ, RspAttribute.BustMaxZ);

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            float GetValue(int dimension, RspAttribute min, RspAttribute max)
            {
                var minValue = cache.Rsp.TryGetValue(new RspIdentifier(clan, min), out var minEntry)
                    ? minEntry.Entry.Value
                    : (ptr + dimension)->Value;
                var maxValue = cache.Rsp.TryGetValue(new RspIdentifier(clan, max), out var maxEntry)
                    ? maxEntry.Entry.Value
                    : (ptr + 3 + dimension)->Value;
                return (maxValue - minValue) * bustScale + minValue;
            }
        }
        else
        {
            ret = Task.Result.Original(cmpResource, storage, clan, gender, bodyType, bustSize);
        }

        Penumbra.Log.Excessive(
            $"[GetRspBust] Invoked on 0x{cmpResource:X} with {clan}, {(Gender)(gender + 1)}, {bodyType}, {bustSize}, returned {storage[0]}, {storage[1]}, {storage[2]}.");
        return ret;
    }

    public void Dispose()
        => _metaState.Config.ModsEnabled -= Toggle;
}
