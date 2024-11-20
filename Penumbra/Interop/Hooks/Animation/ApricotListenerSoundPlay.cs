using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.CrashHandler.Buffers;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Called for some sound effects caused by animations or VFX. </summary>
/// <remarks> Actual function got inlined. </remarks>
public sealed unsafe class ApricotListenerSoundPlayCaller : FastHook<ApricotListenerSoundPlayCaller.Delegate>
{
    private readonly GameState           _state;
    private readonly CollectionResolver  _collectionResolver;
    private readonly CrashHandlerService _crashHandler;

    public ApricotListenerSoundPlayCaller(HookManager hooks, GameState state, CollectionResolver collectionResolver,
        CrashHandlerService crashHandler)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _crashHandler       = crashHandler;
        Task = hooks.CreateHook<Delegate>("Apricot Listener Sound Play Caller", Sigs.ApricotListenerSoundPlayCaller, Detour,
            !HookOverrides.Instance.Animation.ApricotListenerSoundPlayCaller);
    }

    public delegate nint Delegate(nint a1, nint a2, float a3);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private nint Detour(nint a1, nint unused, float timeOffset)
    {
        // Short-circuiting and sanity checks done by game.
        var playTime = a1 == nint.Zero ? -1 : *(float*)(a1 + VolatileOffsets.ApricotListenerSoundPlayCaller.PlayTimeOffset);
        if (playTime < 0)
            return Task.Result.Original(a1, unused, timeOffset);

        var someIntermediate = *(nint*)(a1 + VolatileOffsets.ApricotListenerSoundPlayCaller.SomeIntermediate);
        var flags = someIntermediate == nint.Zero
            ? (ushort)0
            : *(ushort*)(someIntermediate + VolatileOffsets.ApricotListenerSoundPlayCaller.Flags);
        if (((flags >> VolatileOffsets.ApricotListenerSoundPlayCaller.BitShift) & 1) == 0)
            return Task.Result.Original(a1, unused, timeOffset);

        Penumbra.Log.Excessive(
            $"[Apricot Listener Sound Play Caller] Invoked on 0x{a1:X} with {unused}, {timeOffset}.");
        // Fetch the IInstanceListenner (sixth argument to inlined call of SoundPlay)
        var apricotIInstanceListenner = *(nint*)(someIntermediate + VolatileOffsets.ApricotListenerSoundPlayCaller.IInstanceListenner);
        if (apricotIInstanceListenner == nint.Zero)
            return Task.Result.Original(a1, unused, timeOffset);

        // In some cases we can obtain the associated caster via vfunc 1.
        var newData    = ResolveData.Invalid;
        var gameObject = (*(delegate* unmanaged<nint, GameObject*>**)apricotIInstanceListenner)[VolatileOffsets.ApricotListenerSoundPlayCaller.CasterVFunc](apricotIInstanceListenner);
        if (gameObject != null)
        {
            newData = _collectionResolver.IdentifyCollection(gameObject, true);
        }
        else
        {
            // for VfxListenner we can obtain the associated draw object as its first member,
            // if the object has different type, drawObject will contain other values or garbage,
            // but only be used in a dictionary pointer lookup, so this does not hurt.
            var drawObject = ((DrawObject**)apricotIInstanceListenner)[1];
            if (drawObject != null)
                newData = _collectionResolver.IdentifyCollection(drawObject, true);
        }

        _crashHandler.LogAnimation(newData.AssociatedGameObject, newData.ModCollection, AnimationInvocationType.ApricotSoundPlay);
        var last = _state.SetAnimationData(newData);
        var ret  = Task.Result.Original(a1, unused, timeOffset);
        _state.RestoreAnimationData(last);
        return ret;
    }
}
