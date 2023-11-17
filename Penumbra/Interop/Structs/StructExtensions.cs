using FFXIVClientStructs.STD;
using Penumbra.String;

namespace Penumbra.Interop.Structs;

internal static class StructExtensions
{
    // TODO submit this to ClientStructs
    public static unsafe ReadOnlySpan<byte> AsSpan(in this StdString str)
    {
        if (str.Length < 16)
        {
            fixed (StdString* pStr = &str)
            {
                return new(pStr->Buffer, (int)str.Length);
            }
        }
        else
            return new(str.BufferPtr, (int)str.Length);
    }

    public static unsafe ByteString AsByteString(in this StdString str)
        => ByteString.FromSpanUnsafe(str.AsSpan(), true);
}
