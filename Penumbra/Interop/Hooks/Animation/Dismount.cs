using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Called for some animations when dismounting. </summary>
public sealed unsafe class Dismount : FastHook<Dismount.Delegate>
{
    private readonly GameState          _state;
    private readonly CollectionResolver _collectionResolver;

    public Dismount(HookManager hooks, GameState state, CollectionResolver collectionResolver)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        Task                = hooks.CreateHook<Delegate>("Dismount", Sigs.Dismount, Detour, true);
    }

    public delegate void Delegate(nint a1, nint a2);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(nint a1, nint a2)
    {
        Penumbra.Log.Excessive($"[Dismount] Invoked on {a1:X} with {a2:X}.");
        if (a1 == nint.Zero)
        {
            Task.Result.Original(a1, a2);
            return;
        }

        var gameObject = *(GameObject**)(a1 + 8);
        if (gameObject == null)
        {
            Task.Result.Original(a1, a2);
            return;
        }

        var last = _state.SetAnimationData(_collectionResolver.IdentifyCollection(gameObject, true));
        Task.Result.Original(a1, a2);
        _state.RestoreAnimationData(last);
    }
}
