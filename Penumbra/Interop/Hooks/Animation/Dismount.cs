using FFXIVClientStructs.FFXIV.Client.Game.Character;
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
        Task                = hooks.CreateHook<Delegate>("Dismount", Sigs.Dismount, Detour, !HookOverrides.Instance.Animation.Dismount);
    }

    public delegate void Delegate(MountContainer* a1, nint a2);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(MountContainer* a1, nint a2)
    {
        Penumbra.Log.Excessive($"[Dismount] Invoked on 0x{(nint)a1:X} with {a2:X}.");
        if (a1 == null)
        {
            Task.Result.Original(a1, a2);
            return;
        }

        var gameObject = a1->OwnerObject;
        if (gameObject == null)
        {
            Task.Result.Original(a1, a2);
            return;
        }

        var last = _state.SetAnimationData(_collectionResolver.IdentifyCollection((GameObject*) gameObject, true));
        Task.Result.Original(a1, a2);
        _state.RestoreAnimationData(last);
    }
}
