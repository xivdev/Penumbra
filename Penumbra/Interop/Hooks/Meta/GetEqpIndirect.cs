using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
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
        if ((*(byte*)(drawObject + Offsets.GetEqpIndirectSkip1) & 1) == 0 || *(ulong*)(drawObject + Offsets.GetEqpIndirectSkip2) == 0)
            return;

        Penumbra.Log.Excessive($"[Get EQP Indirect] Invoked on {(nint)drawObject:X}.");
        // Shortcut because this is also called all the time.
        // Same thing is checked at the beginning of the original function.

        var       collection = _collectionResolver.IdentifyCollection(drawObject, true);
        using var eqp        = _metaState.ResolveEqpData(collection.ModCollection);
        Task.Result.Original(drawObject);
    }
}
