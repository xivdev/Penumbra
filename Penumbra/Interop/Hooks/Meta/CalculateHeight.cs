using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public sealed unsafe class CalculateHeight : FastHook<CalculateHeight.Delegate>
{
    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public CalculateHeight(HookManager hooks, CollectionResolver collectionResolver, MetaState metaState)
    {
        _collectionResolver = collectionResolver;
        _metaState          = metaState;
        Task = hooks.CreateHook<Delegate>("Calculate Height", (nint)HeightContainer.MemberFunctionPointers.CalculateHeight, Detour,
            !HookOverrides.Instance.Meta.CalculateHeight);
    }

    public delegate ulong Delegate(HeightContainer* character);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ulong Detour(HeightContainer* container)
    {
        var collection = _collectionResolver.IdentifyCollection((GameObject*)container->OwnerObject, true);
        _metaState.RspCollection.Push(collection);
        var ret = Task.Result.Original.Invoke(container);
        Penumbra.Log.Excessive($"[Calculate Height] Invoked on {(nint)container:X} -> {ret}.");
        _metaState.RspCollection.Pop();
        return ret;
    }
}
