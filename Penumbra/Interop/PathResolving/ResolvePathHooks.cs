using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Interop.PathResolving;

public unsafe class ResolvePathHooks : IDisposable
{
    public enum Type
    {
        Human,
        Other,
    }

    private delegate nint MPapResolveDelegate(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex, uint sId);
    private delegate nint NamedResolveDelegate(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex, nint name);
    private delegate nint PerSlotResolveDelegate(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex);
    private delegate nint SingleResolveDelegate(nint drawObject, nint pathBuffer, nint pathBufferSize);
    private delegate nint TmbResolveDelegate(nint drawObject, nint pathBuffer, nint pathBufferSize, nint timelineName);
    // Kept separate from NamedResolveDelegate because the 5th parameter has out semantics here, instead of in.
    private delegate nint VfxResolveDelegate(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex, nint unkOutParam);

    private readonly Hook<PerSlotResolveDelegate> _resolveDecalPathHook;
    private readonly Hook<SingleResolveDelegate>  _resolveEidPathHook;
    private readonly Hook<PerSlotResolveDelegate> _resolveImcPathHook;
    private readonly Hook<MPapResolveDelegate>    _resolveMPapPathHook;
    private readonly Hook<PerSlotResolveDelegate> _resolveMdlPathHook;
    private readonly Hook<NamedResolveDelegate>   _resolveMtrlPathHook;
    private readonly Hook<NamedResolveDelegate>   _resolvePapPathHook;
    private readonly Hook<PerSlotResolveDelegate> _resolvePhybPathHook;
    private readonly Hook<PerSlotResolveDelegate> _resolveSklbPathHook;
    private readonly Hook<PerSlotResolveDelegate> _resolveSkpPathHook;
    private readonly Hook<TmbResolveDelegate>     _resolveTmbPathHook;
    private readonly Hook<VfxResolveDelegate>     _resolveVfxPathHook;

    private readonly PathState _parent;

    public ResolvePathHooks(IGameInteropProvider interop, PathState parent, nint* vTable, Type type)
    {
        _parent               = parent;
        _resolveDecalPathHook = Create<PerSlotResolveDelegate>(interop, vTable[83],       ResolveDecal);
        _resolveEidPathHook   = Create<SingleResolveDelegate>(interop,  vTable[85],       ResolveEid);
        _resolveImcPathHook   = Create<PerSlotResolveDelegate>(interop, vTable[81],       ResolveImc);
        _resolveMPapPathHook  = Create<MPapResolveDelegate>(interop,    vTable[79],       ResolveMPap);
        _resolveMdlPathHook   = Create<PerSlotResolveDelegate>(interop, vTable[73], type, ResolveMdl,  ResolveMdlHuman);
        _resolveMtrlPathHook  = Create<NamedResolveDelegate>(interop,   vTable[82],       ResolveMtrl);
        _resolvePapPathHook   = Create<NamedResolveDelegate>(interop,   vTable[76], type, ResolvePap,  ResolvePapHuman);
        _resolvePhybPathHook  = Create<PerSlotResolveDelegate>(interop, vTable[75], type, ResolvePhyb, ResolvePhybHuman);
        _resolveSklbPathHook  = Create<PerSlotResolveDelegate>(interop, vTable[72], type, ResolveSklb, ResolveSklbHuman);
        _resolveSkpPathHook   = Create<PerSlotResolveDelegate>(interop, vTable[74], type, ResolveSkp,  ResolveSkpHuman);
        _resolveTmbPathHook   = Create<TmbResolveDelegate>(interop,     vTable[77],       ResolveTmb);
        _resolveVfxPathHook   = Create<VfxResolveDelegate>(interop,     vTable[84],       ResolveVfx);
    }

    public void Enable()
    {
        _resolveDecalPathHook.Enable();
        _resolveEidPathHook.Enable();
        _resolveImcPathHook.Enable();
        _resolveMPapPathHook.Enable();
        _resolveMdlPathHook.Enable();
        _resolveMtrlPathHook.Enable();
        _resolvePapPathHook.Enable();
        _resolvePhybPathHook.Enable();
        _resolveSklbPathHook.Enable();
        _resolveSkpPathHook.Enable();
        _resolveTmbPathHook.Enable();
        _resolveVfxPathHook.Enable();
    }

    public void Disable()
    {
        _resolveDecalPathHook.Disable();
        _resolveEidPathHook.Disable();
        _resolveImcPathHook.Disable();
        _resolveMPapPathHook.Disable();
        _resolveMdlPathHook.Disable();
        _resolveMtrlPathHook.Disable();
        _resolvePapPathHook.Disable();
        _resolvePhybPathHook.Disable();
        _resolveSklbPathHook.Disable();
        _resolveSkpPathHook.Disable();
        _resolveTmbPathHook.Disable();
        _resolveVfxPathHook.Disable();
    }

    public void Dispose()
    {
        _resolveDecalPathHook.Dispose();
        _resolveEidPathHook.Dispose();
        _resolveImcPathHook.Dispose();
        _resolveMPapPathHook.Dispose();
        _resolveMdlPathHook.Dispose();
        _resolveMtrlPathHook.Dispose();
        _resolvePapPathHook.Dispose();
        _resolvePhybPathHook.Dispose();
        _resolveSklbPathHook.Dispose();
        _resolveSkpPathHook.Dispose();
        _resolveTmbPathHook.Dispose();
        _resolveVfxPathHook.Dispose();
    }

    private nint ResolveDecal(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex)
        => ResolvePath(drawObject, _resolveDecalPathHook.Original(drawObject, pathBuffer, pathBufferSize, slotIndex));

    private nint ResolveEid(nint drawObject, nint pathBuffer, nint pathBufferSize)
        => ResolvePath(drawObject, _resolveEidPathHook.Original(drawObject, pathBuffer, pathBufferSize));

    private nint ResolveImc(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex)
        => ResolvePath(drawObject, _resolveImcPathHook.Original(drawObject, pathBuffer, pathBufferSize, slotIndex));

    private nint ResolveMPap(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex, uint unkSId)
        => ResolvePath(drawObject, _resolveMPapPathHook.Original(drawObject, pathBuffer, pathBufferSize, slotIndex, unkSId));

    private nint ResolveMdl(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex)
        => ResolvePath(drawObject, _resolveMdlPathHook.Original(drawObject, pathBuffer, pathBufferSize, slotIndex));

    private nint ResolveMtrl(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex, nint mtrlFileName)
        => ResolvePath(drawObject, _resolveMtrlPathHook.Original(drawObject, pathBuffer, pathBufferSize, slotIndex, mtrlFileName));

    private nint ResolvePap(nint drawObject, nint pathBuffer, nint pathBufferSize, uint unkAnimationIndex, nint animationName)
        => ResolvePath(drawObject, _resolvePapPathHook.Original(drawObject, pathBuffer, pathBufferSize, unkAnimationIndex, animationName));

    private nint ResolvePhyb(nint drawObject, nint pathBuffer, nint pathBufferSize, uint partialSkeletonIndex)
        => ResolvePath(drawObject, _resolvePhybPathHook.Original(drawObject, pathBuffer, pathBufferSize, partialSkeletonIndex));

    private nint ResolveSklb(nint drawObject, nint pathBuffer, nint pathBufferSize, uint partialSkeletonIndex)
        => ResolvePath(drawObject, _resolveSklbPathHook.Original(drawObject, pathBuffer, pathBufferSize, partialSkeletonIndex));

    private nint ResolveSkp(nint drawObject, nint pathBuffer, nint pathBufferSize, uint partialSkeletonIndex)
        => ResolvePath(drawObject, _resolveSkpPathHook.Original(drawObject, pathBuffer, pathBufferSize, partialSkeletonIndex));

    private nint ResolveTmb(nint drawObject, nint pathBuffer, nint pathBufferSize, nint timelineName)
        => ResolvePath(drawObject, _resolveTmbPathHook.Original(drawObject, pathBuffer, pathBufferSize, timelineName));

    private nint ResolveVfx(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex, nint unkOutParam)
        => ResolvePath(drawObject, _resolveVfxPathHook.Original(drawObject, pathBuffer, pathBufferSize, slotIndex, unkOutParam));


    private nint ResolveMdlHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex)
    {
        var data = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        using var eqdp = slotIndex > 9 || _parent.InInternalResolve
            ? DisposableContainer.Empty
            : _parent.MetaState.ResolveEqdpData(data.ModCollection, MetaState.GetHumanGenderRace(drawObject), slotIndex < 5, slotIndex > 4);
        return ResolvePath(data, _resolveMdlPathHook.Original(drawObject, pathBuffer, pathBufferSize, slotIndex));
    }

    private nint ResolvePapHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint unkAnimationIndex, nint animationName)
    {
        using var est = GetEstChanges(drawObject, out var data);
        return ResolvePath(data, _resolvePapPathHook.Original(drawObject, pathBuffer, pathBufferSize, unkAnimationIndex, animationName));
    }

    private nint ResolvePhybHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint partialSkeletonIndex)
    {
        using var est = GetEstChanges(drawObject, out var data);
        return ResolvePath(data, _resolvePhybPathHook.Original(drawObject, pathBuffer, pathBufferSize, partialSkeletonIndex));
    }

    private nint ResolveSklbHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint partialSkeletonIndex)
    {
        using var est = GetEstChanges(drawObject, out var data);
        return ResolvePath(data, _resolveSklbPathHook.Original(drawObject, pathBuffer, pathBufferSize, partialSkeletonIndex));
    }

    private nint ResolveSkpHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint partialSkeletonIndex)
    {
        using var est = GetEstChanges(drawObject, out var data);
        return ResolvePath(data, _resolveSkpPathHook.Original(drawObject, pathBuffer, pathBufferSize, partialSkeletonIndex));
    }

    private DisposableContainer GetEstChanges(nint drawObject, out ResolveData data)
    {
        data = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        if (_parent.InInternalResolve)
        {
            return DisposableContainer.Empty;
        }
        return new DisposableContainer(data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility, EstManipulation.EstType.Face),
            data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility,                            EstManipulation.EstType.Body),
            data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility,                            EstManipulation.EstType.Hair),
            data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility,                            EstManipulation.EstType.Head));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Hook<T> Create<T>(IGameInteropProvider interop, nint address, Type type, T other, T human) where T : Delegate
    {
        var del = type switch
        {
            Type.Human  => human,
            _           => other,
        };
        return interop.HookFromAddress(address, del);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Hook<T> Create<T>(IGameInteropProvider interop, nint address, T del) where T : Delegate
        => interop.HookFromAddress(address, del);


    // Implementation
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private nint ResolvePath(nint drawObject, nint path)
    {
        var data = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        return ResolvePath(data, path);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private nint ResolvePath(ResolveData data, nint path)
        => _parent.ResolvePath(data, path);
}
