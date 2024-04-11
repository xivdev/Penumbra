using System.Text.Unicode;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Interop.Hooks.Resources;

public sealed unsafe class ResolvePathHooksBase : IDisposable
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

    public ResolvePathHooksBase(string name, HookManager hooks, PathState parent, nint* vTable, Type type)
    {
        _parent = parent;
        // @formatter:off
        _resolveDecalPathHook = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolveDecal)}", hooks, vTable[83], ResolveDecal);
        _resolveEidPathHook   = Create<SingleResolveDelegate>( $"{name}.{nameof(ResolveEid)}",   hooks, vTable[85], ResolveEid);
        _resolveImcPathHook   = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolveImc)}",   hooks, vTable[81], ResolveImc);
        _resolveMPapPathHook  = Create<MPapResolveDelegate>(   $"{name}.{nameof(ResolveMPap)}",  hooks, vTable[79], ResolveMPap);
        _resolveMdlPathHook   = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolveMdl)}",   hooks, vTable[73], type, ResolveMdl, ResolveMdlHuman);
        _resolveMtrlPathHook  = Create<NamedResolveDelegate>(  $"{name}.{nameof(ResolveMtrl)}",  hooks, vTable[82], ResolveMtrl);
        _resolvePapPathHook   = Create<NamedResolveDelegate>(  $"{name}.{nameof(ResolvePap)}",   hooks, vTable[76], type, ResolvePap, ResolvePapHuman);
        _resolvePhybPathHook  = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolvePhyb)}",  hooks, vTable[75], type, ResolvePhyb, ResolvePhybHuman);
        _resolveSklbPathHook  = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolveSklb)}",  hooks, vTable[72], type, ResolveSklb, ResolveSklbHuman);
        _resolveSkpPathHook   = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolveSkp)}",   hooks, vTable[74], type, ResolveSkp, ResolveSkpHuman);
        _resolveTmbPathHook   = Create<TmbResolveDelegate>(    $"{name}.{nameof(ResolveTmb)}",   hooks, vTable[77], ResolveTmb);
        _resolveVfxPathHook   = Create<VfxResolveDelegate>(    $"{name}.{nameof(ResolveVfx)}",   hooks, vTable[84], type, ResolveVfx, ResolveVfxHuman);
        // @formatter:on
        Enable();
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

    private nint ResolveVfxHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex, nint unkOutParam)
    {
        if (slotIndex <= 4)
            return ResolveVfx(drawObject, pathBuffer, pathBufferSize, slotIndex, unkOutParam);

        var changedEquipData = ((Human*)drawObject)->ChangedEquipData;
        // Enable vfxs for accessories
        if (changedEquipData == null)
            return ResolveVfx(drawObject, pathBuffer, pathBufferSize, slotIndex, unkOutParam);

        var slot    = (ushort*)(changedEquipData + 12 * (nint)slotIndex);
        var model   = slot[0];
        var variant = slot[1];
        var vfxId   = slot[4];

        if (model == 0 || variant == 0 || vfxId == 0)
            return ResolveVfx(drawObject, pathBuffer, pathBufferSize, slotIndex, unkOutParam);

        if (!Utf8.TryWrite(new Span<byte>((void*)pathBuffer, (int)pathBufferSize), $"chara/accessory/a{model:D4}/vfx/eff/va{vfxId:D4}.avfx\0",
                out _))
            return ResolveVfx(drawObject, pathBuffer, pathBufferSize, slotIndex, unkOutParam);

        *(ulong*)unkOutParam = 4;
        return ResolvePath(drawObject, pathBuffer);
    }

    private DisposableContainer GetEstChanges(nint drawObject, out ResolveData data)
    {
        data = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        if (_parent.InInternalResolve)
            return DisposableContainer.Empty;

        return new DisposableContainer(data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility, EstManipulation.EstType.Face),
            data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility,                            EstManipulation.EstType.Body),
            data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility,                            EstManipulation.EstType.Hair),
            data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility,                            EstManipulation.EstType.Head));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Hook<T> Create<T>(string name, HookManager hooks, nint address, Type type, T other, T human) where T : Delegate
    {
        var del = type switch
        {
            Type.Human => human,
            _          => other,
        };
        return hooks.CreateHook(name, address, del).Result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Hook<T> Create<T>(string name, HookManager hooks, nint address, T del) where T : Delegate
        => hooks.CreateHook(name, address, del).Result;


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
