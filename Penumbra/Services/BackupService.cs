using Newtonsoft.Json.Linq;
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

    /// <summary> Try to parse a file to JObject and check backups if this does not succeed. </summary>
    public static JObject? GetJObjectForFile(FilenameService fileNames, string fileName)
    {
        JObject? ret = null;
        if (!File.Exists(fileName))
            return ret;

        try
        {
            var text = File.ReadAllText(fileName);
            ret = JObject.Parse(text);
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Failed to load {fileName}, trying to restore from backup:\n{ex}");
            Backup.TryGetFile(new DirectoryInfo(fileNames.ConfigDirectory), fileName, out ret, out var messages, JObject.Parse);
            Penumbra.Chat.NotificationMessage(messages);
        }

        return ret;
    }
}
