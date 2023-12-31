using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Seems to load character actions when zoning or changing class, maybe. </summary>
public sealed unsafe class SomeActionLoad : FastHook<SomeActionLoad.Delegate>
{
    private readonly GameState          _state;
    private readonly CollectionResolver _collectionResolver;

    public SomeActionLoad(HookManager hooks, GameState state, CollectionResolver collectionResolver)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        Task                = hooks.CreateHook<Delegate>("Some Action Load", Sigs.LoadSomeAction, Detour, true);
    }

    public delegate void Delegate(ActionTimelineManager* timelineManager);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(ActionTimelineManager* timelineManager)
    {
        var last = _state.SetAnimationData(_collectionResolver.IdentifyCollection((GameObject*)timelineManager->Parent, true));
        Penumbra.Log.Excessive($"[Some Action Load] Invoked on 0x{(nint)timelineManager:X}.");
        Task.Result.Original(timelineManager);
        _state.RestoreAnimationData(last);
    }
}
