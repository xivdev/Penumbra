using Luna;
using Newtonsoft.Json.Linq;

namespace Penumbra.Services;

public sealed class BackupService(Logger log, FilenameService provider) : BaseBackupService<FilenameService>(log, provider)
{
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
            Backup.TryGetFile(new DirectoryInfo(fileNames.ConfigurationDirectory), fileName, out ret, out var messages, JObject.Parse);
            Penumbra.Messager.NotificationMessage(messages);
        }

        return ret;
    }
}
