using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
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
        Task                = hooks.CreateHook<Delegate>("Model Load Complete", vtables.HumanVTable[59], Detour, !HookOverrides.Instance.Meta.ModelLoadComplete);
    }

    public delegate void Delegate(DrawObject* drawObject);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject)
    {
        Penumbra.Log.Excessive($"[Model Load Complete] Invoked on {(nint)drawObject:X}.");
        var collection = _collectionResolver.IdentifyCollection(drawObject, true);
        _metaState.EqpCollection.Push(collection);
        _metaState.EqdpCollection.Push(collection);
        Task.Result.Original(drawObject);
        _metaState.EqpCollection.Pop();
        _metaState.EqdpCollection.Pop();
    }
}
