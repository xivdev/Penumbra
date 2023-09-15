namespace Penumbra.Interop.Structs;

public static class ShaderPackageUtility
{
    [StructLayout(LayoutKind.Explicit, Size = 0xC)]
    public unsafe struct Sampler
    {
        [FieldOffset(0x0)]
        public uint Crc;

        [FieldOffset(0x4)]
        public uint Id;

        [FieldOffset(0xA)]
        public ushort Slot;
    }
}
