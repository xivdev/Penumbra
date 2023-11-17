using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.Interop.Services;
using Penumbra.String;

namespace Penumbra.Interop.PathResolving;

public unsafe class PathState : IDisposable
{
    [Signature(Sigs.HumanVTable, ScanType = ScanType.StaticAddress)]
    private readonly nint* _humanVTable = null!;

    [Signature(Sigs.WeaponVTable, ScanType = ScanType.StaticAddress)]
    private readonly nint* _weaponVTable = null!;

    [Signature(Sigs.DemiHumanVTable, ScanType = ScanType.StaticAddress)]
    private readonly nint* _demiHumanVTable = null!;

    [Signature(Sigs.MonsterVTable, ScanType = ScanType.StaticAddress)]
    private readonly nint* _monsterVTable = null!;

    public readonly CollectionResolver CollectionResolver;
    public readonly MetaState          MetaState;
    public readonly CharacterUtility   CharacterUtility;

    private readonly ResolvePathHooks _human;
    private readonly ResolvePathHooks _weapon;
    private readonly ResolvePathHooks _demiHuman;
    private readonly ResolvePathHooks _monster;

    private readonly ThreadLocal<ResolveData> _resolveData     = new(() => ResolveData.Invalid, true);
    private readonly ThreadLocal<uint>        _internalResolve = new(() => 0, false);

    public IList<ResolveData> CurrentData
        => _resolveData.Values;

    public bool InInternalResolve
        => _internalResolve.Value != 0u;

    public PathState(CollectionResolver collectionResolver, MetaState metaState, CharacterUtility characterUtility, IGameInteropProvider interop)
    {
        interop.InitializeFromAttributes(this);
        CollectionResolver = collectionResolver;
        MetaState          = metaState;
        CharacterUtility   = characterUtility;
        _human             = new ResolvePathHooks(interop, this, _humanVTable,     ResolvePathHooks.Type.Human);
        _weapon            = new ResolvePathHooks(interop, this, _weaponVTable,    ResolvePathHooks.Type.Other);
        _demiHuman         = new ResolvePathHooks(interop, this, _demiHumanVTable, ResolvePathHooks.Type.Other);
        _monster           = new ResolvePathHooks(interop, this, _monsterVTable,   ResolvePathHooks.Type.Other);
        _human.Enable();
        _weapon.Enable();
        _demiHuman.Enable();
        _monster.Enable();
    }


    public void Dispose()
    {
        _resolveData.Dispose();
        _internalResolve.Dispose();
        _human.Dispose();
        _weapon.Dispose();
        _demiHuman.Dispose();
        _monster.Dispose();
    }

    public bool Consume(ByteString _, out ResolveData collection)
    {
        if (_resolveData.IsValueCreated)
        {
            collection         = _resolveData.Value;
            _resolveData.Value = ResolveData.Invalid;
            return collection.Valid;
        }

        collection = ResolveData.Invalid;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nint ResolvePath(nint gameObject, ModCollection collection, nint path)
    {
        if (path == nint.Zero)
            return path;

        if (!InInternalResolve)
        {
            _resolveData.Value = collection.ToResolveData(gameObject);
        }
        return path;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nint ResolvePath(ResolveData data, nint path)
    {
        if (path == nint.Zero)
            return path;

        if (!InInternalResolve)
        {
            _resolveData.Value = data;
        }
        return path;
    }

    /// <summary>
    /// Temporarily disables metadata mod application and resolve data capture on the current thread. <para />
    /// Must be called to prevent race conditions between Penumbra's internal path resolution (for example for Resource Trees) and the game's path resolution. <para />
    /// Please note that this will make path resolution cases that depend on metadata incorrect.
    /// </summary>
    /// <returns> A struct that will undo this operation when disposed. Best used with: <code>using (var _ = pathState.EnterInternalResolve()) { ... }</code> </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public InternalResolveRaii EnterInternalResolve()
        => new(this);

    public readonly ref struct InternalResolveRaii
    {
        private readonly ThreadLocal<uint> _internalResolve;

        public InternalResolveRaii(PathState parent)
        {
            _internalResolve = parent._internalResolve;
            ++_internalResolve.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public readonly void Dispose()
        {
            --_internalResolve.Value;
        }
    }
}
