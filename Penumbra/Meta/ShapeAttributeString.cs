using Lumina.Misc;
using Newtonsoft.Json;
using Penumbra.GameData.Files.PhybStructs;
using Penumbra.String.Functions;

namespace Penumbra.Meta;

[JsonConverter(typeof(Converter))]
public struct ShapeAttributeString : IEquatable<ShapeAttributeString>, IComparable<ShapeAttributeString>
{
    public const int MaxLength = 30;

    public static readonly ShapeAttributeString Empty = new();

    private FixedString32 _buffer;

    public int Count
        => _buffer[31];

    public int Length
        => _buffer[31];

    public override string ToString()
        => Encoding.UTF8.GetString(_buffer[..Length]);

    public byte this[int index]
        => _buffer[index];

    public unsafe ReadOnlySpan<byte> AsSpan
    {
        get
        {
            fixed (void* ptr = &this)
            {
                return new ReadOnlySpan<byte>(ptr, Length);
            }
        }
    }

    public static unsafe bool ValidateCustomShapeString(byte* shape)
    {
        // "shpx_*"
        if (shape is null)
            return false;

        if (*shape++ is not (byte)'s'
         || *shape++ is not (byte)'h'
         || *shape++ is not (byte)'p'
         || *shape++ is not (byte)'x'
         || *shape++ is not (byte)'_'
         || *shape is 0)
            return false;

        return true;
    }

    public bool ValidateCustomShapeString()
    {
        // "shpx_*"
        if (Length < 6)
            return false;

        if (_buffer[0] is not (byte)'s'
         || _buffer[1] is not (byte)'h'
         || _buffer[2] is not (byte)'p'
         || _buffer[3] is not (byte)'x'
         || _buffer[4] is not (byte)'_')
            return false;

        return true;
    }

    public static unsafe bool ValidateCustomAttributeString(byte* shape)
    {
        // "atrx_*"
        if (shape is null)
            return false;

        if (*shape++ is not (byte)'a'
         || *shape++ is not (byte)'t'
         || *shape++ is not (byte)'r'
         || *shape++ is not (byte)'x'
         || *shape++ is not (byte)'_'
         || *shape is 0)
            return false;

        return true;
    }

    public bool ValidateCustomAttributeString()
    {
        // "atrx_*"
        if (Length < 6)
            return false;

        if (_buffer[0] is not (byte)'a'
         || _buffer[1] is not (byte)'t'
         || _buffer[2] is not (byte)'r'
         || _buffer[3] is not (byte)'x'
         || _buffer[4] is not (byte)'_')
            return false;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsAnkle()
        => CheckCenter('a', 'n');

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsWaist()
        => CheckCenter('w', 'a');

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsWrist()
        => CheckCenter('w', 'r');

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool CheckCenter(char first, char second)
        => Length > 8 && _buffer[5] == first && _buffer[6] == second && _buffer[7] is (byte)'_';

    public bool Equals(ShapeAttributeString other)
        => Length == other.Length && _buffer[..Length].SequenceEqual(other._buffer[..Length]);

    public override bool Equals(object? obj)
        => obj is ShapeAttributeString other && Equals(other);

    public override int GetHashCode()
        => (int)Crc32.Get(_buffer[..Length]);

    public static bool operator ==(ShapeAttributeString left, ShapeAttributeString right)
        => left.Equals(right);

    public static bool operator !=(ShapeAttributeString left, ShapeAttributeString right)
        => !left.Equals(right);

    public static unsafe bool TryRead(byte* pointer, out ShapeAttributeString ret)
    {
        var span = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pointer);
        return TryRead(span, out ret);
    }

    public unsafe int CompareTo(ShapeAttributeString other)
    {
        fixed (void* lhs = &this)
        {
            return ByteStringFunctions.Compare((byte*)lhs, Length, (byte*)&other, other.Length);
        }
    }

    public static bool TryRead(ReadOnlySpan<byte> utf8, out ShapeAttributeString ret)
    {
        if (utf8.Length is 0 or > MaxLength)
        {
            ret = Empty;
            return false;
        }

        ret = Empty;
        utf8.CopyTo(ret._buffer);
        ret._buffer[utf8.Length] = 0;
        ret._buffer[31]          = (byte)utf8.Length;
        return true;
    }

    public static bool TryRead(ReadOnlySpan<char> utf16, out ShapeAttributeString ret)
    {
        ret = Empty;
        if (!Encoding.UTF8.TryGetBytes(utf16, ret._buffer[..MaxLength], out var written))
            return false;

        ret._buffer[written] = 0;
        ret._buffer[31]      = (byte)written;
        return true;
    }

    public void ForceLength(byte length)
    {
        if (length > MaxLength)
            length = MaxLength;
        _buffer[length] = 0;
        _buffer[31]     = length;
    }

    private sealed class Converter : JsonConverter<ShapeAttributeString>
    {
        public override void WriteJson(JsonWriter writer, ShapeAttributeString value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override ShapeAttributeString ReadJson(JsonReader reader, Type objectType, ShapeAttributeString existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var value = serializer.Deserialize<string>(reader);
            if (!TryRead(value, out existingValue))
                throw new JsonReaderException($"Could not parse {value} into ShapeAttributeString.");

            return existingValue;
        }
    }
}
