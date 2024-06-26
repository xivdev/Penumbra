using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public sealed unsafe class GetEqpIndirect2 : FastHook<GetEqpIndirect2.Delegate>
{
    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public GetEqpIndirect2(HookManager hooks, CollectionResolver collectionResolver, MetaState metaState)
    {
        _collectionResolver = collectionResolver;
        _metaState          = metaState;
        Task                = hooks.CreateHook<Delegate>("Get EQP Indirect 2", Sigs.GetEqpIndirect2, Detour, HookSettings.MetaParentHooks);
    }

    public delegate void Delegate(DrawObject* drawObject);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject)
    {
        // Shortcut because this is also called all the time.
        // Same thing is checked at the beginning of the original function.
        if (((*(uint*)((nint)drawObject + Offsets.GetEqpIndirect2Skip) >> 0x12) & 1) == 0)
            return;

        Penumbra.Log.Excessive($"[Get EQP Indirect 2] Invoked on {(nint)drawObject:X}.");
        var collection = _collectionResolver.IdentifyCollection(drawObject, true);
        _metaState.EqpCollection.Push(collection);
        Task.Result.Original(drawObject);
        _metaState.EqpCollection.Pop();
    }
}
