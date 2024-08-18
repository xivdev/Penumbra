using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Called for some animations when mounted or mounting. </summary>
public sealed unsafe class SomeMountAnimation : FastHook<SomeMountAnimation.Delegate>
{
    private readonly GameState          _state;
    private readonly CollectionResolver _collectionResolver;

    public SomeMountAnimation(HookManager hooks, GameState state, CollectionResolver collectionResolver)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        Task                = hooks.CreateHook<Delegate>("Some Mount Animation", Sigs.UnkMountAnimation, Detour, !HookOverrides.Instance.Animation.SomeMountAnimation);
    }

    public delegate void Delegate(DrawObject* drawObject, uint unk1, byte unk2, uint unk3);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject, uint unk1, byte unk2, uint unk3)
    {
        Penumbra.Log.Excessive($"[Some Mount Animation] Invoked on {(nint)drawObject:X} with {unk1}, {unk2}, {unk3}.");
        var last = _state.SetAnimationData(_collectionResolver.IdentifyCollection(drawObject, true));
        Task.Result.Original(drawObject, unk1, unk2, unk3);
        _state.RestoreAnimationData(last);
    }
}
