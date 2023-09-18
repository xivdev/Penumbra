namespace Penumbra.Interop.Structs;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct ResidentResourceManager
{
    [FieldOffset(0x00)]
    public void** VTable;

    [FieldOffset(0x08)]
    public void** ResourceListVTable;

    [FieldOffset(0x14)]
    public uint NumResources;

    [FieldOffset(0x18)]
    public ResourceHandle** ResourceList;
}
