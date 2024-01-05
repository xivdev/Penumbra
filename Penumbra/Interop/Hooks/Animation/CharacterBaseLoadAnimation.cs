using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary>
/// Probably used when the base idle animation gets loaded.
/// Make it aware of the correct collection to load the correct pap files.
/// </summary>
public sealed unsafe class CharacterBaseLoadAnimation : FastHook<CharacterBaseLoadAnimation.Delegate>
{
    private readonly GameState          _state;
    private readonly CollectionResolver _collectionResolver;
    private readonly DrawObjectState    _drawObjectState;

    public CharacterBaseLoadAnimation(HookManager hooks, GameState state, CollectionResolver collectionResolver,
        DrawObjectState drawObjectState)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _drawObjectState    = drawObjectState;
        Task                = hooks.CreateHook<Delegate>("CharacterBase Load Animation", Sigs.CharacterBaseLoadAnimation, Detour, true);
    }

    public delegate void Delegate(DrawObject* drawBase);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject)
    {
        var lastObj = _state.LastGameObject;
        if (lastObj == nint.Zero && _drawObjectState.TryGetValue((nint)drawObject, out var p))
            lastObj = p.Item1;
        var last = _state.SetAnimationData(_collectionResolver.IdentifyCollection((GameObject*)lastObj, true));
        Penumbra.Log.Excessive($"[CharacterBase Load Animation] Invoked on {(nint)drawObject:X}");
        Task.Result.Original(drawObject);
        _state.RestoreAnimationData(last);
    }
}
