using System;
using OtterGui.Classes;
using OtterGui.Log;
using Penumbra.Mods;

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
    public void SaveAllOptionGroups(Mod mod)
    {
        foreach (var file in FileNames.GetOptionGroupFiles(mod))
        {
            try
            {
                if (file.Exists)
                    file.Delete();
            }
            catch (Exception e)
            {
                Log.Error($"Could not delete outdated group file {file}:\n{e}");
            }
        }

        for (var i = 0; i < mod.Groups.Count; ++i)
            ImmediateSave(new ModSaveGroup(mod, i));
    }
}
