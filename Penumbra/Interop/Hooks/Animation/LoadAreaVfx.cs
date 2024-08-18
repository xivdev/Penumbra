using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.CrashHandler.Buffers;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Load a ground-based area VFX. </summary>
public sealed unsafe class LoadAreaVfx : FastHook<LoadAreaVfx.Delegate>
{
    private readonly GameState           _state;
    private readonly CollectionResolver  _collectionResolver;
    private readonly CrashHandlerService _crashHandler;

    public LoadAreaVfx(HookManager hooks, GameState state, CollectionResolver collectionResolver, CrashHandlerService crashHandler)
    {
        _state = state;
        _collectionResolver = collectionResolver;
        _crashHandler = crashHandler;
        Task = hooks.CreateHook<Delegate>("Load Area VFX", Sigs.LoadAreaVfx, Detour, !HookOverrides.Instance.Animation.LoadAreaVfx);
    }

    public delegate nint Delegate(uint vfxId, float* pos, GameObject* caster, float unk1, float unk2, byte unk3);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private nint Detour(uint vfxId, float* pos, GameObject* caster, float unk1, float unk2, byte unk3)
    {
        var newData = caster != null
            ? _collectionResolver.IdentifyCollection(caster, true)
            : ResolveData.Invalid;

        var last = _state.SetAnimationData(newData);
        _crashHandler.LogAnimation(newData.AssociatedGameObject, newData.ModCollection, AnimationInvocationType.LoadAreaVfx);
        var ret = Task.Result.Original(vfxId, pos, caster, unk1, unk2, unk3);
        Penumbra.Log.Excessive(
            $"[Load Area VFX] Invoked with {vfxId}, [{pos[0]} {pos[1]} {pos[2]}], 0x{(nint)caster:X}, {unk1}, {unk2}, {unk3} -> 0x{ret:X}.");
        _state.RestoreAnimationData(last);
        return ret;
    }
}
