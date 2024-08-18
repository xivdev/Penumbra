using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.CrashHandler.Buffers;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Seems to load character actions when zoning or changing class, maybe. </summary>
public sealed unsafe class SomeActionLoad : FastHook<SomeActionLoad.Delegate>
{
    private readonly GameState           _state;
    private readonly CollectionResolver  _collectionResolver;
    private readonly CrashHandlerService _crashHandler;

    public SomeActionLoad(HookManager hooks, GameState state, CollectionResolver collectionResolver, CrashHandlerService crashHandler)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _crashHandler       = crashHandler;
        Task                = hooks.CreateHook<Delegate>("Some Action Load", Sigs.LoadSomeAction, Detour, !HookOverrides.Instance.Animation.SomeActionLoad);
    }

    public delegate void Delegate(TimelineContainer* timelineManager);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(TimelineContainer* timelineManager)
    {
        var newData = _collectionResolver.IdentifyCollection((GameObject*)timelineManager->OwnerObject, true);
        var last    = _state.SetAnimationData(newData);
        Penumbra.Log.Excessive($"[Some Action Load] Invoked on 0x{(nint)timelineManager:X}.");
        _crashHandler.LogAnimation(newData.AssociatedGameObject, newData.ModCollection, AnimationInvocationType.ActionLoad);
        Task.Result.Original(timelineManager);
        _state.RestoreAnimationData(last);
    }
}
