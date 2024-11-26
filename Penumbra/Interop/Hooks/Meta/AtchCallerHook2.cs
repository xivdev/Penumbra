using FFXIVClientStructs.FFXIV.Client.Game.Character;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public unsafe class AtchCallerHook2 : FastHook<AtchCallerHook2.Delegate>, IDisposable
{
    public delegate void Delegate(DrawObjectData* data, uint slot, nint unk, Model playerModel, uint unk2);

    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public AtchCallerHook2(HookManager hooks, MetaState metaState, CollectionResolver collectionResolver)
    {
        _metaState          = metaState;
        _collectionResolver = collectionResolver;
        Task = hooks.CreateHook<Delegate>("AtchCaller2", Sigs.AtchCaller2, Detour,
            metaState.Config.EnableMods && !HookOverrides.Instance.Meta.AtchCaller2);
        if (!HookOverrides.Instance.Meta.AtchCaller2)
            _metaState.Config.ModsEnabled += Toggle;
    }

    private void Detour(DrawObjectData* data, uint slot, nint unk, Model playerModel, uint unk2)
    {
        var collection = _collectionResolver.IdentifyCollection(playerModel.AsDrawObject, true);
        _metaState.AtchCollection.Push(collection);
        Task.Result.Original(data, slot, unk, playerModel, unk2);
        _metaState.AtchCollection.Pop();
        Penumbra.Log.Excessive(
            $"[AtchCaller2] Invoked on 0x{(ulong)data:X} with {slot}, {unk:X}, 0x{playerModel.Address:X}, {unk2}, identified to {collection.ModCollection.AnonymizedName}.");
    }

    public void Dispose()
        => _metaState.Config.ModsEnabled -= Toggle;
}
