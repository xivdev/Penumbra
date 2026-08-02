using Dalamud.Plugin.Services;
using Penumbra.GameData;

namespace Penumbra.Interop;

public sealed unsafe class CharacterBaseVTables(ISigScanner sigScanner) : Luna.IService
{
    public readonly nint* HumanVTable     = (nint*)sigScanner.GetStaticAddressFromSig(Sigs.HumanVTable);
    public readonly nint* WeaponVTable    = (nint*)sigScanner.GetStaticAddressFromSig(Sigs.WeaponVTable);
    public readonly nint* DemiHumanVTable = (nint*)sigScanner.GetStaticAddressFromSig(Sigs.DemiHumanVTable);
    public readonly nint* MonsterVTable   = (nint*)sigScanner.GetStaticAddressFromSig(Sigs.MonsterVTable);
}
