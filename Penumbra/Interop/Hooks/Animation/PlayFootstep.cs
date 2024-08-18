using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Animation;

public sealed unsafe class PlayFootstep : FastHook<PlayFootstep.Delegate>
{
    private readonly GameState          _state;
    private readonly CollectionResolver _collectionResolver;

    public PlayFootstep(HookManager hooks, GameState state, CollectionResolver collectionResolver)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        Task                = hooks.CreateHook<Delegate>("Play Footstep", Sigs.FootStepSound, Detour, !HookOverrides.Instance.Animation.PlayFootstep);
    }

    public delegate void Delegate(GameObject* gameObject, int id, int unk);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(GameObject* gameObject, int id, int unk)
    {
        Penumbra.Log.Excessive($"[Play Footstep] Invoked on 0x{(nint)gameObject:X} with {id}, {unk}.");
        var last = _state.SetAnimationData(_collectionResolver.IdentifyCollection(gameObject, true));
        Task.Result.Original(gameObject, id, unk);
        _state.RestoreAnimationData(last);
    }
}
