using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using OtterGui.Services;
using Penumbra.GameData;

namespace Penumbra.Interop;

public sealed unsafe class CharacterBaseVTables : IService
{
    [Signature(Sigs.HumanVTable, ScanType = ScanType.StaticAddress)]
    public readonly nint* HumanVTable = null!;

    [Signature(Sigs.WeaponVTable, ScanType = ScanType.StaticAddress)]
    public readonly nint* WeaponVTable = null!;

    [Signature(Sigs.DemiHumanVTable, ScanType = ScanType.StaticAddress)]
    public readonly nint* DemiHumanVTable = null!;

    [Signature(Sigs.MonsterVTable, ScanType = ScanType.StaticAddress)]
    public readonly nint* MonsterVTable = null!;

    public CharacterBaseVTables(IGameInteropProvider interop)
        => interop.InitializeFromAttributes(this);
}
