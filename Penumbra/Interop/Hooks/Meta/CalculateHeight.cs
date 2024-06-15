using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Interop.PathResolving;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace Penumbra.Interop.Hooks.Meta;

public sealed unsafe class CalculateHeight : FastHook<CalculateHeight.Delegate>
{
    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public CalculateHeight(HookManager hooks, CollectionResolver collectionResolver, MetaState metaState)
    {
        _collectionResolver = collectionResolver;
        _metaState = metaState;
        Task = hooks.CreateHook<Delegate>("Calculate Height", (nint)Character.MemberFunctionPointers.CalculateHeight, Detour, true);
    }

    public delegate ulong Delegate(Character* character);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ulong Detour(Character* character)
    {
        _metaState.RspCollection = _collectionResolver.IdentifyCollection((GameObject*)character, true);
        var ret = Task.Result.Original.Invoke(character);
        Penumbra.Log.Excessive($"[Calculate Height] Invoked on {(nint)character:X} -> {ret}.");
        _metaState.RspCollection = ResolveData.Invalid;
        return ret;
    }
}
