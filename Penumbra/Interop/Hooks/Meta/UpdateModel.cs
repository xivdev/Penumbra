using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public sealed unsafe class UpdateModel : FastHook<UpdateModel.Delegate>
{
    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public UpdateModel(HookManager hooks, CollectionResolver collectionResolver, MetaState metaState)
    {
        _collectionResolver = collectionResolver;
        _metaState          = metaState;
        Task                = hooks.CreateHook<Delegate>("Update Model", Sigs.UpdateModel, Detour, true);
    }

    public delegate void Delegate(DrawObject* drawObject);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject)
    {
        // Shortcut because this is called all the time.
        // Same thing is checked at the beginning of the original function.
        if (*(int*)((nint)drawObject + Offsets.UpdateModelSkip) == 0)
            return;

        Penumbra.Log.Excessive($"[Update Model] Invoked on {(nint)drawObject:X}.");
        var       collection = _collectionResolver.IdentifyCollection(drawObject, true);
        using var eqdp = _metaState.ResolveEqdpData(collection.ModCollection, MetaState.GetDrawObjectGenderRace((nint)drawObject), true, true);
        _metaState.EqpCollection = collection;
        Task.Result.Original(drawObject);
        _metaState.EqpCollection = ResolveData.Invalid;
    }
}
