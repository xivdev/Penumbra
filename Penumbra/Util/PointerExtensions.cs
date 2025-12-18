namespace Penumbra.Util;

public static class PointerExtensions
{
    public static unsafe ref TField GetField<TField, TPointer>(this ref TPointer reference, int offset)
        where TPointer : unmanaged
        where TField : unmanaged
    {
        var pointer = (byte*)Unsafe.AsPointer(ref reference) + offset;
        return ref *(TField*)pointer;
    }

    public static unsafe ref TField GetField<TField, TPointer>(TPointer* itemPointer, int offset)
        where TPointer : unmanaged
        where TField : unmanaged
    {
        var pointer = (byte*)itemPointer + offset;
        return ref *(TField*)pointer;
    }
}
