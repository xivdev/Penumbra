using System.Text.Json.Nodes;

namespace Penumbra.CrashHandler.Buffers;

/// <summary> Only expose the write interface for the buffer. </summary>
public interface ICharacterBaseBufferWriter
{
    /// <summary> Write a line into the buffer with the given data. </summary>
    /// <param name="characterAddress"> The address of the related character, if known. </param>
    /// <param name="characterName"> The name of the related character, anonymized or relying on index if unavailable, if known. </param>
    /// <param name="collectionId"> The GUID of the associated collection. </param>
    public void WriteLine(nint characterAddress, ReadOnlySpan<byte> characterName, Guid collectionId);
}

/// <summary> The full crash entry for a loaded character base. </summary>
public record struct CharacterLoadedEntry(
    double Age,
    DateTimeOffset Timestamp,
    int ThreadId,
    string CharacterName,
    string CharacterAddress,
    Guid CollectionId) : ICrashDataEntry;

internal sealed class CharacterBaseBuffer : MemoryMappedBuffer, ICharacterBaseBufferWriter, IBufferReader
{
    private const int    _version      = 1;
    private const int    _lineCount    = 10;
    private const int    _lineCapacity = 128;
    private const string _name         = "Penumbra.CharacterBase";

    public void WriteLine(nint characterAddress, ReadOnlySpan<byte> characterName, Guid collectionId)
    {
        var accessor = GetCurrentLineLocking();
        lock (accessor)
        {
            accessor.Write(0,  DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            accessor.Write(8,  Environment.CurrentManagedThreadId);
            accessor.Write(12, characterAddress);
            var span = GetSpan(accessor, 20, 16);
            collectionId.TryWriteBytes(span);
            span = GetSpan(accessor, 36);
            WriteSpan(characterName, span);
        }
    }

    public IEnumerable<JsonObject> GetLines(DateTimeOffset crashTime)
    {
        var lineCount = (int)CurrentLineCount;
        for (var i = lineCount - 1; i >= 0; --i)
        {
            var line          = GetLine(i);
            var timestamp     = DateTimeOffset.FromUnixTimeMilliseconds(BitConverter.ToInt64(line));
            var thread        = BitConverter.ToInt32(line[8..]);
            var address       = BitConverter.ToUInt64(line[12..]);
            var collectionId  = new Guid(line[20..36]);
            var characterName = ReadString(line[36..]);
            yield return new JsonObject
            {
                [nameof(CharacterLoadedEntry.Age)]              = (crashTime - timestamp).TotalSeconds,
                [nameof(CharacterLoadedEntry.Timestamp)]        = timestamp,
                [nameof(CharacterLoadedEntry.ThreadId)]         = thread,
                [nameof(CharacterLoadedEntry.CharacterName)]    = characterName,
                [nameof(CharacterLoadedEntry.CharacterAddress)] = address.ToString("X"),
                [nameof(CharacterLoadedEntry.CollectionId)]     = collectionId,
            };
        }
    }

    public uint TotalCount
        => TotalWrittenLines;

    public static IBufferReader CreateReader(int pid)
        => new CharacterBaseBuffer(false, pid);

    public static ICharacterBaseBufferWriter CreateWriter(int pid)
        => new CharacterBaseBuffer(pid);

    private CharacterBaseBuffer(bool writer, int pid)
        : base($"{_name}_{pid}_{_version}", _version)
    { }

    private CharacterBaseBuffer(int pid)
        : base($"{_name}_{pid}_{_version}", _version, _lineCount, _lineCapacity)
    { }
}
