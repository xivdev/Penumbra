using Dalamud.Interface.ImGuiNotification;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.Import;
using Penumbra.Import.Structs;
using Penumbra.Meta;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

public partial class ModCreator(
    SaveService saveService,
    Configuration config,
    ModDataEditor dataEditor,
    MetaFileManager metaFileManager,
    GamePathParser gamePathParser,
    ImcChecker imcChecker) : IService
{
    public readonly Configuration Config = config;

    /// <summary> Creates directory and files necessary for a new mod without adding it to the manager. </summary>
    public DirectoryInfo? CreateEmptyMod(DirectoryInfo basePath, string newName, string description = "")
    {
        try
        {
            var newDir = CreateModFolder(basePath, newName, Config.ReplaceNonAsciiOnImport, true);
            dataEditor.CreateMeta(newDir, newName, Config.DefaultModAuthor, description, "1.0", string.Empty);
            CreateDefaultFiles(newDir);
            return newDir;
        }
        catch (Exception e)
        {
            Penumbra.Messager.NotificationMessage(e, $"Could not create directory for new Mod {newName}.", NotificationType.Error, false);
            return null;
        }
    }

    /// <summary> Load a mod by its directory. </summary>
    public Mod? LoadMod(DirectoryInfo modPath, bool incorporateMetaChanges, bool deleteDefaultMetaChanges)
    {
        modPath.Refresh();
        if (!modPath.Exists)
        {
            Penumbra.Log.Error($"Supplied mod directory {modPath} does not exist.");
            return null;
        }

        var mod = new Mod(modPath);
        if (ReloadMod(mod, incorporateMetaChanges, deleteDefaultMetaChanges, out _))
            return mod;

        // Can not be base path not existing because that is checked before.
        Penumbra.Log.Warning($"Mod at {modPath} without name is not supported.");
        return null;
    }

    /// <summary> Reload a mod from its mod path. </summary>
    public bool ReloadMod(Mod mod, bool incorporateMetaChanges, bool deleteDefaultMetaChanges, out ModDataChangeType modDataChange)
    {
        modDataChange = ModDataChangeType.Deletion;
        if (!Directory.Exists(mod.ModPath.FullName))
            return false;

        modDataChange = dataEditor.LoadMeta(this, mod);
        if (modDataChange.HasFlag(ModDataChangeType.Deletion) || mod.Name.Length == 0)
            return false;

        dataEditor.LoadLocalData(mod);
        LoadDefaultOption(mod);
        LoadAllGroups(mod);
        if (incorporateMetaChanges)
            IncorporateAllMetaChanges(mod, true);
        if (deleteDefaultMetaChanges && !Config.KeepDefaultMetaChanges)
        {
            foreach (var container in mod.AllDataContainers)
            {
                if (ModMetaEditor.DeleteDefaultValues(metaFileManager, imcChecker, container.Manipulations))
                    saveService.ImmediateSaveSync(new ModSaveGroup(container, Config.ReplaceNonAsciiOnImport));
            }
        }

        return true;
    }

    /// <summary> Load all option groups for a given mod. </summary>
    public void LoadAllGroups(Mod mod)
    {
        mod.Groups.Clear();
        var changes = false;
        foreach (var file in saveService.FileNames.GetOptionGroupFiles(mod))
        {
            var group = LoadModGroup(mod, file);
            if (group != null && mod.Groups.All(g => g.Name != group.Name))
            {
                changes = changes
                 || saveService.FileNames.OptionGroupFile(mod.ModPath.FullName, mod.Groups.Count, group.Name, true)
                 != Path.Combine(file.DirectoryName!, ReplaceBadXivSymbols(file.Name, true));
                mod.Groups.Add(group);
            }
            else
            {
                changes = true;
            }
        }

        if (changes)
            saveService.SaveAllOptionGroups(mod, true, Config.ReplaceNonAsciiOnImport);
    }

    /// <summary> Load the default option for a given mod.</summary>
    public void LoadDefaultOption(Mod mod)
    {
        var defaultFile = saveService.FileNames.OptionGroupFile(mod, -1, Config.ReplaceNonAsciiOnImport);
        try
        {
            var jObject = File.Exists(defaultFile) ? JObject.Parse(File.ReadAllText(defaultFile)) : new JObject();
            SubMod.LoadDataContainer(jObject, mod.Default, mod.ModPath);
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
    public static DirectoryInfo CreateModFolder(DirectoryInfo outDirectory, string modListName, bool onlyAscii, bool create)
    {
        var name = modListName;
        if (name.Length == 0)
            name = "_";

        var newModFolderBase = NewOptionDirectory(outDirectory, name, onlyAscii);
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
        foreach (var subMod in mod.AllDataContainers)
        {
            var (localChanges, localDeleteList) =  IncorporateMetaChanges(subMod, mod.ModPath, false, true);
            changes                             |= localChanges;
            if (delete)
                deleteList.AddRange(localDeleteList);
        }

        DeleteDeleteList(deleteList, delete);

        if (!changes)
            return;

        saveService.SaveAllOptionGroups(mod, false, Config.ReplaceNonAsciiOnImport);
        saveService.ImmediateSaveSync(new ModSaveGroup(mod.ModPath, mod.Default, Config.ReplaceNonAsciiOnImport));
    }


    /// <summary>
    /// If .meta or .rgsp files are encountered, parse them and incorporate their meta changes into the mod.
    /// If delete is true, the files are deleted afterwards.
    /// </summary>
    public (bool Changes, List<string> DeleteList) IncorporateMetaChanges(IModDataContainer option, DirectoryInfo basePath, bool delete, bool deleteDefault)
    {
        var deleteList   = new List<string>();
        var oldSize      = option.Manipulations.Count;
        var deleteString = delete ? "with deletion." : "without deletion.";
        foreach (var (key, file) in option.Files.ToList())
        {
            var ext1 = key.Extension().AsciiToLower().ToString();
            var ext2 = file.Extension.ToLowerInvariant();
            try
            {
                if (ext1 == ".meta" || ext2 == ".meta")
                {
                    option.Files.Remove(key);
                    if (!file.Exists)
                        continue;

                    var meta = new TexToolsMeta(metaFileManager, gamePathParser, File.ReadAllBytes(file.FullName),
                        Config.KeepDefaultMetaChanges);
                    Penumbra.Log.Verbose(
                        $"Incorporating {file} as Metadata file of {meta.MetaManipulations.Count} manipulations {deleteString}");
                    deleteList.Add(file.FullName);
                    option.Manipulations.UnionWith(meta.MetaManipulations);
                }
                else if (ext1 == ".rgsp" || ext2 == ".rgsp")
                {
                    option.Files.Remove(key);
                    if (!file.Exists)
                        continue;

                    var rgsp = TexToolsMeta.FromRgspFile(metaFileManager, file.FullName, File.ReadAllBytes(file.FullName),
                        Config.KeepDefaultMetaChanges);
                    Penumbra.Log.Verbose(
                        $"Incorporating {file} as racial scaling file of {rgsp.MetaManipulations.Count} manipulations {deleteString}");
                    deleteList.Add(file.FullName);

                    option.Manipulations.UnionWith(rgsp.MetaManipulations);
                }
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not incorporate meta changes in mod {basePath} from file {file.FullName}:\n{e}");
            }
        }

        DeleteDeleteList(deleteList, delete);
        var changes = oldSize < option.Manipulations.Count;
        if (deleteDefault && !Config.KeepDefaultMetaChanges)
            changes |= ModMetaEditor.DeleteDefaultValues(metaFileManager, imcChecker, option.Manipulations);

        return (changes, deleteList);
    }

    /// <summary>
    /// Create the name for a group or option subfolder based on its parent folder and given name.
    /// subFolderName should never be empty, and the result is unique and contains no invalid symbols.
    /// </summary>
    public static DirectoryInfo? NewSubFolderName(DirectoryInfo parentFolder, string subFolderName, bool onlyAscii)
    {
        var newModFolderBase = NewOptionDirectory(parentFolder, subFolderName, onlyAscii);
        var newModFolder     = newModFolderBase.FullName.ObtainUniqueFile();
        return newModFolder.Length == 0 ? null : new DirectoryInfo(newModFolder);
    }

    /// <summary> Create a file for an option group from given data. </summary>
    public void CreateOptionGroup(DirectoryInfo baseFolder, GroupType type, string name,
        ModPriority priority, int index, Setting defaultSettings, string desc, IEnumerable<MultiSubMod> subMods)
    {
        switch (type)
        {
            case GroupType.Multi:
            {
                var group = MultiModGroup.WithoutMod(name);
                group.Description     = desc;
                group.Priority        = priority;
                group.DefaultSettings = defaultSettings;
                group.OptionData.AddRange(subMods.Select(s => s.Clone(group)));
                saveService.ImmediateSaveSync(ModSaveGroup.WithoutMod(baseFolder, group, index, Config.ReplaceNonAsciiOnImport));
                break;
            }
            case GroupType.Single:
            {
                var group = SingleModGroup.CreateForSaving(name);
                group.Description     = desc;
                group.Priority        = priority;
                group.DefaultSettings = defaultSettings;
                group.OptionData.AddRange(subMods.Select(s => s.ConvertToSingle(group)));
                saveService.ImmediateSaveSync(ModSaveGroup.WithoutMod(baseFolder, group, index, Config.ReplaceNonAsciiOnImport));
                break;
            }
        }
    }

    /// <summary> Create the data for a given sub mod from its data and the folder it is based on. </summary>
    public MultiSubMod CreateSubMod(DirectoryInfo baseFolder, DirectoryInfo optionFolder, OptionList option, ModPriority priority)
    {
        var list = optionFolder.EnumerateNonHiddenFiles()
            .Select(f => (Utf8GamePath.FromFile(f, optionFolder, out var gamePath), gamePath, new FullPath(f)))
            .Where(t => t.Item1);

        var mod = MultiSubMod.WithoutGroup(option.Name, option.Description, priority);
        foreach (var (_, gamePath, file) in list)
            mod.Files.TryAdd(gamePath, file);

        IncorporateMetaChanges(mod, baseFolder, true, true);

        return mod;
    }

    /// <summary>
    /// Create the default data file from all unused files that were not handled before
    /// and are used in sub mods.
    /// </summary>
    internal void CreateDefaultFiles(DirectoryInfo directory)
    {
        var mod = new Mod(directory);
        ReloadMod(mod, false, false, out _);
        foreach (var file in mod.FindUnusedFiles())
        {
            if (Utf8GamePath.FromFile(new FileInfo(file.FullName), directory, out var gamePath))
                mod.Default.Files.TryAdd(gamePath, file);
        }

        IncorporateMetaChanges(mod.Default, directory, true, true);
        saveService.ImmediateSaveSync(new ModSaveGroup(mod.ModPath, mod.Default, Config.ReplaceNonAsciiOnImport));
    }

    /// <summary> Return the name of a new valid directory based on the base directory and the given name. </summary>
    public static DirectoryInfo NewOptionDirectory(DirectoryInfo baseDir, string optionName, bool onlyAscii)
    {
        var option = ReplaceBadXivSymbols(optionName, onlyAscii);
        return new DirectoryInfo(Path.Combine(baseDir.FullName, option.Length > 0 ? option : "_"));
    }

    /// <summary> Normalize for nicer names, and remove invalid symbols or invalid paths. </summary>
    public static string ReplaceBadXivSymbols(string s, bool onlyAscii, string replacement = "_")
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
            else if (onlyAscii && c.IsInvalidAscii())
                sb.Append(replacement);
            else
                sb.Append(c);
        }

        return sb.ToString().Trim();
    }

    public void SplitMultiGroups(DirectoryInfo baseDir)
    {
        var mod = new Mod(baseDir);

        var files   = saveService.FileNames.GetOptionGroupFiles(mod).ToList();
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
                    using var j = new JsonTextWriter(oldFile);
                    j.Formatting = Formatting.Indented;
                    json.WriteTo(j);
                }

                Penumbra.Log.Debug(
                    $"Writing the remaining {options.Count - IModGroup.MaxMultiOptions} options to {Path.GetFileName(newPath)} after split.");
                using (var newFile = File.CreateText(newPath))
                {
                    using var j = new JsonTextWriter(newFile);
                    j.Formatting = Formatting.Indented;
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
    private static IModGroup? LoadModGroup(Mod mod, FileInfo file)
    {
        if (!File.Exists(file.FullName))
            return null;

        try
        {
            var json = JObject.Parse(File.ReadAllText(file.FullName));
            switch (json[nameof(Type)]?.ToObject<GroupType>() ?? GroupType.Single)
            {
                case GroupType.Multi:  return MultiModGroup.Load(mod, json);
                case GroupType.Single: return SingleModGroup.Load(mod, json);
                case GroupType.Imc:    return ImcModGroup.Load(mod, json);
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not read mod group from {file.FullName}:\n{e}");
        }

        return null;
    }

    internal static void DeleteDeleteList(IEnumerable<string> deleteList, bool delete)
    {
        if (!delete)
            return;

        foreach (var file in deleteList)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not delete incorporated meta file {file}:\n{e}");
            }
        }
    }
}
