using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public sealed unsafe class RspSetupCharacter : FastHook<RspSetupCharacter.Delegate>
{
    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public RspSetupCharacter(HookManager hooks, CollectionResolver collectionResolver, MetaState metaState)
    {
        _collectionResolver = collectionResolver;
        _metaState          = metaState;
        Task                = hooks.CreateHook<Delegate>("RSP Setup Character", Sigs.RspSetupCharacter, Detour, !HookOverrides.Instance.Meta.RspSetupCharacter);
    }

    public delegate void Delegate(DrawObject* drawObject, nint unk2, float unk3, nint unk4, byte unk5);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(DrawObject* drawObject, nint unk2, float unk3, nint unk4, byte unk5)
    {
        Penumbra.Log.Excessive($"[RSP Setup Character] Invoked on {(nint)drawObject:X} with {unk2}, {unk3}, {unk4}, {unk5}.");
        // Skip if we are coming from ChangeCustomize.
        if (_metaState.CustomizeChangeCollection.Valid)
        {
            Task.Result.Original.Invoke(drawObject, unk2, unk3, unk4, unk5);
            return;
        }

        var collection = _collectionResolver.IdentifyCollection(drawObject, true);
        _metaState.RspCollection.Push(collection);
        Task.Result.Original.Invoke(drawObject, unk2, unk3, unk4, unk5);
        _metaState.RspCollection.Pop();
    }
}
