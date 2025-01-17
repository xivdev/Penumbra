using Dalamud.Utility;
using Newtonsoft.Json.Linq;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

[Flags]
public enum ModDataChangeType : ushort
{
    None        = 0x0000,
    Name        = 0x0001,
    Author      = 0x0002,
    Description = 0x0004,
    Version     = 0x0008,
    Website     = 0x0010,
    Deletion    = 0x0020,
    Migration   = 0x0040,
    ModTags     = 0x0080,
    ImportDate  = 0x0100,
    Favorite    = 0x0200,
    LocalTags   = 0x0400,
    Note        = 0x0800,
    Image       = 0x1000,
}

public class ModDataEditor(SaveService saveService, CommunicatorService communicatorService) : IService
{
    public SaveService SaveService
        => saveService;

    /// <summary> Create the file containing the meta information about a mod from scratch. </summary>
    public void CreateMeta(DirectoryInfo directory, string? name, string? author, string? description, string? version,
        string? website)
    {
        var mod = new Mod(directory);
        mod.Name        = name.IsNullOrEmpty() ? mod.Name : new LowerString(name!);
        mod.Author      = author != null ? new LowerString(author) : mod.Author;
        mod.Description = description ?? mod.Description;
        mod.Version     = version ?? mod.Version;
        mod.Website     = website ?? mod.Website;
        saveService.ImmediateSaveSync(new ModMeta(mod));
    }

    public void ChangeModName(Mod mod, string newName)
    {
        if (mod.Name.Text == newName)
            return;

        var oldName = mod.Name;
        mod.Name = newName;
        saveService.QueueSave(new ModMeta(mod));
        communicatorService.ModDataChanged.Invoke(ModDataChangeType.Name, mod, oldName.Text);
    }

    public void ChangeModAuthor(Mod mod, string newAuthor)
    {
        if (mod.Author == newAuthor)
            return;

        mod.Author = newAuthor;
        saveService.QueueSave(new ModMeta(mod));
        communicatorService.ModDataChanged.Invoke(ModDataChangeType.Author, mod, null);
    }

    public void ChangeModDescription(Mod mod, string newDescription)
    {
        if (mod.Description == newDescription)
            return;

        mod.Description = newDescription;
        saveService.QueueSave(new ModMeta(mod));
        communicatorService.ModDataChanged.Invoke(ModDataChangeType.Description, mod, null);
    }

    public void ChangeModVersion(Mod mod, string newVersion)
    {
        if (mod.Version == newVersion)
            return;

        mod.Version = newVersion;
        saveService.QueueSave(new ModMeta(mod));
        communicatorService.ModDataChanged.Invoke(ModDataChangeType.Version, mod, null);
    }

    public void ChangeModWebsite(Mod mod, string newWebsite)
    {
        if (mod.Website == newWebsite)
            return;

        mod.Website = newWebsite;
        saveService.QueueSave(new ModMeta(mod));
        communicatorService.ModDataChanged.Invoke(ModDataChangeType.Website, mod, null);
    }

    public void ChangeModTag(Mod mod, int tagIdx, string newTag)
        => ChangeTag(mod, tagIdx, newTag, false);

    public void ChangeLocalTag(Mod mod, int tagIdx, string newTag)
        => ChangeTag(mod, tagIdx, newTag, true);

    public void ChangeModFavorite(Mod mod, bool state)
    {
        if (mod.Favorite == state)
            return;

        mod.Favorite = state;
        saveService.QueueSave(new ModLocalData(mod));
        communicatorService.ModDataChanged.Invoke(ModDataChangeType.Favorite, mod, null);
    }

    public void ResetModImportDate(Mod mod)
    {
        var newDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (mod.ImportDate == newDate)
            return;

        mod.ImportDate = newDate;
        saveService.QueueSave(new ModLocalData(mod));
        communicatorService.ModDataChanged.Invoke(ModDataChangeType.ImportDate, mod, null);
    }

    public void ChangeModNote(Mod mod, string newNote)
    {
        if (mod.Note == newNote)
            return;

        mod.Note = newNote;
        saveService.QueueSave(new ModLocalData(mod));
        communicatorService.ModDataChanged.Invoke(ModDataChangeType.Favorite, mod, null);
    }

    private void ChangeTag(Mod mod, int tagIdx, string newTag, bool local)
    {
        var which = local ? mod.LocalTags : mod.ModTags;
        if (tagIdx < 0 || tagIdx > which.Count)
            return;

        ModDataChangeType flags;
        if (tagIdx == which.Count)
        {
            flags = ModLocalData.UpdateTags(mod, local ? null : which.Append(newTag), local ? which.Append(newTag) : null);
        }
        else
        {
            var tmp = which.ToArray();
            tmp[tagIdx] = newTag;
            flags       = ModLocalData.UpdateTags(mod, local ? null : tmp, local ? tmp : null);
        }

        if (flags.HasFlag(ModDataChangeType.ModTags))
            saveService.QueueSave(new ModMeta(mod));

        if (flags.HasFlag(ModDataChangeType.LocalTags))
            saveService.QueueSave(new ModLocalData(mod));

        if (flags != 0)
            communicatorService.ModDataChanged.Invoke(flags, mod, null);
    }

    public void MoveDataFile(DirectoryInfo oldMod, DirectoryInfo newMod)
    {
        var oldFile = saveService.FileNames.LocalDataFile(oldMod.Name);
        var newFile = saveService.FileNames.LocalDataFile(newMod.Name);
        if (!File.Exists(oldFile))
            return;

        try
        {
            File.Move(oldFile, newFile, true);
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not move local data file {oldFile} to {newFile}:\n{e}");
        }
    }
}
