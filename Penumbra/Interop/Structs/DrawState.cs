namespace Penumbra.Interop.Structs;

[Flags]
public enum DrawState : uint
{
    Invisibility      = 0x00_00_00_02,
    IsLoading         = 0x00_00_08_00,
    SomeNpcFlag       = 0x00_00_01_00,
    MaybeCulled       = 0x00_00_04_00,
    MaybeHiddenMinion = 0x00_00_80_00,
    MaybeHiddenSummon = 0x00_80_00_00,
}