using System.Collections.Frozen;
using OtterGui;

namespace Penumbra;

public static class GuidExtensions
{
    private const string Chars =
        "0123456789"
      + "abcdefghij"
      + "klmnopqrst"
      + "uv";

    private static ReadOnlySpan<byte> Bytes
        => "0123456789abcdefghijklmnopqrstuv"u8;

    private static readonly FrozenDictionary<char, byte>
        ReverseChars = Chars.WithIndex().ToFrozenDictionary(t => t.Value, t => (byte)t.Index);

    private static readonly FrozenDictionary<byte, byte> ReverseBytes =
        ReverseChars.ToFrozenDictionary(kvp => (byte)kvp.Key, kvp => kvp.Value);

    public static unsafe string OptimizedString(this Guid guid)
    {
        var bytes = stackalloc ulong[2];
        if (!guid.TryWriteBytes(new Span<byte>(bytes, 16)))
            return guid.ToString("N");

        var u1 = bytes[0];
        var u2 = bytes[1];
        Span<char> text =
        [
            Chars[(int)(u1 & 0x1F)],
            Chars[(int)((u1 >> 5) & 0x1F)],
            Chars[(int)((u1 >> 10) & 0x1F)],
            Chars[(int)((u1 >> 15) & 0x1F)],
            Chars[(int)((u1 >> 20) & 0x1F)],
            Chars[(int)((u1 >> 25) & 0x1F)],
            Chars[(int)((u1 >> 30) & 0x1F)],
            Chars[(int)((u1 >> 35) & 0x1F)],
            Chars[(int)((u1 >> 40) & 0x1F)],
            Chars[(int)((u1 >> 45) & 0x1F)],
            Chars[(int)((u1 >> 50) & 0x1F)],
            Chars[(int)((u1 >> 55) & 0x1F)],
            Chars[(int)((u1 >> 60) | ((u2 & 0x01) << 4))],
            Chars[(int)((u2 >> 1) & 0x1F)],
            Chars[(int)((u2 >> 6) & 0x1F)],
            Chars[(int)((u2 >> 11) & 0x1F)],
            Chars[(int)((u2 >> 16) & 0x1F)],
            Chars[(int)((u2 >> 21) & 0x1F)],
            Chars[(int)((u2 >> 26) & 0x1F)],
            Chars[(int)((u2 >> 31) & 0x1F)],
            Chars[(int)((u2 >> 36) & 0x1F)],
            Chars[(int)((u2 >> 41) & 0x1F)],
            Chars[(int)((u2 >> 46) & 0x1F)],
            Chars[(int)((u2 >> 51) & 0x1F)],
            Chars[(int)((u2 >> 56) & 0x1F)],
            Chars[(int)((u2 >> 61) & 0x1F)],
        ];
        return new string(text);
    }

    public static unsafe bool FromOptimizedString(ReadOnlySpan<char> text, out Guid guid)
    {
        if (text.Length != 26)
            return Return(out guid);

        var bytes = stackalloc ulong[2];
        if (!ReverseChars.TryGetValue(text[0], out var b0))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[1], out var b1))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[2], out var b2))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[3], out var b3))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[4], out var b4))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[5], out var b5))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[6], out var b6))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[7], out var b7))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[8], out var b8))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[9], out var b9))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[10], out var b10))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[11], out var b11))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[12], out var b12))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[13], out var b13))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[14], out var b14))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[15], out var b15))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[16], out var b16))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[17], out var b17))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[18], out var b18))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[19], out var b19))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[20], out var b20))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[21], out var b21))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[22], out var b22))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[23], out var b23))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[24], out var b24))
            return Return(out guid);
        if (!ReverseChars.TryGetValue(text[25], out var b25))
            return Return(out guid);

        bytes[0] = b0
          | ((ulong)b1 << 5)
          | ((ulong)b2 << 10)
          | ((ulong)b3 << 15)
          | ((ulong)b4 << 20)
          | ((ulong)b5 << 25)
          | ((ulong)b6 << 30)
          | ((ulong)b7 << 35)
          | ((ulong)b8 << 40)
          | ((ulong)b9 << 45)
          | ((ulong)b10 << 50)
          | ((ulong)b11 << 55)
          | ((ulong)b12 << 60);
        bytes[1] = ((ulong)b12 >> 4)
          | ((ulong)b13 << 1)
          | ((ulong)b14 << 6)
          | ((ulong)b15 << 11)
          | ((ulong)b16 << 16)
          | ((ulong)b17 << 21)
          | ((ulong)b18 << 26)
          | ((ulong)b19 << 31)
          | ((ulong)b20 << 36)
          | ((ulong)b21 << 41)
          | ((ulong)b22 << 46)
          | ((ulong)b23 << 51)
          | ((ulong)b24 << 56)
          | ((ulong)b25 << 61);
        guid = new Guid(new Span<byte>(bytes, 16));
        return true;

        static bool Return(out Guid guid)
        {
            guid = Guid.Empty;
            return false;
        }
    }

    public static unsafe bool FromOptimizedString(ReadOnlySpan<byte> text, out Guid guid)
    {
        if (text.Length != 26)
            return Return(out guid);

        var bytes = stackalloc ulong[2];
        if (!ReverseBytes.TryGetValue(text[0], out var b0))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[1], out var b1))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[2], out var b2))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[3], out var b3))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[4], out var b4))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[5], out var b5))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[6], out var b6))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[7], out var b7))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[8], out var b8))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[9], out var b9))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[10], out var b10))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[11], out var b11))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[12], out var b12))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[13], out var b13))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[14], out var b14))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[15], out var b15))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[16], out var b16))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[17], out var b17))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[18], out var b18))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[19], out var b19))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[20], out var b20))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[21], out var b21))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[22], out var b22))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[23], out var b23))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[24], out var b24))
            return Return(out guid);
        if (!ReverseBytes.TryGetValue(text[25], out var b25))
            return Return(out guid);

        bytes[0] = b0
          | ((ulong)b1 << 5)
          | ((ulong)b2 << 10)
          | ((ulong)b3 << 15)
          | ((ulong)b4 << 20)
          | ((ulong)b5 << 25)
          | ((ulong)b6 << 30)
          | ((ulong)b7 << 35)
          | ((ulong)b8 << 40)
          | ((ulong)b9 << 45)
          | ((ulong)b10 << 50)
          | ((ulong)b11 << 55)
          | ((ulong)b12 << 60);
        bytes[1] = ((ulong)b12 >> 4)
          | ((ulong)b13 << 1)
          | ((ulong)b14 << 6)
          | ((ulong)b15 << 11)
          | ((ulong)b16 << 16)
          | ((ulong)b17 << 21)
          | ((ulong)b18 << 26)
          | ((ulong)b19 << 31)
          | ((ulong)b20 << 36)
          | ((ulong)b21 << 41)
          | ((ulong)b22 << 46)
          | ((ulong)b23 << 51)
          | ((ulong)b24 << 56)
          | ((ulong)b25 << 61);
        guid = new Guid(new Span<byte>(bytes, 16));
        return true;

        static bool Return(out Guid guid)
        {
            guid = Guid.Empty;
            return false;
        }
    }
}
