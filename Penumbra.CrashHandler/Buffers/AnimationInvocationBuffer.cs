using System.Text.Json.Nodes;

namespace Penumbra.CrashHandler.Buffers;

/// <summary> The types of currently hooked and relevant animation loading functions. </summary>
public enum AnimationInvocationType : int
{
    PapLoad,
    ActionLoad,
    ScheduleClipUpdate,
    LoadTimelineResources,
    LoadCharacterVfx,
    LoadCharacterSound,
    ApricotSoundPlay,
    LoadAreaVfx,
    CharacterBaseLoadAnimation,
}

/// <summary> The full crash entry for an invoked vfx function. </summary>
public record struct VfxFuncInvokedEntry(
    double Age,
    DateTimeOffset Timestamp,
    int ThreadId,
    string InvocationType,
    string CharacterName,
    string CharacterAddress,
    Guid CollectionId) : ICrashDataEntry;

/// <summary> Only expose the write interface for the buffer. </summary>
public interface IAnimationInvocationBufferWriter
{
    /// <summary> Write a line into the buffer with the given data. </summary>
    /// <param name="characterAddress"> The address of the related character, if known. </param>
    /// <param name="characterName"> The name of the related character, anonymized or relying on index if unavailable, if known. </param>
    /// <param name="collectionId"> The GUID of the associated collection. </param>
    /// <param name="type"> The type of VFX func called. </param>
    public void WriteLine(nint characterAddress, ReadOnlySpan<byte> characterName, Guid collectionId, AnimationInvocationType type);
}

internal sealed class AnimationInvocationBuffer : MemoryMappedBuffer, IAnimationInvocationBufferWriter, IBufferReader
{
    private const int    _version      = 1;
    private const int    _lineCount    = 64;
    private const int    _lineCapacity = 128;
    private const string _name         = "Penumbra.AnimationInvocation";

    public void WriteLine(nint characterAddress, ReadOnlySpan<byte> characterName, Guid collectionId, AnimationInvocationType type)
    {
        var accessor = GetCurrentLineLocking();
        lock (accessor)
        {
            accessor.Write(0,  DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            accessor.Write(8,  Environment.CurrentManagedThreadId);
            accessor.Write(12, (int)type);
            accessor.Write(16, characterAddress);
            var span = GetSpan(accessor, 24, 16);
            collectionId.TryWriteBytes(span);
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            span = GetSpan(accessor, 40);
            WriteSpan(characterName, span);
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    public uint TotalCount
        => TotalWrittenLines;

    public IEnumerable<JsonObject> GetLines(DateTimeOffset crashTime)
    {
        var lineCount = (int)CurrentLineCount;
        for (var i = lineCount - 1; i >= 0; --i)
        {
            var line          = GetLine(i);
            var timestamp     = DateTimeOffset.FromUnixTimeMilliseconds(BitConverter.ToInt64(line));
            var thread        = BitConverter.ToInt32(line[8..]);
            var type          = (AnimationInvocationType)BitConverter.ToInt32(line[12..]);
            var address       = BitConverter.ToUInt64(line[16..]);
            var collectionId  = new Guid(line[24..40]);
            var characterName = ReadString(line[40..]);
            yield return new JsonObject()
            {
                [nameof(VfxFuncInvokedEntry.Age)]              = (crashTime - timestamp).TotalSeconds,
                [nameof(VfxFuncInvokedEntry.Timestamp)]        = timestamp,
                [nameof(VfxFuncInvokedEntry.ThreadId)]         = thread,
                [nameof(VfxFuncInvokedEntry.InvocationType)]   = ToName(type),
                [nameof(VfxFuncInvokedEntry.CharacterName)]    = characterName,
                [nameof(VfxFuncInvokedEntry.CharacterAddress)] = address.ToString("X"),
                [nameof(VfxFuncInvokedEntry.CollectionId)]     = collectionId,
            };
        }
    }

    public static IBufferReader CreateReader(int pid)
        => new AnimationInvocationBuffer(false, pid);

    public static IAnimationInvocationBufferWriter CreateWriter(int pid)
        => new AnimationInvocationBuffer(pid);

    private AnimationInvocationBuffer(bool writer, int pid)
        : base($"{_name}_{pid}_{_version}", _version)
    { }

    private AnimationInvocationBuffer(int pid)
        : base($"{_name}_{pid}_{_version}", _version, _lineCount, _lineCapacity)
    { }

    private static string ToName(AnimationInvocationType type)
        => type switch
        {
            AnimationInvocationType.PapLoad                    => "PAP Load",
            AnimationInvocationType.ActionLoad                 => "Action Load",
            AnimationInvocationType.ScheduleClipUpdate         => "Schedule Clip Update",
            AnimationInvocationType.LoadTimelineResources      => "Load Timeline Resources",
            AnimationInvocationType.LoadCharacterVfx           => "Load Character VFX",
            AnimationInvocationType.LoadCharacterSound         => "Load Character Sound",
            AnimationInvocationType.ApricotSoundPlay           => "Apricot Sound Play",
            AnimationInvocationType.LoadAreaVfx                => "Load Area VFX",
            AnimationInvocationType.CharacterBaseLoadAnimation => "Load Animation (CharacterBase)",
            _                                                  => $"Unknown ({(int)type})",
        };
}
