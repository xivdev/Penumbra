using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public sealed unsafe class GetEqpIndirect : FastHook<GetEqpIndirect.Delegate>
{
    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public GetEqpIndirect(HookManager hooks, CollectionResolver collectionResolver, MetaState metaState)
    {
        _collectionResolver = collectionResolver;
        _metaState          = metaState;
        Task                = hooks.CreateHook<Delegate>("Get EQP Indirect", Sigs.GetEqpIndirect, Detour, true);
    }

    public delegate void Delegate(DrawObject* drawObject);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject)
    {
        // Shortcut because this is also called all the time.
        // Same thing is checked at the beginning of the original function.
        if ((*(byte*)((nint)drawObject + Offsets.GetEqpIndirectSkip1) & 1) == 0 || *(ulong*)((nint)drawObject + Offsets.GetEqpIndirectSkip2) == 0)
            return;

        Penumbra.Log.Excessive($"[Get EQP Indirect] Invoked on {(nint)drawObject:X}.");
        var collection = _collectionResolver.IdentifyCollection(drawObject, true);
        _metaState.EqpCollection.Push(collection);
        Task.Result.Original(drawObject);
        _metaState.EqpCollection.Pop();
    }
}
