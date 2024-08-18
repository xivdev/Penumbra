using Penumbra.CrashHandler.Buffers;

namespace Penumbra.CrashHandler;

/// <summary> A base entry for crash data. </summary>
public interface ICrashDataEntry
{
    /// <summary> The timestamp of the event. </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary> The thread invoking the event. </summary>
    int ThreadId { get; }

    /// <summary> The age of the event compared to the crash. (Redundantly with the timestamp) </summary>
    double Age { get; }
}

/// <summary> A full set of crash data. </summary>
public class CrashData
{
    /// <summary> The mode this data was obtained - manually or from a crash. </summary>
    public string Mode { get; set; } = "Unknown";

    /// <summary> The time this crash data was generated. </summary>
    public DateTimeOffset CrashTime { get; set; } = DateTimeOffset.UnixEpoch;

    /// <summary> Penumbra's Version when this crash data was created. </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary> The Game's Version when this crash data was created. </summary>
    public string GameVersion { get; set; } = string.Empty;

    /// <summary> The FFXIV process ID when this data was generated. </summary>
    public int ProcessId { get; set; } = 0;

    /// <summary> The FFXIV Exit Code (if any) when this data was generated. </summary>
    public int ExitCode { get; set; } = 0;

    /// <summary> The total amount of characters loaded during this session. </summary>
    public int TotalCharactersLoaded { get; set; } = 0;

    /// <summary> The total amount of modded files loaded during this session. </summary>
    public int TotalModdedFilesLoaded { get; set; } = 0;

    /// <summary> The total amount of vfx functions invoked during this session. </summary>
    public int TotalVFXFuncsInvoked { get; set; } = 0;

    /// <summary> The last character loaded before this crash data was generated. </summary>
    public CharacterLoadedEntry? LastCharacterLoaded
        => LastCharactersLoaded.Count == 0 ? default : LastCharactersLoaded[0];

    /// <summary> The last modded file loaded before this crash data was generated. </summary>
    public ModdedFileLoadedEntry? LastModdedFileLoaded
        => LastModdedFilesLoaded.Count == 0 ? default : LastModdedFilesLoaded[0];

    /// <summary> The last vfx function invoked before this crash data was generated. </summary>
    public VfxFuncInvokedEntry? LastVfxFuncInvoked
        => LastVFXFuncsInvoked.Count == 0 ? default : LastVFXFuncsInvoked[0];

    /// <summary> A collection of the last few characters loaded before this crash data was generated. </summary>
    public List<CharacterLoadedEntry> LastCharactersLoaded { get; set; } = [];

    /// <summary> A collection of the last few modded files loaded before this crash data was generated. </summary>
    public List<ModdedFileLoadedEntry> LastModdedFilesLoaded { get; set; } = [];

    /// <summary> A collection of the last few vfx functions invoked before this crash data was generated. </summary>
    public List<VfxFuncInvokedEntry> LastVFXFuncsInvoked { get; set; } = [];
}
