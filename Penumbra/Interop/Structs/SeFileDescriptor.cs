namespace Penumbra.Interop.Structs;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct SeFileDescriptor
{
    [FieldOffset(0x00)]
    public FileMode FileMode;

    [FieldOffset(0x30)]
    public void* FileDescriptor;

    [FieldOffset(0x50)]
    public ResourceHandle* ResourceHandle;

    [FieldOffset(0x70)]
    public char Utf16FileName;
}
