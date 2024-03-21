using System.Text.Json.Nodes;
using Penumbra.CrashHandler.Buffers;

namespace Penumbra.CrashHandler;

public interface IBufferReader
{
    public uint                    TotalCount { get; }
    public IEnumerable<JsonObject> GetLines(DateTimeOffset crashTime);
}

public sealed class GameEventLogReader(int pid) : IDisposable
{
    public readonly (IBufferReader Reader, string TypeSingular, string TypePlural)[] Readers =
    [
        (CharacterBaseBuffer.CreateReader(pid), "CharacterLoaded", "CharactersLoaded"),
        (ModdedFileBuffer.CreateReader(pid), "ModdedFileLoaded", "ModdedFilesLoaded"),
        (AnimationInvocationBuffer.CreateReader(pid), "VFXFuncInvoked", "VFXFuncsInvoked"),
    ];

    public void Dispose()
    {
        foreach (var (reader, _, _) in Readers)
            (reader as IDisposable)?.Dispose();
    }


    public JsonObject Dump(string mode, int processId, int exitCode, string version, string gameVersion)
    {
        var crashTime = DateTimeOffset.UtcNow;
        var obj = new JsonObject
        {
            [nameof(CrashData.Mode)]        = mode,
            [nameof(CrashData.CrashTime)]   = DateTimeOffset.UtcNow,
            [nameof(CrashData.ProcessId)]   = processId,
            [nameof(CrashData.ExitCode)]    = exitCode,
            [nameof(CrashData.Version)]     = version,
            [nameof(CrashData.GameVersion)] = gameVersion,
        };

        foreach (var (reader, singular, _) in Readers)
            obj["Last" + singular] = reader.GetLines(crashTime).FirstOrDefault();

        foreach (var (reader, _, plural) in Readers)
        {
            obj["Total" + plural] = reader.TotalCount;
            var array = new JsonArray();
            foreach (var file in reader.GetLines(crashTime))
                array.Add(file);
            obj["Last" + plural] = array;
        }

        return obj;
    }
}
