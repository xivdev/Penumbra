using OtterGui.Classes;
using OtterGui.Log;
using OtterGui.Services;
using Penumbra.Mods;
using Penumbra.Mods.Groups;

namespace Penumbra.Services;

/// <summary>
/// Any file type that we want to save via SaveService.
/// </summary>
public interface ISavable : ISavable<FilenameService>
{ }

public sealed class SaveService(Logger log, FrameworkManager framework, FilenameService fileNames, BackupService backupService)
    : SaveServiceBase<FilenameService>(log, framework, fileNames, backupService.Awaiter), IService
{
    /// <summary> Immediately delete all existing option group files for a mod and save them anew. </summary>
    public void SaveAllOptionGroups(Mod mod, bool backup, bool onlyAscii)
    {
        foreach (var file in FileNames.GetOptionGroupFiles(mod))
        {
            try
            {
                if (file.Exists)
                    if (backup)
                        file.MoveTo(file.FullName + ".bak", true);
                    else
                        file.Delete();
            }
            catch (Exception e)
            {
                Log.Error($"Could not {(backup ? "move" : "delete")} outdated group file {file}:\n{e}");
            }
        }

        if (mod.Groups.Count > 0)
        {
            foreach (var group in mod.Groups.SkipLast(1))
                ImmediateSave(new ModSaveGroup(group, onlyAscii));
            ImmediateSaveSync(new ModSaveGroup(mod.Groups[^1], onlyAscii));
        }
    }
}
