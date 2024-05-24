using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public sealed unsafe class ModelLoadComplete : FastHook<ModelLoadComplete.Delegate>
{
    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public ModelLoadComplete(HookManager hooks, CollectionResolver collectionResolver, MetaState metaState, CharacterBaseVTables vtables)
    {
        _collectionResolver = collectionResolver;
        _metaState          = metaState;
        Task                = hooks.CreateHook<Delegate>("Model Load Complete", vtables.HumanVTable[58], Detour, true);
    }

    public delegate void Delegate(DrawObject* drawObject);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject)
    {
        Penumbra.Log.Excessive($"[Model Load Complete] Invoked on {(nint)drawObject:X}.");
        var       collection = _collectionResolver.IdentifyCollection(drawObject, true);
        using var eqdp = _metaState.ResolveEqdpData(collection.ModCollection, MetaState.GetDrawObjectGenderRace((nint)drawObject), true, true);
        _metaState.EqpCollection = collection;
        Task.Result.Original(drawObject);
        _metaState.EqpCollection = ResolveData.Invalid;
    }
}
