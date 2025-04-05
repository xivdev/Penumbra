using FFXIVClientStructs.FFXIV.Client.Game.Character;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public unsafe class AtchCallerHook1 : FastHook<AtchCallerHook1.Delegate>, IDisposable
{
    public delegate void Delegate(DrawObjectData* data, uint slot, nint unk, Model playerModel);

    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public AtchCallerHook1(HookManager hooks, MetaState metaState, CollectionResolver collectionResolver)
    {
        _metaState          = metaState;
        _collectionResolver = collectionResolver;
        Task = hooks.CreateHook<Delegate>("AtchCaller1", Sigs.AtchCaller1, Detour,
            metaState.Config.EnableMods && !HookOverrides.Instance.Meta.AtchCaller1);
        if (!HookOverrides.Instance.Meta.AtchCaller1)
            _metaState.Config.ModsEnabled += Toggle;
    }

    private void Detour(DrawObjectData* data, uint slot, nint unk, Model playerModel)
    {
        var collection = playerModel.Valid ? _collectionResolver.IdentifyCollection(playerModel.AsDrawObject, true) : _collectionResolver.DefaultCollection;
        _metaState.AtchCollection.Push(collection);
        Task.Result.Original(data, slot, unk, playerModel);
        _metaState.AtchCollection.Pop();
        Penumbra.Log.Excessive(
            $"[AtchCaller1] Invoked on 0x{(ulong)data:X} with {slot}, {unk:X}, 0x{playerModel.Address:X}, identified to {collection.ModCollection.Identity.AnonymizedName}.");
    }

    public void Dispose()
        => _metaState.Config.ModsEnabled -= Toggle;
}
