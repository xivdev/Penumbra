namespace Penumbra.Interop.Structs;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct MtrlResource
{
    [FieldOffset(0x00)]
    public ResourceHandle Handle;

    [FieldOffset(0xC8)]
    public ShaderPackageResourceHandle* ShpkResourceHandle;

    [FieldOffset(0xD0)]
    public TextureEntry* TexSpace; // Contains the offsets for the tex files inside the string list.

    [FieldOffset(0xE0)]
    public byte* StringList;

    [FieldOffset(0xF8)]
    public ushort ShpkOffset;

    [FieldOffset(0xFA)]
    public byte NumTex;

    public byte* ShpkString
        => StringList + ShpkOffset;

    public byte* TexString(int idx)
        => StringList + TexSpace[idx].PathOffset;

    public bool TexIsDX11(int idx)
        => TexSpace[idx].Flags >= 0x8000;

    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct TextureEntry
    {
        [FieldOffset(0x00)]
        public TextureResourceHandle* ResourceHandle;

        [FieldOffset(0x08)]
        public ushort PathOffset;

        [FieldOffset(0x0A)]
        public ushort Flags;
    }
}
