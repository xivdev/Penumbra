using System.Runtime.InteropServices;

namespace Penumbra.Structs {
    [StructLayout( LayoutKind.Explicit, Size = 0x130)]
    public unsafe struct TextureResourceHandle {
        [FieldOffset( 0x0 )] public ResourceHandle ResourceHandle;
        [FieldOffset( 0x38 )] public void* Unk;
        [FieldOffset( 0x118 )] public void* KernelTexture;
        [FieldOffset( 0x120 )] public void* NewKernelTexture;
    }
}
