using OtterGui.Classes;
using OtterGui.Log;
using Penumbra.Mods;
using Penumbra.Mods.Subclasses;

namespace Penumbra.Services;

/// <summary>
/// Any file type that we want to save via SaveService.
/// </summary>
public interface ISavable : ISavable<FilenameService>
{ }

public sealed class SaveService : SaveServiceBase<FilenameService>
{
    public SaveService(Logger log, FrameworkManager framework, FilenameService fileNames)
        : base(log, framework, fileNames)
    { }

    /// <summary> Immediately delete all existing option group files for a mod and save them anew. </summary>
    public void SaveAllOptionGroups(Mod mod, bool backup)
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

        for (var i = 0; i < mod.Groups.Count; ++i)
            ImmediateSave(new ModSaveGroup(mod, i));
    }
}
