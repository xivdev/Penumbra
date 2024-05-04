using Newtonsoft.Json.Linq;
using OtterGui.Classes;
using OtterGui.Log;
using OtterGui.Services;

namespace Penumbra.Services;

public class BackupService : IAsyncService
{
    private readonly Logger                  _logger;
    private readonly DirectoryInfo           _configDirectory;
    private readonly IReadOnlyList<FileInfo> _fileNames;

    /// <inheritdoc/>
    public Task Awaiter { get; }

    /// <inheritdoc/>
    public bool Finished
        => Awaiter.IsCompletedSuccessfully;

    /// <summary> Start a backup process on the collected files. </summary>
    public BackupService(Logger logger, FilenameService fileNames)
    {
        _logger          = logger;
        _fileNames       = PenumbraFiles(fileNames);
        _configDirectory = new DirectoryInfo(fileNames.ConfigDirectory);
        Awaiter          = Task.Run(() => Backup.CreateAutomaticBackup(logger, new DirectoryInfo(fileNames.ConfigDirectory), _fileNames));
    }

    /// <summary> Create a permanent backup with a given name for migrations. </summary>
    public void CreateMigrationBackup(string name)
        => Backup.CreatePermanentBackup(_logger, _configDirectory, _fileNames, name);

    /// <summary> Collect all relevant files for penumbra configuration. </summary>
    private static IReadOnlyList<FileInfo> PenumbraFiles(FilenameService fileNames)
    {
        var list = fileNames.CollectionFiles.ToList();
        list.AddRange(fileNames.LocalDataFiles);
        list.Add(new FileInfo(fileNames.ConfigFile));
        list.Add(new FileInfo(fileNames.FilesystemFile));
        list.Add(new FileInfo(fileNames.ActiveCollectionsFile));
        list.Add(new FileInfo(fileNames.PredefinedTagFile));
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
            Penumbra.Messager.NotificationMessage(messages);
        }

        return ret;
    }
}
