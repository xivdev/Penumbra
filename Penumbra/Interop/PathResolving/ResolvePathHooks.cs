using Dalamud.Hooking;
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
        Weapon,
        Other,
    }

    private delegate nint GeneralResolveDelegate(nint drawObject, nint path, nint unk3, uint unk4);
    private delegate nint MPapResolveDelegate(nint drawObject, nint path, nint unk3, uint unk4, uint unk5);
    private delegate nint MaterialResolveDelegate(nint drawObject, nint path, nint unk3, uint unk4, ulong unk5);
    private delegate nint EidResolveDelegate(nint drawObject, nint path, nint unk3);

    private readonly Hook<GeneralResolveDelegate>  _resolveDecalPathHook;
    private readonly Hook<EidResolveDelegate>      _resolveEidPathHook;
    private readonly Hook<GeneralResolveDelegate>  _resolveImcPathHook;
    private readonly Hook<MPapResolveDelegate>     _resolveMPapPathHook;
    private readonly Hook<GeneralResolveDelegate>  _resolveMdlPathHook;
    private readonly Hook<MaterialResolveDelegate> _resolveMtrlPathHook;
    private readonly Hook<MaterialResolveDelegate> _resolvePapPathHook;
    private readonly Hook<GeneralResolveDelegate>  _resolvePhybPathHook;
    private readonly Hook<GeneralResolveDelegate>  _resolveSklbPathHook;
    private readonly Hook<GeneralResolveDelegate>  _resolveSkpPathHook;
    private readonly Hook<EidResolveDelegate>      _resolveTmbPathHook;
    private readonly Hook<MaterialResolveDelegate> _resolveVfxPathHook;

    private readonly PathState _parent;

    public ResolvePathHooks(PathState parent, nint* vTable, Type type)
    {
        _parent               = parent;
        _resolveDecalPathHook = Create<GeneralResolveDelegate>(vTable[83], type, ResolveDecalWeapon, ResolveDecal);
        _resolveEidPathHook   = Create<EidResolveDelegate>(vTable[85], type, ResolveEidWeapon, ResolveEid);
        _resolveImcPathHook   = Create<GeneralResolveDelegate>(vTable[81], type, ResolveImcWeapon, ResolveImc);
        _resolveMPapPathHook  = Create<MPapResolveDelegate>(vTable[79], type, ResolveMPapWeapon, ResolveMPap);
        _resolveMdlPathHook   = Create<GeneralResolveDelegate>(vTable[73], type, ResolveMdlWeapon, ResolveMdl, ResolveMdlHuman);
        _resolveMtrlPathHook  = Create<MaterialResolveDelegate>(vTable[82], type, ResolveMtrlWeapon, ResolveMtrl);
        _resolvePapPathHook   = Create<MaterialResolveDelegate>(vTable[76], type, ResolvePapWeapon,  ResolvePap, ResolvePapHuman);
        _resolvePhybPathHook  = Create<GeneralResolveDelegate>(vTable[75], type, ResolvePhybWeapon, ResolvePhyb, ResolvePhybHuman);
        _resolveSklbPathHook  = Create<GeneralResolveDelegate>(vTable[72], type, ResolveSklbWeapon, ResolveSklb, ResolveSklbHuman);
        _resolveSkpPathHook   = Create<GeneralResolveDelegate>(vTable[74], type, ResolveSkpWeapon,  ResolveSkp,  ResolveSkpHuman);
        _resolveTmbPathHook   = Create<EidResolveDelegate>(vTable[77], type, ResolveTmbWeapon, ResolveTmb);
        _resolveVfxPathHook   = Create<MaterialResolveDelegate>(vTable[84], type, ResolveVfxWeapon, ResolveVfx);
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

    private nint ResolveDecal(nint drawObject, nint path, nint unk3, uint unk4)
        => ResolvePath(drawObject, _resolveDecalPathHook.Original(drawObject, path, unk3, unk4));

    private nint ResolveEid(nint drawObject, nint path, nint unk3)
        => ResolvePath(drawObject, _resolveEidPathHook.Original(drawObject, path, unk3));

    private nint ResolveImc(nint drawObject, nint path, nint unk3, uint unk4)
        => ResolvePath(drawObject, _resolveImcPathHook.Original(drawObject, path, unk3, unk4));

    private nint ResolveMPap(nint drawObject, nint path, nint unk3, uint unk4, uint unk5)
        => ResolvePath(drawObject, _resolveMPapPathHook.Original(drawObject, path, unk3, unk4, unk5));

    private nint ResolveMdl(nint drawObject, nint path, nint unk3, uint modelType)
        => ResolvePath(drawObject, _resolveMdlPathHook.Original(drawObject, path, unk3, modelType));

    private nint ResolveMtrl(nint drawObject, nint path, nint unk3, uint unk4, ulong unk5)
        => ResolvePath(drawObject, _resolveMtrlPathHook.Original(drawObject, path, unk3, unk4, unk5));

    private nint ResolvePap(nint drawObject, nint path, nint unk3, uint unk4, ulong unk5)
        => ResolvePath(drawObject, _resolvePapPathHook.Original(drawObject, path, unk3, unk4, unk5));

    private nint ResolvePhyb(nint drawObject, nint path, nint unk3, uint unk4)
        => ResolvePath(drawObject, _resolvePhybPathHook.Original(drawObject, path, unk3, unk4));

    private nint ResolveSklb(nint drawObject, nint path, nint unk3, uint unk4)
        => ResolvePath(drawObject, _resolveSklbPathHook.Original(drawObject, path, unk3, unk4));

    private nint ResolveSkp(nint drawObject, nint path, nint unk3, uint unk4)
        => ResolvePath(drawObject, _resolveSkpPathHook.Original(drawObject, path, unk3, unk4));

    private nint ResolveTmb(nint drawObject, nint path, nint unk3)
        => ResolvePath(drawObject, _resolveTmbPathHook.Original(drawObject, path, unk3));

    private nint ResolveVfx(nint drawObject, nint path, nint unk3, uint unk4, ulong unk5)
        => ResolvePath(drawObject, _resolveVfxPathHook.Original(drawObject, path, unk3, unk4, unk5));


    private nint ResolveMdlHuman(nint drawObject, nint path, nint unk3, uint modelType)
    {
        var data = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        using var eqdp = modelType > 9
            ? DisposableContainer.Empty
            : _parent.MetaState.ResolveEqdpData(data.ModCollection, MetaState.GetHumanGenderRace(drawObject), modelType < 5, modelType > 4);
        return ResolvePath(data, _resolveMdlPathHook.Original(drawObject, path, unk3, modelType));
    }

    private nint ResolvePapHuman(nint drawObject, nint path, nint unk3, uint unk4, ulong unk5)
    {
        using var est = GetEstChanges(drawObject, out var data);
        return ResolvePath(data, _resolvePapPathHook.Original(drawObject, path, unk3, unk4, unk5));
    }

    private nint ResolvePhybHuman(nint drawObject, nint path, nint unk3, uint unk4)
    {
        using var est = GetEstChanges(drawObject, out var data);
        return ResolvePath(data, _resolvePhybPathHook.Original(drawObject, path, unk3, unk4));
    }

    private nint ResolveSklbHuman(nint drawObject, nint path, nint unk3, uint unk4)
    {
        using var est = GetEstChanges(drawObject, out var data);
        return ResolvePath(data, _resolveSklbPathHook.Original(drawObject, path, unk3, unk4));
    }

    private nint ResolveSkpHuman(nint drawObject, nint path, nint unk3, uint unk4)
    {
        using var est = GetEstChanges(drawObject, out var data);
        return ResolvePath(data, _resolveSkpPathHook.Original(drawObject, path, unk3, unk4));
    }

    private DisposableContainer GetEstChanges(nint drawObject, out ResolveData data)
    {
        data = _parent.CollectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        return new DisposableContainer(data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility, EstManipulation.EstType.Face),
            data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility,                            EstManipulation.EstType.Body),
            data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility,                            EstManipulation.EstType.Hair),
            data.ModCollection.TemporarilySetEstFile(_parent.CharacterUtility,                            EstManipulation.EstType.Head));
    }

    private nint ResolveDecalWeapon(nint drawObject, nint path, nint unk3, uint unk4)
        => ResolvePath(drawObject, _resolveDecalPathHook.Original(drawObject, path, unk3, unk4));

    private nint ResolveEidWeapon(nint drawObject, nint path, nint unk3)
        => ResolvePath(drawObject, _resolveEidPathHook.Original(drawObject, path, unk3));

    private nint ResolveImcWeapon(nint drawObject, nint path, nint unk3, uint unk4)
        => ResolvePath(drawObject, _resolveImcPathHook.Original(drawObject, path, unk3, unk4));

    private nint ResolveMPapWeapon(nint drawObject, nint path, nint unk3, uint unk4, uint unk5)
        => ResolvePath(drawObject, _resolveMPapPathHook.Original(drawObject, path, unk3, unk4, unk5));

    private nint ResolveMdlWeapon(nint drawObject, nint path, nint unk3, uint modelType)
        => ResolvePath(drawObject, _resolveMdlPathHook.Original(drawObject, path, unk3, modelType));

    private nint ResolveMtrlWeapon(nint drawObject, nint path, nint unk3, uint unk4, ulong unk5)
        => ResolvePath(drawObject, _resolveMtrlPathHook.Original(drawObject, path, unk3, unk4, unk5));

    private nint ResolvePapWeapon(nint drawObject, nint path, nint unk3, uint unk4, ulong unk5)
        => ResolvePath(drawObject, _resolvePapPathHook.Original(drawObject, path, unk3, unk4, unk5));

    private nint ResolvePhybWeapon(nint drawObject, nint path, nint unk3, uint unk4)
        => ResolvePath(drawObject, _resolvePhybPathHook.Original(drawObject, path, unk3, unk4));

    private nint ResolveSklbWeapon(nint drawObject, nint path, nint unk3, uint unk4)
        => ResolvePath(drawObject, _resolveSklbPathHook.Original(drawObject, path, unk3, unk4));

    private nint ResolveSkpWeapon(nint drawObject, nint path, nint unk3, uint unk4)
        => ResolvePath(drawObject, _resolveSkpPathHook.Original(drawObject, path, unk3, unk4));

    private nint ResolveTmbWeapon(nint drawObject, nint path, nint unk3)
        => ResolvePath(drawObject, _resolveTmbPathHook.Original(drawObject, path, unk3));

    private nint ResolveVfxWeapon(nint drawObject, nint path, nint unk3, uint unk4, ulong unk5)
        => ResolvePath(drawObject, _resolveVfxPathHook.Original(drawObject, path, unk3, unk4, unk5));


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Hook<T> Create<T>(nint address, Type type, T weapon, T other, T human) where T : Delegate
    {
        var del = type switch
        {
            Type.Human  => human,
            Type.Weapon => weapon,
            _           => other,
        };
        return Hook<T>.FromAddress(address, del);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Hook<T> Create<T>(nint address, Type type, T weapon, T other) where T : Delegate
        => Create(address, type, weapon, other, other);


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
