using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.GameData;
using Penumbra.Import;
using Penumbra.Import.Structs;
using Penumbra.Meta;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Mods;

public partial class ModCreator
{
    private readonly Configuration   _config;
    private readonly SaveService     _saveService;
    private readonly ModDataEditor   _dataEditor;
    private readonly MetaFileManager _metaFileManager;
    private readonly IGamePathParser _gamePathParser;

    public ModCreator(SaveService saveService, Configuration config, ModDataEditor dataEditor, MetaFileManager metaFileManager,
        IGamePathParser gamePathParser)
    {
        _saveService     = saveService;
        _config          = config;
        _dataEditor      = dataEditor;
        _metaFileManager = metaFileManager;
        _gamePathParser  = gamePathParser;
    }

    /// <summary> Creates directory and files necessary for a new mod without adding it to the manager. </summary>
    public DirectoryInfo? CreateEmptyMod(DirectoryInfo basePath, string newName, string description = "")
    {
        try
        {
            var newDir = CreateModFolder(basePath, newName);
            _dataEditor.CreateMeta(newDir, newName, _config.DefaultModAuthor, description, "1.0", string.Empty);
            CreateDefaultFiles(newDir);
            return newDir;
        }
        catch (Exception e)
        {
            Penumbra.Chat.NotificationMessage($"Could not create directory for new Mod {newName}:\n{e}", "Failure",
                NotificationType.Error);
            return null;
        }
    }

    /// <summary> Load a mod by its directory. </summary>
    public Mod? LoadMod(DirectoryInfo modPath, bool incorporateMetaChanges)
    {
        modPath.Refresh();
        if (!modPath.Exists)
        {
            Penumbra.Log.Error($"Supplied mod directory {modPath} does not exist.");
            return null;
        }

        var mod = new Mod(modPath);
        if (ReloadMod(mod, incorporateMetaChanges, out _))
            return mod;

        // Can not be base path not existing because that is checked before.
        Penumbra.Log.Warning($"Mod at {modPath} without name is not supported.");
        return null;
    }

    /// <summary> Reload a mod from its mod path. </summary>
    public bool ReloadMod(Mod mod, bool incorporateMetaChanges, out ModDataChangeType modDataChange)
    {
        modDataChange = ModDataChangeType.Deletion;
        if (!Directory.Exists(mod.ModPath.FullName))
            return false;

        modDataChange = _dataEditor.LoadMeta(this, mod);
        if (modDataChange.HasFlag(ModDataChangeType.Deletion) || mod.Name.Length == 0)
            return false;

        _dataEditor.LoadLocalData(mod);
        LoadDefaultOption(mod);
        LoadAllGroups(mod);
        if (incorporateMetaChanges)
            IncorporateAllMetaChanges(mod, true);

        return true;
    }

    /// <summary> Load all option groups for a given mod. </summary>
    public void LoadAllGroups(Mod mod)
    {
        mod.Groups.Clear();
        var changes = false;
        foreach (var file in _saveService.FileNames.GetOptionGroupFiles(mod))
        {
            var group = LoadModGroup(mod, file, mod.Groups.Count);
            if (group != null && mod.Groups.All(g => g.Name != group.Name))
            {
                changes = changes
                 || _saveService.FileNames.OptionGroupFile(mod.ModPath.FullName, mod.Groups.Count, group.Name) != file.FullName;
                mod.Groups.Add(group);
            }
            else
            {
                changes = true;
            }
        }

        if (changes)
            _saveService.SaveAllOptionGroups(mod);
    }

    /// <summary> Load the default option for a given mod.</summary>
    public void LoadDefaultOption(Mod mod)
    {
        var defaultFile = _saveService.FileNames.OptionGroupFile(mod, -1);
        mod.Default.SetPosition(-1, 0);
        try
        {
            if (!File.Exists(defaultFile))
                mod.Default.Load(mod.ModPath, new JObject(), out _);
            else
                mod.Default.Load(mod.ModPath, JObject.Parse(File.ReadAllText(defaultFile)), out _);
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not parse default file for {mod.Name}:\n{e}");
        }
    }

    /// <summary>
    /// Create and return a new directory based on the given directory and name, that is <br/>
    ///    - Not Empty.<br/>
    ///    - Unique, by appending (digit) for duplicates.<br/>
    ///    - Containing no symbols invalid for FFXIV or windows paths.<br/>
    /// </summary>
    public static DirectoryInfo CreateModFolder(DirectoryInfo outDirectory, string modListName, bool create = true)
    {
        var name = modListName;
        if (name.Length == 0)
            name = "_";

        var newModFolderBase = NewOptionDirectory(outDirectory, name);
        var newModFolder     = newModFolderBase.FullName.ObtainUniqueFile();
        if (newModFolder.Length == 0)
            throw new IOException("Could not create mod folder: too many folders of the same name exist.");

        if (create)
            Directory.CreateDirectory(newModFolder);

        return new DirectoryInfo(newModFolder);
    }

    /// <summary>
    /// Convert all .meta and .rgsp files to their respective meta changes and add them to their options.
    /// Deletes the source files if delete is true.
    /// </summary>
    public void IncorporateAllMetaChanges(Mod mod, bool delete)
    {
        var          changes    = false;
        List<string> deleteList = new();
        foreach (var subMod in mod.AllSubMods)
        {
            var (localChanges, localDeleteList) =  IncorporateMetaChanges(subMod, mod.ModPath, false);
            changes                             |= localChanges;
            if (delete)
                deleteList.AddRange(localDeleteList);
        }

        SubMod.DeleteDeleteList(deleteList, delete);

        if (!changes)
            return;

        _saveService.SaveAllOptionGroups(mod);
        _saveService.ImmediateSave(new ModSaveGroup(mod.ModPath, mod.Default));
    }


    /// <summary>
    /// If .meta or .rgsp files are encountered, parse them and incorporate their meta changes into the mod.
    /// If delete is true, the files are deleted afterwards.
    /// </summary>
    public (bool Changes, List<string> DeleteList) IncorporateMetaChanges(SubMod option, DirectoryInfo basePath, bool delete)
    {
        var deleteList   = new List<string>();
        var oldSize      = option.ManipulationData.Count;
        var deleteString = delete ? "with deletion." : "without deletion.";
        foreach (var (key, file) in option.Files.ToList())
        {
            var ext1 = key.Extension().AsciiToLower().ToString();
            var ext2 = file.Extension.ToLowerInvariant();
            try
            {
                if (ext1 == ".meta" || ext2 == ".meta")
                {
                    option.FileData.Remove(key);
                    if (!file.Exists)
                        continue;

                    var meta = new TexToolsMeta(_metaFileManager, _gamePathParser, File.ReadAllBytes(file.FullName),
                        _config.KeepDefaultMetaChanges);
                    Penumbra.Log.Verbose(
                        $"Incorporating {file} as Metadata file of {meta.MetaManipulations.Count} manipulations {deleteString}");
                    deleteList.Add(file.FullName);
                    option.ManipulationData.UnionWith(meta.MetaManipulations);
                }
                else if (ext1 == ".rgsp" || ext2 == ".rgsp")
                {
                    option.FileData.Remove(key);
                    if (!file.Exists)
                        continue;

                    var rgsp = TexToolsMeta.FromRgspFile(_metaFileManager, file.FullName, File.ReadAllBytes(file.FullName),
                        _config.KeepDefaultMetaChanges);
                    Penumbra.Log.Verbose(
                        $"Incorporating {file} as racial scaling file of {rgsp.MetaManipulations.Count} manipulations {deleteString}");
                    deleteList.Add(file.FullName);

                    option.ManipulationData.UnionWith(rgsp.MetaManipulations);
                }
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not incorporate meta changes in mod {basePath} from file {file.FullName}:\n{e}");
            }
        }

        SubMod.DeleteDeleteList(deleteList, delete);
        return (oldSize < option.ManipulationData.Count, deleteList);
    }

    /// <summary>
    /// Create the name for a group or option subfolder based on its parent folder and given name.
    /// subFolderName should never be empty, and the result is unique and contains no invalid symbols.
    /// </summary>
    public static DirectoryInfo? NewSubFolderName(DirectoryInfo parentFolder, string subFolderName)
    {
        var newModFolderBase = NewOptionDirectory(parentFolder, subFolderName);
        var newModFolder     = newModFolderBase.FullName.ObtainUniqueFile();
        return newModFolder.Length == 0 ? null : new DirectoryInfo(newModFolder);
    }

    /// <summary> Create a file for an option group from given data. </summary>
    public void CreateOptionGroup(DirectoryInfo baseFolder, GroupType type, string name,
        int priority, int index, uint defaultSettings, string desc, IEnumerable<ISubMod> subMods)
    {
        switch (type)
        {
            case GroupType.Multi:
            {
                var group = new MultiModGroup()
                {
                    Name            = name,
                    Description     = desc,
                    Priority        = priority,
                    DefaultSettings = defaultSettings,
                };
                group.PrioritizedOptions.AddRange(subMods.OfType<SubMod>().Select((s, idx) => (s, idx)));
                _saveService.ImmediateSave(new ModSaveGroup(baseFolder, group, index));
                break;
            }
            case GroupType.Single:
            {
                var group = new SingleModGroup()
                {
                    Name            = name,
                    Description     = desc,
                    Priority        = priority,
                    DefaultSettings = defaultSettings,
                };
                group.OptionData.AddRange(subMods.OfType<SubMod>());
                _saveService.ImmediateSave(new ModSaveGroup(baseFolder, group, index));
                break;
            }
        }
    }

    /// <summary> Create the data for a given sub mod from its data and the folder it is based on. </summary>
    public ISubMod CreateSubMod(DirectoryInfo baseFolder, DirectoryInfo optionFolder, OptionList option)
    {
        var list = optionFolder.EnumerateNonHiddenFiles()
            .Select(f => (Utf8GamePath.FromFile(f, optionFolder, out var gamePath, true), gamePath, new FullPath(f)))
            .Where(t => t.Item1);

        var mod = new SubMod(null!) // Mod is irrelevant here, only used for saving.
        {
            Name        = option.Name,
            Description = option.Description,
        };
        foreach (var (_, gamePath, file) in list)
            mod.FileData.TryAdd(gamePath, file);

        IncorporateMetaChanges(mod, baseFolder, true);
        return mod;
    }

    /// <summary> Create an empty sub mod for single groups with None options. </summary>
    internal static ISubMod CreateEmptySubMod(string name)
        => new SubMod(null!) // Mod is irrelevant here, only used for saving.
        {
            Name = name,
        };

    /// <summary>
    /// Create the default data file from all unused files that were not handled before
    /// and are used in sub mods.
    /// </summary>
    internal void CreateDefaultFiles(DirectoryInfo directory)
    {
        var mod = new Mod(directory);
        ReloadMod(mod, false, out _);
        foreach (var file in mod.FindUnusedFiles())
        {
            if (Utf8GamePath.FromFile(new FileInfo(file.FullName), directory, out var gamePath, true))
                mod.Default.FileData.TryAdd(gamePath, file);
        }

        IncorporateMetaChanges(mod.Default, directory, true);
        _saveService.ImmediateSave(new ModSaveGroup(mod, -1));
    }

    /// <summary> Return the name of a new valid directory based on the base directory and the given name. </summary>
    public static DirectoryInfo NewOptionDirectory(DirectoryInfo baseDir, string optionName)
    {
        var option = ReplaceBadXivSymbols(optionName);
        return new DirectoryInfo(Path.Combine(baseDir.FullName, option.Length > 0 ? option : "_"));
    }

    /// <summary> Normalize for nicer names, and remove invalid symbols or invalid paths. </summary>
    public static string ReplaceBadXivSymbols(string s, string replacement = "_")
    {
        switch (s)
        {
            case ".":  return replacement;
            case "..": return replacement + replacement;
        }

        StringBuilder sb = new(s.Length);
        foreach (var c in s.Normalize(NormalizationForm.FormKC))
        {
            if (c.IsInvalidInPath())
                sb.Append(replacement);
            else
                sb.Append(c);
        }

        return sb.ToString().Trim();
    }

    public void SplitMultiGroups(DirectoryInfo baseDir)
    {
        var mod = new Mod(baseDir);

        var files   = _saveService.FileNames.GetOptionGroupFiles(mod).ToList();
        var idx     = 0;
        var reorder = false;
        foreach (var groupFile in files)
        {
            ++idx;
            try
            {
                if (reorder)
                {
                    var newName = $"{baseDir.FullName}\\group_{idx:D3}{groupFile.Name[9..]}";
                    Penumbra.Log.Debug($"Moving {groupFile.Name} to {Path.GetFileName(newName)} due to reordering after multi group split.");
                    groupFile.MoveTo(newName, false);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Could not reorder group file after splitting multi group on .pmp import.", ex);
            }

            try
            {
                var json = JObject.Parse(File.ReadAllText(groupFile.FullName));
                if (json[nameof(IModGroup.Type)]?.ToObject<GroupType>() is not GroupType.Multi)
                    continue;

                var name = json[nameof(IModGroup.Name)]?.ToObject<string>() ?? string.Empty;
                if (name.Length == 0)
                    continue;


                var options = json["Options"]?.Children().ToList();
                if (options is not { Count: > IModGroup.MaxMultiOptions })
                    continue;

                Penumbra.Log.Information($"Splitting multi group {name} in {mod.Name} due to {options.Count} being too many options.");
                var clone = json.DeepClone();
                reorder = true;
                foreach (var o in options.Skip(IModGroup.MaxMultiOptions))
                    o.Remove();

                var newOptions = clone["Options"]!.Children().ToList();
                foreach (var o in newOptions.Take(IModGroup.MaxMultiOptions))
                    o.Remove();

                var match       = DuplicateNumber().Match(name);
                var startNumber = match.Success ? int.Parse(match.Groups[0].Value) : 1;
                name = match.Success ? name[..4] : name;
                var oldName = $"{name}, Part {startNumber}";
                var oldPath = $"{baseDir.FullName}\\group_{idx:D3}_{oldName.RemoveInvalidPathSymbols().ToLowerInvariant()}.json";
                var newName = $"{name}, Part {startNumber + 1}";
                var newPath = $"{baseDir.FullName}\\group_{++idx:D3}_{newName.RemoveInvalidPathSymbols().ToLowerInvariant()}.json";
                json[nameof(IModGroup.Name)]  = oldName;
                clone[nameof(IModGroup.Name)] = newName;

                clone[nameof(IModGroup.DefaultSettings)] = 0u;

                Penumbra.Log.Debug($"Writing the first {IModGroup.MaxMultiOptions} options to {Path.GetFileName(oldPath)} after split.");
                using (var oldFile = File.CreateText(oldPath))
                {
                    using var j = new JsonTextWriter(oldFile)
                    {
                        Formatting = Formatting.Indented,
                    };
                    json.WriteTo(j);
                }

                Penumbra.Log.Debug(
                    $"Writing the remaining {options.Count - IModGroup.MaxMultiOptions} options to {Path.GetFileName(newPath)} after split.");
                using (var newFile = File.CreateText(newPath))
                {
                    using var j = new JsonTextWriter(newFile)
                    {
                        Formatting = Formatting.Indented,
                    };
                    clone.WriteTo(j);
                }

                Penumbra.Log.Debug(
                    $"Deleting the old group file at {groupFile.Name} after splitting it into {Path.GetFileName(oldPath)} and {Path.GetFileName(newPath)}.");
                groupFile.Delete();
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not split multi group file {groupFile.Name} on .pmp import.", ex);
            }
        }
    }

    [GeneratedRegex(@", Part (\d+)$", RegexOptions.NonBacktracking)]
    private static partial Regex DuplicateNumber();


    /// <summary> Load an option group for a specific mod by its file and index. </summary>
    private static IModGroup? LoadModGroup(Mod mod, FileInfo file, int groupIdx)
    {
        if (!File.Exists(file.FullName))
            return null;

        try
        {
            var json = JObject.Parse(File.ReadAllText(file.FullName));
            switch (json[nameof(Type)]?.ToObject<GroupType>() ?? GroupType.Single)
            {
                case GroupType.Multi:  return MultiModGroup.Load(mod, json, groupIdx);
                case GroupType.Single: return SingleModGroup.Load(mod, json, groupIdx);
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not read mod group from {file.FullName}:\n{e}");
        }

        return null;
    }
}
