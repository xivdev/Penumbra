using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

/// <summary>
/// GMP. This gets called every time when changing visor state, and it accesses the gmp file itself,
/// but it only applies a changed gmp file after a redraw for some reason.
/// </summary>
public sealed unsafe class SetupVisor : FastHook<SetupVisor.Delegate>
{
    private readonly CollectionResolver _collectionResolver;
    private readonly MetaState          _metaState;

    public SetupVisor(HookManager hooks, CollectionResolver collectionResolver, MetaState metaState)
    {
        _collectionResolver = collectionResolver;
        _metaState          = metaState;
        Task                = hooks.CreateHook<Delegate>("Setup Visor", Sigs.SetupVisor, Detour, !HookOverrides.Instance.Meta.SetupVisor);
    }

    public delegate byte Delegate(DrawObject* drawObject, ushort modelId, byte visorState);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private byte Detour(DrawObject* drawObject, ushort modelId, byte visorState)
    {
        var collection = _collectionResolver.IdentifyCollection(drawObject, true);
        _metaState.GmpCollection.Push((collection, modelId));
        var ret = Task.Result.Original.Invoke(drawObject, modelId, visorState);
        Penumbra.Log.Excessive($"[Setup Visor] Invoked on {(nint)drawObject:X} with {modelId}, {visorState} -> {ret}.");
        _metaState.GmpCollection.Pop();
        return ret;
    }
}
