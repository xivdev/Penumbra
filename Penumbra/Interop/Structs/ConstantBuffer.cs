namespace Penumbra.Interop.Structs;

[StructLayout(LayoutKind.Explicit, Size = 0x70)]
public unsafe struct ConstantBuffer
{
    [FieldOffset(0x20)]
    public int Size;

    [FieldOffset(0x24)]
    public int Flags;

    [FieldOffset(0x28)]
    private void* _maybeSourcePointer;

    public bool TryGetBuffer(out Span<float> buffer)
    {
        if ((Flags & 0x4003) == 0 && _maybeSourcePointer != null)
        {
            buffer = new Span<float>(_maybeSourcePointer, Size >> 2);
            return true;
        }
        else
        {
            buffer = null;
            return false;
        }
    }
}
