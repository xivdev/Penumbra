using OtterGui.Services;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Resources;

public sealed unsafe class ResolvePathHooks(HookManager hooks, CharacterBaseVTables vTables, PathState pathState) : IDisposable, IRequiredService
{
    // @formatter:off
    private readonly ResolvePathHooksBase _human     = new("Human",     hooks, pathState, vTables.HumanVTable,     ResolvePathHooksBase.Type.Human);
    private readonly ResolvePathHooksBase _weapon    = new("Weapon",    hooks, pathState, vTables.WeaponVTable,    ResolvePathHooksBase.Type.Other);
    private readonly ResolvePathHooksBase _demiHuman = new("DemiHuman", hooks, pathState, vTables.DemiHumanVTable, ResolvePathHooksBase.Type.Other);
    private readonly ResolvePathHooksBase _monster   = new("Monster",   hooks, pathState, vTables.MonsterVTable,   ResolvePathHooksBase.Type.Other);
    // @formatter:on

    public void Enable()
    {
        _human.Enable();
        _weapon.Enable();
        _demiHuman.Enable();
        _monster.Enable();
    }

    public void Disable()
    {
        _human.Disable();
        _weapon.Disable();
        _demiHuman.Disable();
        _monster.Disable();
    }

    public void Dispose()
    {
        _human.Dispose();
        _weapon.Dispose();
        _demiHuman.Dispose();
        _monster.Dispose();
    }
}
