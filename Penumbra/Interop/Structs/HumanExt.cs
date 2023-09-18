using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace Penumbra.Interop.Structs;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct HumanExt
{
    [FieldOffset(0x0)]
    public Human Human;

    [FieldOffset(0x0)]
    public CharacterBaseExt CharacterBase;

    [FieldOffset(0x9E8)]
    public ResourceHandle* Decal;

    [FieldOffset(0x9F0)]
    public ResourceHandle* LegacyBodyDecal;
}
