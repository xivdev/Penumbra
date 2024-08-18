using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Text;

namespace Penumbra.CrashHandler.Buffers;

public class MemoryMappedBuffer : IDisposable
{
    private const int MinHeaderLength = 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4;

    private readonly MemoryMappedFile           _file;
    private readonly MemoryMappedViewAccessor   _header;
    private readonly MemoryMappedViewAccessor[] _lines = [];

    public readonly  int  Version;
    public readonly  uint LineCount;
    public readonly  uint LineCapacity;
    private readonly uint _lineMask;
    private          bool _disposed;

    protected uint CurrentLineCount
    {
        get => _header.ReadUInt32(16);
        set => _header.Write(16, value);
    }

    protected uint CurrentLinePosition
    {
        get => _header.ReadUInt32(20);
        set => _header.Write(20, value);
    }

    public uint TotalWrittenLines
    {
        get => _header.ReadUInt32(24);
        protected set => _header.Write(24, value);
    }

    public MemoryMappedBuffer(string mapName, int version, uint lineCount, uint lineCapacity)
    {
        Version      = version;
        LineCount    = BitOperations.RoundUpToPowerOf2(Math.Clamp(lineCount,    2, int.MaxValue >> 3));
        LineCapacity = BitOperations.RoundUpToPowerOf2(Math.Clamp(lineCapacity, 2, int.MaxValue >> 3));
        _lineMask    = LineCount - 1;
        var fileName     = Encoding.UTF8.GetBytes(mapName);
        var headerLength = (uint)(4 + 4 + 4 + 4 + 4 + 4 + 4 + fileName.Length + 1);
        headerLength = (headerLength & 0b111) > 0 ? (headerLength & ~0b111u) + 0b1000 : headerLength;
        var capacity = LineCount * LineCapacity + headerLength;
        _file = MemoryMappedFile.CreateNew(mapName, capacity, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None,
            HandleInheritability.Inheritable);
        _header = _file.CreateViewAccessor(0, headerLength);
        _header.Write(0,  headerLength);
        _header.Write(4,  Version);
        _header.Write(8,  LineCount);
        _header.Write(12, LineCapacity);
        _header.WriteArray(28, fileName, 0, fileName.Length);
        _header.Write(fileName.Length + 28, (byte)0);
        _lines = Enumerable.Range(0, (int)LineCount).Select(i
                => _file.CreateViewAccessor(headerLength + i * LineCapacity, LineCapacity, MemoryMappedFileAccess.ReadWrite))
            .ToArray();
    }

    public MemoryMappedBuffer(string mapName, int? expectedVersion = null, uint? expectedMinLineCount = null,
        uint? expectedMinLineCapacity = null)
    {
        _lines = [];
        _file  = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.ReadWrite, HandleInheritability.Inheritable);
        using var headerLine   = _file.CreateViewAccessor(0, 4, MemoryMappedFileAccess.Read);
        var       headerLength = headerLine.ReadUInt32(0);
        if (headerLength < MinHeaderLength)
            Throw($"Map {mapName} did not contain a valid header.");

        _header      = _file.CreateViewAccessor(0, headerLength, MemoryMappedFileAccess.ReadWrite);
        Version      = _header.ReadInt32(4);
        LineCount    = _header.ReadUInt32(8);
        LineCapacity = _header.ReadUInt32(12);
        _lineMask    = LineCount - 1;
        if (expectedVersion.HasValue && expectedVersion.Value != Version)
            Throw($"Map {mapName} has version {Version} instead of {expectedVersion.Value}.");

        if (LineCount < expectedMinLineCount)
            Throw($"Map {mapName} has line count {LineCount} but line count >= {expectedMinLineCount.Value} is required.");

        if (LineCapacity < expectedMinLineCapacity)
            Throw($"Map {mapName} has line capacity {LineCapacity} but line capacity >= {expectedMinLineCapacity.Value} is required.");

        var name = ReadString(GetSpan(_header, 28));
        if (name != mapName)
            Throw($"Map {mapName} does not contain its map name at the expected location.");

        _lines = Enumerable.Range(0, (int)LineCount).Select(i
                => _file.CreateViewAccessor(headerLength + i * LineCapacity, LineCapacity, MemoryMappedFileAccess.ReadWrite))
            .ToArray();

        [DoesNotReturn]
        void Throw(string text)
        {
            _file.Dispose();
            _header?.Dispose();
            _disposed = true;
            throw new Exception(text);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    protected static string ReadString(Span<byte> span)
    {
        if (span.IsEmpty)
            throw new Exception("String from empty span requested.");

        var termination = span.IndexOf((byte)0);
        if (termination < 0)
            throw new Exception("String in span is not terminated.");

        return Encoding.UTF8.GetString(span[..termination]);
    }

    protected static int WriteString(string text, Span<byte> span)
    {
        var bytes  = Encoding.UTF8.GetBytes(text);
        var source = (Span<byte>)bytes;
        var length = source.Length + 1;
        if (length > span.Length)
            source = source[..(span.Length - 1)];
        source.CopyTo(span);
        span[bytes.Length] = 0;
        return source.Length + 1;
    }

    protected static int WriteSpan(ReadOnlySpan<byte> input, Span<byte> span)
    {
        var length = input.Length + 1;
        if (length > span.Length)
            input = input[..(span.Length - 1)];

        input.CopyTo(span);
        span[input.Length] = 0;
        return input.Length + 1;
    }

    protected Span<byte> GetLine(int i)
    {
        if (i < 0 || i > LineCount)
            return null;

        lock (_header)
        {
            var lineIdx = (CurrentLinePosition + i) & _lineMask;
            if (lineIdx > CurrentLineCount)
                return null;

            return GetSpan(_lines[lineIdx]);
        }
    }


    protected MemoryMappedViewAccessor GetCurrentLineLocking()
    {
        MemoryMappedViewAccessor view;
        lock (_header)
        {
            var currentLineCount = CurrentLineCount;
            if (currentLineCount == LineCount)
            {
                var currentLinePos = CurrentLinePosition;
                view                = _lines[currentLinePos]!;
                CurrentLinePosition = (currentLinePos + 1) & _lineMask;
            }
            else
            {
                view = _lines[currentLineCount];
                ++CurrentLineCount;
            }

            ++TotalWrittenLines;
            _header.Flush();
        }

        return view;
    }

    protected static Span<byte> GetSpan(MemoryMappedViewAccessor accessor, int offset = 0)
        => GetSpan(accessor, offset, (int)accessor.Capacity - offset);

    protected static unsafe Span<byte> GetSpan(MemoryMappedViewAccessor accessor, int offset, int size)
    {
        byte* ptr = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        size = Math.Min(size, (int)accessor.Capacity - offset);
        if (size < 0)
            return [];

        var span = new Span<byte>(ptr + offset + accessor.PointerOffset, size);
        return span;
    }

    protected void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _header?.Dispose();
        foreach (var line in _lines)
            line?.Dispose();
        _file?.Dispose();
    }

    ~MemoryMappedBuffer()
        => Dispose(false);
}
