using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Called for some animations when using a Parasol. </summary>
public sealed unsafe class SomeParasolAnimation : FastHook<SomeParasolAnimation.Delegate>
{
    private readonly GameState          _state;
    private readonly CollectionResolver _collectionResolver;

    public SomeParasolAnimation(HookManager hooks, GameState state, CollectionResolver collectionResolver)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        Task                = hooks.CreateHook<Delegate>("Some Parasol Animation", Sigs.UnkParasolAnimation, Detour, !HookOverrides.Instance.Animation.SomeParasolAnimation);
    }

    public delegate void Delegate(DrawObject* drawObject, int unk1);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject, int unk1)
    {
        Penumbra.Log.Excessive($"[Some Mount Animation] Invoked on {(nint)drawObject:X} with {unk1}.");
        var last = _state.SetAnimationData(_collectionResolver.IdentifyCollection(drawObject, true));
        Task.Result.Original(drawObject, unk1);
        _state.RestoreAnimationData(last);
    }
}
