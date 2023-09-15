using OtterGui.Classes;
using OtterGui.Log;
using Penumbra.Util;

namespace Penumbra.Services;

public class BackupService
{
    public BackupService(Logger logger, StartTracker timer, FilenameService fileNames)
    {
        using var t     = timer.Measure(StartTimeType.Backup);
        var       files = PenumbraFiles(fileNames);
        Backup.CreateAutomaticBackup(logger, new DirectoryInfo(fileNames.ConfigDirectory), files);
    }

    // Collect all relevant files for penumbra configuration.
    private static IReadOnlyList<FileInfo> PenumbraFiles(FilenameService fileNames)
    {
        var list = fileNames.CollectionFiles.ToList();
        list.AddRange(fileNames.LocalDataFiles);
        list.Add(new FileInfo(fileNames.ConfigFile));
        list.Add(new FileInfo(fileNames.FilesystemFile));
        list.Add(new FileInfo(fileNames.ActiveCollectionsFile));
        return list;
    }
}
