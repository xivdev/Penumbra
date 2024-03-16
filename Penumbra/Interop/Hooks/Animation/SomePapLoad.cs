using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.CrashHandler.Buffers;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Unknown what exactly this is, but it seems to load a bunch of paps. </summary>
public sealed unsafe class SomePapLoad : FastHook<SomePapLoad.Delegate>
{
    private readonly GameState           _state;
    private readonly CollectionResolver  _collectionResolver;
    private readonly IObjectTable        _objects;
    private readonly CrashHandlerService _crashHandler;

    public SomePapLoad(HookManager hooks, GameState state, CollectionResolver collectionResolver, IObjectTable objects,
        CrashHandlerService crashHandler)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _objects            = objects;
        _crashHandler       = crashHandler;
        Task                = hooks.CreateHook<Delegate>("Some PAP Load", Sigs.LoadSomePap, Detour, true);
    }

    public delegate void Delegate(nint a1, int a2, nint a3, int a4);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(nint a1, int a2, nint a3, int a4)
    {
        Penumbra.Log.Excessive($"[Some PAP Load] Invoked on 0x{a1:X} with {a2}, {a3}, {a4}.");
        var timelinePtr = a1 + Offsets.TimeLinePtr;
        if (timelinePtr != nint.Zero)
        {
            var actorIdx = (int)(*(*(ulong**)timelinePtr + 1) >> 3);
            if (actorIdx >= 0 && actorIdx < _objects.Length)
            {
                var newData = _collectionResolver.IdentifyCollection((GameObject*)_objects.GetObjectAddress(actorIdx), true);
                var last = _state.SetAnimationData(newData);
                _crashHandler.LogAnimation(newData.AssociatedGameObject, newData.ModCollection, AnimationInvocationType.PapLoad);
                Task.Result.Original(a1, a2, a3, a4);
                _state.RestoreAnimationData(last);
                return;
            }
        }

        Task.Result.Original(a1, a2, a3, a4);
    }
}
