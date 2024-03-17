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
public sealed unsafe class ApricotListenerSoundPlay : FastHook<ApricotListenerSoundPlay.Delegate>
{
    private readonly GameState           _state;
    private readonly CollectionResolver  _collectionResolver;
    private readonly CrashHandlerService _crashHandler;

    public ApricotListenerSoundPlay(HookManager hooks, GameState state, CollectionResolver collectionResolver, CrashHandlerService crashHandler)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _crashHandler       = crashHandler;
        Task                = hooks.CreateHook<Delegate>("Apricot Listener Sound Play", Sigs.ApricotListenerSoundPlay, Detour, true);
    }

    public delegate nint Delegate(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private nint Detour(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6)
    {
        Penumbra.Log.Excessive($"[Apricot Listener Sound Play] Invoked on 0x{a1:X} with {a2}, {a3}, {a4}, {a5}, {a6}.");
        if (a6 == nint.Zero)
            return Task.Result.Original(a1, a2, a3, a4, a5, a6);

        // a6 is some instance of Apricot.IInstanceListenner, in some cases we can obtain the associated caster via vfunc 1.
        var gameObject = (*(delegate* unmanaged<nint, GameObject*>**)a6)[1](a6);
        var newData    = ResolveData.Invalid;
        if (gameObject != null)
        {
            newData = _collectionResolver.IdentifyCollection(gameObject, true);
        }
        else
        {
            // for VfxListenner we can obtain the associated draw object as its first member,
            // if the object has different type, drawObject will contain other values or garbage,
            // but only be used in a dictionary pointer lookup, so this does not hurt.
            var drawObject = ((DrawObject**)a6)[1];
            if (drawObject != null)
                newData = _collectionResolver.IdentifyCollection(drawObject, true);
        }

        _crashHandler.LogAnimation(newData.AssociatedGameObject, newData.ModCollection, AnimationInvocationType.ApricotSoundPlay);
        var last = _state.SetAnimationData(newData);
        var ret  = Task.Result.Original(a1, a2, a3, a4, a5, a6);
        _state.RestoreAnimationData(last);
        return ret;
    }
}
