using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

/// <summary> The actual function is inlined, so we need to hook its only callsite: Human.UpdateRender instead. </summary>
public sealed unsafe class UpdateRender : FastHook<UpdateRender.Delegate>
{
    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public UpdateRender(HookManager hooks, CollectionResolver collectionResolver, MetaState metaState, CharacterBaseVTables vTables)
    {
        _collectionResolver = collectionResolver;
        _metaState = metaState;
        Task = hooks.CreateHook<Delegate>("Human.UpdateRender", vTables.HumanVTable[4], Detour, !HookOverrides.Instance.Meta.UpdateRender);
    }

    public delegate void Delegate(DrawObject* drawObject);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject)
    {
        Penumbra.Log.Excessive($"[Human.UpdateRender] Invoked on {(nint)drawObject:X}.");
        var collection = _collectionResolver.IdentifyCollection(drawObject, true);
        _metaState.EqpCollection.Push(collection);
        Task.Result.Original(drawObject);
        _metaState.EqpCollection.Pop();
    }
}
