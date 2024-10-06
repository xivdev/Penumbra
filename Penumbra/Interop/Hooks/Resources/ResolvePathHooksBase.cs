using System.Text.Unicode;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;

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
    private delegate nint SkeletonVFuncDelegate(nint drawObject, int estType, nint unk);

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
    private readonly Hook<SkeletonVFuncDelegate>? _vFunc81Hook;
    private readonly Hook<SkeletonVFuncDelegate>? _vFunc83Hook;

    private readonly PathState _parent;

    public ResolvePathHooksBase(string name, HookManager hooks, PathState parent, nint* vTable, Type type)
    {
        _parent = parent;
        // @formatter:off
        _resolveSklbPathHook  = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolveSklb)}",  hooks, vTable[76], type, ResolveSklb, ResolveSklbHuman);
        _resolveMdlPathHook   = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolveMdl)}",   hooks, vTable[77], type, ResolveMdl, ResolveMdlHuman);
        _resolveSkpPathHook   = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolveSkp)}",   hooks, vTable[78], type, ResolveSkp, ResolveSkpHuman);
        _resolvePhybPathHook  = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolvePhyb)}",  hooks, vTable[79], type, ResolvePhyb, ResolvePhybHuman);

        _vFunc81Hook          = Create<SkeletonVFuncDelegate>( $"{name}.{nameof(VFunc81)}",      hooks, vTable[81], type, null, VFunc81);

        _vFunc83Hook          = Create<SkeletonVFuncDelegate>( $"{name}.{nameof(VFunc83)}",      hooks, vTable[83], type, null, VFunc83);

        _resolvePapPathHook   = Create<NamedResolveDelegate>(  $"{name}.{nameof(ResolvePap)}",   hooks, vTable[84], type, ResolvePap, ResolvePapHuman);
        _resolveTmbPathHook   = Create<TmbResolveDelegate>(    $"{name}.{nameof(ResolveTmb)}",   hooks, vTable[85], ResolveTmb);
        _resolveMPapPathHook  = Create<MPapResolveDelegate>(   $"{name}.{nameof(ResolveMPap)}",  hooks, vTable[87], ResolveMPap);
        _resolveImcPathHook   = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolveImc)}",   hooks, vTable[89], ResolveImc);
        _resolveMtrlPathHook  = Create<NamedResolveDelegate>(  $"{name}.{nameof(ResolveMtrl)}",  hooks, vTable[90], ResolveMtrl);
        _resolveDecalPathHook = Create<PerSlotResolveDelegate>($"{name}.{nameof(ResolveDecal)}", hooks, vTable[92], ResolveDecal);
        _resolveVfxPathHook   = Create<VfxResolveDelegate>(    $"{name}.{nameof(ResolveVfx)}",   hooks, vTable[93], type, ResolveVfx, ResolveVfxHuman);
        _resolveEidPathHook   = Create<SingleResolveDelegate>( $"{name}.{nameof(ResolveEid)}",   hooks, vTable[94], ResolveEid);
        
        
        // @formatter:on
        if (!HookOverrides.Instance.Resources.ResolvePathHooks)
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
        _vFunc81Hook?.Enable();
        _vFunc83Hook?.Enable();
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
        _vFunc81Hook?.Disable();
        _vFunc83Hook?.Disable();
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
        _vFunc81Hook?.Dispose();
        _vFunc83Hook?.Dispose();
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
        var collection = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        if (slotIndex < 10)
            _parent.MetaState.EqdpCollection.Push(collection);

        var ret = ResolvePath(collection, _resolveMdlPathHook.Original(drawObject, pathBuffer, pathBufferSize, slotIndex));
        if (slotIndex < 10)
            _parent.MetaState.EqdpCollection.Pop();

        return ret;
    }

    private nint ResolvePapHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint unkAnimationIndex, nint animationName)
    {
        var collection = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        _parent.MetaState.EstCollection.Push(collection);
        var ret = ResolvePath(collection,
            _resolvePapPathHook.Original(drawObject, pathBuffer, pathBufferSize, unkAnimationIndex, animationName));
        _parent.MetaState.EstCollection.Pop();
        return ret;
    }

    private nint ResolvePhybHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint partialSkeletonIndex)
    {
        var collection = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        _parent.MetaState.EstCollection.Push(collection);
        var ret = ResolvePath(collection, _resolvePhybPathHook.Original(drawObject, pathBuffer, pathBufferSize, partialSkeletonIndex));
        _parent.MetaState.EstCollection.Pop();
        return ret;
    }

    private nint ResolveSklbHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint partialSkeletonIndex)
    {
        var collection = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        _parent.MetaState.EstCollection.Push(collection);
        var ret = ResolvePath(collection, _resolveSklbPathHook.Original(drawObject, pathBuffer, pathBufferSize, partialSkeletonIndex));
        _parent.MetaState.EstCollection.Pop();
        return ret;
    }

    private nint ResolveSkpHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint partialSkeletonIndex)
    {
        var collection = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        _parent.MetaState.EstCollection.Push(collection);
        var ret = ResolvePath(collection, _resolveSkpPathHook.Original(drawObject, pathBuffer, pathBufferSize, partialSkeletonIndex));
        _parent.MetaState.EstCollection.Pop();
        return ret;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct ChangedEquipData
    {
        [FieldOffset(0)]
        public PrimaryId Model;

        [FieldOffset(2)]
        public Variant Variant;

        [FieldOffset(20)]
        public ushort VfxId;

        [FieldOffset(22)]
        public GenderRace GenderRace;
    }

    private nint ResolveVfxHuman(nint drawObject, nint pathBuffer, nint pathBufferSize, uint slotIndex, nint unkOutParam)
    {
        if (slotIndex is <= 4 or >= 10)
            return ResolveVfx(drawObject, pathBuffer, pathBufferSize, slotIndex, unkOutParam);

        var changedEquipData = (ChangedEquipData*)((Human*)drawObject)->ChangedEquipData;
        // Enable vfxs for accessories
        if (changedEquipData == null)
            return ResolveVfx(drawObject, pathBuffer, pathBufferSize, slotIndex, unkOutParam);

        ref var slot = ref changedEquipData[slotIndex];

        if (slot.Model == 0 || slot.Variant == 0 || slot.VfxId == 0)
            return ResolveVfx(drawObject, pathBuffer, pathBufferSize, slotIndex, unkOutParam);

        if (!Utf8.TryWrite(new Span<byte>((void*)pathBuffer, (int)pathBufferSize),
                $"chara/accessory/a{slot.Model.Id:D4}/vfx/eff/va{slot.VfxId:D4}.avfx\0",
                out _))
            return ResolveVfx(drawObject, pathBuffer, pathBufferSize, slotIndex, unkOutParam);

        *(ulong*)unkOutParam = 4;
        return ResolvePath(drawObject, pathBuffer);
    }

    private nint VFunc81(nint drawObject, int estType, nint unk)
    {
        var collection = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        _parent.MetaState.EstCollection.Push(collection);
        var ret = _vFunc81Hook!.Original(drawObject, estType, unk);
        _parent.MetaState.EstCollection.Pop();
        return ret;
    }

    private nint VFunc83(nint drawObject, int estType, nint unk)
    {
        var collection = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        _parent.MetaState.EstCollection.Push(collection);
        var ret = _vFunc83Hook!.Original(drawObject, estType, unk);
        _parent.MetaState.EstCollection.Pop();
        return ret;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    [return: NotNullIfNotNull(nameof(other))]
    private static Hook<T>? Create<T>(string name, HookManager hooks, nint address, Type type, T? other, T human) where T : Delegate
    {
        var del = type switch
        {
            Type.Human => human,
            _          => other,
        };
        if (del == null)
            return null;

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
