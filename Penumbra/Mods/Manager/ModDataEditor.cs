using System;
using System.IO;
using System.Linq;
using Dalamud.Utility;
using Newtonsoft.Json.Linq;
using OtterGui.Classes;
using Penumbra.Services;
using Penumbra.Util;

namespace Penumbra.Mods;

public class ModDataEditor
{
    private readonly FilenameService     _filenameService;
    private readonly SaveService         _saveService;
    private readonly CommunicatorService _communicatorService;

    public ModDataEditor(FilenameService filenameService, SaveService saveService, CommunicatorService communicatorService)
    {
        _filenameService     = filenameService;
        _saveService         = saveService;
        _communicatorService = communicatorService;
    }

    public string MetaFile(Mod mod)
        => _filenameService.ModMetaPath(mod);

    public string DataFile(Mod mod)
        => _filenameService.LocalDataFile(mod);

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
        _saveService.ImmediateSave(new Mod.ModMeta(mod));
    }

    public ModDataChangeType LoadLocalData(Mod mod)
    {
        var dataFile = _filenameService.LocalDataFile(mod);

        var importDate = 0L;
        var localTags  = Enumerable.Empty<string>();
        var favorite   = false;
        var note       = string.Empty;

        var save = true;
        if (File.Exists(dataFile))
        {
            save = false;
            try
            {
                var text = File.ReadAllText(dataFile);
                var json = JObject.Parse(text);

                importDate = json[nameof(Mod.ImportDate)]?.Value<long>() ?? importDate;
                favorite   = json[nameof(Mod.Favorite)]?.Value<bool>() ?? favorite;
                note       = json[nameof(Mod.Note)]?.Value<string>() ?? note;
                localTags  = json[nameof(Mod.LocalTags)]?.Values<string>().OfType<string>() ?? localTags;
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not load local mod data:\n{e}");
            }
        }

        if (importDate == 0)
            importDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ModDataChangeType changes = 0;
        if (mod.ImportDate != importDate)
        {
            mod.ImportDate =  importDate;
            changes        |= ModDataChangeType.ImportDate;
        }

        changes |= mod.UpdateTags(null, localTags);

        if (mod.Favorite != favorite)
        {
            mod.Favorite =  favorite;
            changes      |= ModDataChangeType.Favorite;
        }

        if (mod.Note != note)
        {
            mod.Note =  note;
            changes  |= ModDataChangeType.Note;
        }

        if (save)
            _saveService.QueueSave(new Mod.ModData(mod));

        return changes;
    }

    public ModDataChangeType LoadMeta(Mod mod)
    {
        var metaFile = _filenameService.ModMetaPath(mod);
        if (!File.Exists(metaFile))
        {
            Penumbra.Log.Debug($"No mod meta found for {mod.ModPath.Name}.");
            return ModDataChangeType.Deletion;
        }

        try
        {
            var text = File.ReadAllText(metaFile);
            var json = JObject.Parse(text);

            var newName        = json[nameof(Mod.Name)]?.Value<string>() ?? string.Empty;
            var newAuthor      = json[nameof(Mod.Author)]?.Value<string>() ?? string.Empty;
            var newDescription = json[nameof(Mod.Description)]?.Value<string>() ?? string.Empty;
            var newVersion     = json[nameof(Mod.Version)]?.Value<string>() ?? string.Empty;
            var newWebsite     = json[nameof(Mod.Website)]?.Value<string>() ?? string.Empty;
            var newFileVersion = json[nameof(Mod.ModMeta.FileVersion)]?.Value<uint>() ?? 0;
            var importDate     = json[nameof(Mod.ImportDate)]?.Value<long>();
            var modTags        = json[nameof(Mod.ModTags)]?.Values<string>().OfType<string>();

            ModDataChangeType changes = 0;
            if (mod.Name != newName)
            {
                changes  |= ModDataChangeType.Name;
                mod.Name =  newName;
            }

            if (mod.Author != newAuthor)
            {
                changes    |= ModDataChangeType.Author;
                mod.Author =  newAuthor;
            }

            if (mod.Description != newDescription)
            {
                changes         |= ModDataChangeType.Description;
                mod.Description =  newDescription;
            }

            if (mod.Version != newVersion)
            {
                changes     |= ModDataChangeType.Version;
                mod.Version =  newVersion;
            }

            if (mod.Website != newWebsite)
            {
                changes     |= ModDataChangeType.Website;
                mod.Website =  newWebsite;
            }

            if (newFileVersion != Mod.ModMeta.FileVersion)
            {
                if (Mod.Migration.Migrate(mod, json, ref newFileVersion))
                {
                    changes |= ModDataChangeType.Migration;
                    _saveService.ImmediateSave(new Mod.ModMeta(mod));
                }
            }

            if (importDate != null && mod.ImportDate != importDate.Value)
            {
                mod.ImportDate =  importDate.Value;
                changes        |= ModDataChangeType.ImportDate;
            }

            changes |= mod.UpdateTags(modTags, null);

            return changes;
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not load mod meta:\n{e}");
            return ModDataChangeType.Deletion;
        }
    }

    public void ChangeModName(Mod mod, string newName)
    {
        if (mod.Name.Text == newName)
            return;

        var oldName = mod.Name;
        mod.Name = newName;
        _saveService.QueueSave(new Mod.ModMeta(mod));
        _communicatorService.ModDataChanged.Invoke(ModDataChangeType.Name, mod, oldName.Text);
    }

    public void ChangeModAuthor(Mod mod, string newAuthor)
    {
        if (mod.Author == newAuthor)
            return;

        mod.Author = newAuthor;
        _saveService.QueueSave(new Mod.ModMeta(mod));
        _communicatorService.ModDataChanged.Invoke(ModDataChangeType.Author, mod, null);
    }

    public void ChangeModDescription(Mod mod, string newDescription)
    {
        if (mod.Description == newDescription)
            return;

        mod.Description = newDescription;
        _saveService.QueueSave(new Mod.ModMeta(mod));
        _communicatorService.ModDataChanged.Invoke(ModDataChangeType.Description, mod, null);
    }

    public void ChangeModVersion(Mod mod, string newVersion)
    {
        if (mod.Version == newVersion)
            return;

        mod.Version = newVersion;
        _saveService.QueueSave(new Mod.ModMeta(mod));
        _communicatorService.ModDataChanged.Invoke(ModDataChangeType.Version, mod, null);
    }

    public void ChangeModWebsite(Mod mod, string newWebsite)
    {
        if (mod.Website == newWebsite)
            return;

        mod.Website = newWebsite;
        _saveService.QueueSave(new Mod.ModMeta(mod));
        _communicatorService.ModDataChanged.Invoke(ModDataChangeType.Website, mod, null);
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
        _saveService.QueueSave(new Mod.ModData(mod));
        ;
        _communicatorService.ModDataChanged.Invoke(ModDataChangeType.Favorite, mod, null);
    }

    public void ChangeModNote(Mod mod, string newNote)
    {
        if (mod.Note == newNote)
            return;

        mod.Note = newNote;
        _saveService.QueueSave(new Mod.ModData(mod));
        ;
        _communicatorService.ModDataChanged.Invoke(ModDataChangeType.Favorite, mod, null);
    }


    private void ChangeTag(Mod mod, int tagIdx, string newTag, bool local)
    {
        var which = local ? mod.LocalTags : mod.ModTags;
        if (tagIdx < 0 || tagIdx > which.Count)
            return;

        ModDataChangeType flags = 0;
        if (tagIdx == which.Count)
        {
            flags = mod.UpdateTags(local ? null : which.Append(newTag), local ? which.Append(newTag) : null);
        }
        else
        {
            var tmp = which.ToArray();
            tmp[tagIdx] = newTag;
            flags       = mod.UpdateTags(local ? null : tmp, local ? tmp : null);
        }

        if (flags.HasFlag(ModDataChangeType.ModTags))
            _saveService.QueueSave(new Mod.ModMeta(mod));

        if (flags.HasFlag(ModDataChangeType.LocalTags))
            _saveService.QueueSave(new Mod.ModData(mod));

        if (flags != 0)
            _communicatorService.ModDataChanged.Invoke(flags, mod, null);
    }

    public void MoveDataFile(DirectoryInfo oldMod, DirectoryInfo newMod)
    {
        var oldFile = _filenameService.LocalDataFile(oldMod.Name);
        var newFile = _filenameService.LocalDataFile(newMod.Name);
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
