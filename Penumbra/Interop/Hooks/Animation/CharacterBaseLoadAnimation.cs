using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.CrashHandler.Buffers;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary>
/// Probably used when the base idle animation gets loaded.
/// Make it aware of the correct collection to load the correct pap files.
/// </summary>
public sealed unsafe class CharacterBaseLoadAnimation : FastHook<CharacterBaseLoadAnimation.Delegate>
{
    private readonly GameState           _state;
    private readonly CollectionResolver  _collectionResolver;
    private readonly DrawObjectState     _drawObjectState;
    private readonly CrashHandlerService _crashHandler;

    public CharacterBaseLoadAnimation(HookManager hooks, GameState state, CollectionResolver collectionResolver,
        DrawObjectState drawObjectState, CrashHandlerService crashHandler)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _drawObjectState    = drawObjectState;
        _crashHandler       = crashHandler;
        Task = hooks.CreateHook<Delegate>("CharacterBase Load Animation", Sigs.CharacterBaseLoadAnimation, Detour,
            !HookOverrides.Instance.Animation.CharacterBaseLoadAnimation);
    }

    public delegate void Delegate(DrawObject* drawBase);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject)
    {
        var lastObj = _state.LastGameObject;
        if (lastObj == nint.Zero && _drawObjectState.TryGetValue((nint)drawObject, out var p))
            lastObj = p.Item1;
        var data = _collectionResolver.IdentifyCollection((GameObject*)lastObj, true);
        var last = _state.SetAnimationData(data);
        _crashHandler.LogAnimation(data.AssociatedGameObject, data.ModCollection, AnimationInvocationType.CharacterBaseLoadAnimation);
        Penumbra.Log.Excessive($"[CharacterBase Load Animation] Invoked on {(nint)drawObject:X}");
        Task.Result.Original(drawObject);
        _state.RestoreAnimationData(last);
    }
}
