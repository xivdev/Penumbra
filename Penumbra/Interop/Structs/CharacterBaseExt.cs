using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace Penumbra.Interop.Structs;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct CharacterBaseExt
{
    [FieldOffset(0x0)]
    public CharacterBase CharacterBase;

    [FieldOffset(0x258)]
    public Texture** ColorTableTextures;
}
