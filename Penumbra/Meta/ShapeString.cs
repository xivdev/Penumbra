using Lumina.Misc;
using Newtonsoft.Json;
using Penumbra.GameData.Files.PhybStructs;
using Penumbra.String.Functions;

namespace Penumbra.Meta;

[JsonConverter(typeof(Converter))]
public struct ShapeString : IEquatable<ShapeString>, IComparable<ShapeString>
{
    public const int MaxLength = 30;

    public static readonly ShapeString Empty = new();

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

    public bool Equals(ShapeString other)
        => Length == other.Length && _buffer[..Length].SequenceEqual(other._buffer[..Length]);

    public override bool Equals(object? obj)
        => obj is ShapeString other && Equals(other);

    public override int GetHashCode()
        => (int)Crc32.Get(_buffer[..Length]);

    public static bool operator ==(ShapeString left, ShapeString right)
        => left.Equals(right);

    public static bool operator !=(ShapeString left, ShapeString right)
        => !left.Equals(right);

    public static unsafe bool TryRead(byte* pointer, out ShapeString ret)
    {
        var span = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pointer);
        return TryRead(span, out ret);
    }

    public unsafe int CompareTo(ShapeString other)
    {
        fixed (void* lhs = &this)
        {
            return ByteStringFunctions.Compare((byte*)lhs, Length, (byte*)&other, other.Length);
        }
    }

    public static bool TryRead(ReadOnlySpan<byte> utf8, out ShapeString ret)
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

    public static bool TryRead(ReadOnlySpan<char> utf16, out ShapeString ret)
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

    private sealed class Converter : JsonConverter<ShapeString>
    {
        public override void WriteJson(JsonWriter writer, ShapeString value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override ShapeString ReadJson(JsonReader reader, Type objectType, ShapeString existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            var value = serializer.Deserialize<string>(reader);
            if (!TryRead(value, out existingValue))
                throw new JsonReaderException($"Could not parse {value} into ShapeString.");

            return existingValue;
        }
    }
}
