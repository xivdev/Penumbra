using Dalamud.Plugin;
using Luna;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.Files;

public sealed class FilenameService(IDalamudPluginInterface pi, LocalModDatabase database) : BaseFilePathProvider(pi)
{
    public readonly struct MigrationPaths(string configDirectory)
    {
        public readonly string Ephemeral          = Path.Combine(configDirectory, "ephemeral_config.json");
        public readonly string FileSystem         = Path.Combine(configDirectory, "sort_order.json");
        public readonly string LocalDataDirectory = Path.Combine(configDirectory, "mod_data");
        public readonly string UiConfigFile       = Path.Combine(configDirectory, "ui_config.json");
        public readonly string FilterFile         = Path.Combine(configDirectory, "filters.json");

        /// <summary> Obtain the path of the file describing a given option group by its index and the mod. If the index is less than 0, return the path for the default mod file. </summary>
        public string OptionGroupFile(Mod mod, int index, bool onlyAscii)
            => OptionGroupFile(mod.ModPath.FullName, index, index >= 0 ? mod.Groups[index].Name : string.Empty, onlyAscii);

        /// <summary> Obtain the path of the file describing a given option group by its index, name and basepath. If the index is less than 0, return the path for the default mod file. </summary>
        public string OptionGroupFile(string basePath, int index, string name, bool onlyAscii)
        {
            var fileName = index >= 0
                ? $"group_{index + 1:D3}_{name.ToLowerInvariant().ReplaceBadXivSymbols(onlyAscii)}.json"
                : "default_mod.json";
            return Path.Combine(basePath, fileName);
        }

        /// <summary> Enumerate all group files for a given mod. </summary>
        public IEnumerable<FileInfo> GetOptionGroupFiles(Mod mod)
            => mod.ModPath.EnumerateFiles("group_*.json");

        /// <summary> Enumerate all outdated local data files. </summary>
        public IEnumerable<string> OldLocalDataFiles
            => !Directory.Exists(LocalDataDirectory) ? [] : Directory.EnumerateFiles(LocalDataDirectory, "*.json");
    }

    public readonly struct FileSystemPaths(string fileSystemFolder)
    {
        public readonly string Folder                = fileSystemFolder;
        public readonly string LockedNodes           = Path.Combine(fileSystemFolder, "locked_nodes.json");
        public readonly string Organization          = Path.Combine(fileSystemFolder, "organization.json");
        public readonly string ExpandedFolders       = Path.Combine(fileSystemFolder, "expanded_folders.json");
        public readonly string SelectedNodes         = Path.Combine(fileSystemFolder, "selected_nodes.json");
        public readonly string EmptyFoldersMigration = Path.Combine(fileSystemFolder, "empty_folders.json");
    }

    public readonly struct ConfigPaths(string configFolder)
    {
        public readonly string Folder   = configFolder;
        public readonly string Main     = Path.Combine(configFolder, "penumbra.json");
        public readonly string Ui       = Path.Combine(configFolder, "ui.json");
        public readonly string Io       = Path.Combine(configFolder, "io.json");
        public readonly string Editing  = Path.Combine(configFolder, "editing.json");
        public readonly string Behavior = Path.Combine(configFolder, "behavior.json");
        public readonly string Advanced = Path.Combine(configFolder, "advanced.json");

        public readonly string Filters   = Path.Combine(configFolder, "filters.json");
        public readonly string Ephemeral = Path.Combine(configFolder, "ephemeral.json");
    }

    public readonly FileSystemPaths FileSystem            = new(Path.Combine(pi.ConfigDirectory.FullName, "mod_filesystem"));
    public readonly ConfigPaths     Config                = new(Path.Combine(pi.ConfigDirectory.FullName, "config"));
    public readonly MigrationPaths  Migration             = new(pi.ConfigDirectory.FullName);
    public readonly string          CollectionDirectory   = Path.Combine(pi.ConfigDirectory.FullName, "collections");
    public readonly string          LocalModDatabase      = Path.Combine(pi.ConfigDirectory.FullName, "mod_data.db");
    public readonly string          ActiveCollectionsFile = Path.Combine(pi.ConfigDirectory.FullName, "active_collections.json");
    public readonly string          PredefinedTagFile     = Path.Combine(pi.ConfigDirectory.FullName, "predefined_tags.json");
    public readonly string          ManagementLog         = Path.Combine(pi.ConfigDirectory.FullName, "management.log");
    public readonly string          CrashHandlerExe       = Path.Combine(pi.AssemblyLocation.DirectoryName!, "Penumbra.CrashHandler.exe");
    public readonly string          LogFileName           = Path.Combine(pi.ConfigDirectory.Parent!.Parent!.FullName, "Penumbra.log");

    /// <summary> Obtain the path of a collection file given its name.</summary>
    public string CollectionFile(ModCollection collection)
        => CollectionFile(collection.Identity.Identifier);

    /// <summary> Obtain the path of a collection file given its name. </summary>
    public string CollectionFile(string collectionName)
        => Path.Combine(CollectionDirectory, $"{collectionName}.json");

    /// <summary> Enumerate all collection files. </summary>
    public IEnumerable<string> CollectionFiles
        => !Directory.Exists(CollectionDirectory) ? [] : Directory.EnumerateFiles(CollectionDirectory, "*.json");

    /// <summary> Obtain the path of the meta file for a given mod. Returns an empty string if the mod is temporary. </summary>
    public string ModMetaPath(Mod mod)
        => ModMetaPath(mod.ModPath.FullName);

    /// <summary> Obtain the path of the meta file given a mod directory. </summary>
    public string ModMetaPath(string modDirectory)
        => Path.Combine(modDirectory, "meta.json");

    /// <summary> Collect all relevant files for penumbra configuration. </summary>
    public override List<IBackupFile> GetBackupFiles()
    {
        var list = CollectionFiles.Select(IBackupFile (f) => new DefaultBackupFile(f)).ToList();
        list.Add(database.CreateBackupFile(LocalModDatabase));
        list.Add(new DefaultBackupFile(ActiveCollectionsFile));
        list.Add(new DefaultBackupFile(PredefinedTagFile));

        list.Add(new DefaultBackupFile(Config.Main));
        list.Add(new DefaultBackupFile(Config.Ui));
        list.Add(new DefaultBackupFile(Config.Io));
        list.Add(new DefaultBackupFile(Config.Editing));
        list.Add(new DefaultBackupFile(Config.Behavior));
        list.Add(new DefaultBackupFile(Config.Advanced));

        list.Add(new DefaultBackupFile(FileSystem.LockedNodes));
        list.Add(new DefaultBackupFile(FileSystem.Organization));
        // Do not back up expanded folders, selected nodes, ui configuration or ephemeral config.
        return list;
    }
}
