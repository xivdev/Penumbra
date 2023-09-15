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

    private readonly ThreadLocal<ResolveData> _resolveData = new(() => ResolveData.Invalid, true);

    public IList<ResolveData> CurrentData
        => _resolveData.Values;

    public PathState(CollectionResolver collectionResolver, MetaState metaState, CharacterUtility characterUtility)
    {
        SignatureHelper.Initialise(this);
        CollectionResolver = collectionResolver;
        MetaState          = metaState;
        CharacterUtility   = characterUtility;
        _human             = new ResolvePathHooks(this, _humanVTable,     ResolvePathHooks.Type.Human);
        _weapon            = new ResolvePathHooks(this, _weaponVTable,    ResolvePathHooks.Type.Weapon);
        _demiHuman         = new ResolvePathHooks(this, _demiHumanVTable, ResolvePathHooks.Type.Other);
        _monster           = new ResolvePathHooks(this, _monsterVTable,   ResolvePathHooks.Type.Other);
        _human.Enable();
        _weapon.Enable();
        _demiHuman.Enable();
        _monster.Enable();
    }


    public void Dispose()
    {
        _resolveData.Dispose();
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

        _resolveData.Value = collection.ToResolveData(gameObject);
        return path;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nint ResolvePath(ResolveData data, nint path)
    {
        if (path == nint.Zero)
            return path;

        _resolveData.Value = data;
        return path;
    }
}
