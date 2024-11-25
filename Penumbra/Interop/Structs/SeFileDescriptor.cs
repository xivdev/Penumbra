using Dalamud.Memory;
using Penumbra.String.Functions;

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

    public FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle* CsResourceHandele
        => (FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle*)ResourceHandle;

    public string FileName
    {
        get
        {
            fixed (char* ptr = &Utf16FileName)
            {
                return MemoryMarshal.CreateReadOnlySpanFromNullTerminated(ptr).ToString();
            }
        }
    }
}
