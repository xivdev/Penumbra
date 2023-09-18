namespace Penumbra.Interop.Structs;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct VfxParams
{
    [FieldOffset(0x118)]
    public uint GameObjectId;

    [FieldOffset(0x11C)]
    public byte GameObjectType;

    [FieldOffset(0xD0)]
    public ushort TargetCount;

    [FieldOffset(0x120)]
    public fixed ulong Target[16];
}
