using Dalamud.Plugin;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Mods;

namespace Penumbra.Services;

public class FilenameService(IDalamudPluginInterface pi) : IService
{
    public readonly string ConfigDirectory       = pi.ConfigDirectory.FullName;
    public readonly string CollectionDirectory   = Path.Combine(pi.ConfigDirectory.FullName, "collections");
    public readonly string LocalDataDirectory    = Path.Combine(pi.ConfigDirectory.FullName, "mod_data");
    public readonly string ConfigFile            = pi.ConfigFile.FullName;
    public readonly string EphemeralConfigFile   = Path.Combine(pi.ConfigDirectory.FullName, "ephemeral_config.json");
    public readonly string FilesystemFile        = Path.Combine(pi.ConfigDirectory.FullName, "sort_order.json");
    public readonly string ActiveCollectionsFile = Path.Combine(pi.ConfigDirectory.FullName, "active_collections.json");
    public readonly string PredefinedTagFile     = Path.Combine(pi.ConfigDirectory.FullName, "predefined_tags.json");

    public readonly string CrashHandlerExe =
        Path.Combine(pi.AssemblyLocation.DirectoryName!, "Penumbra.CrashHandler.exe");

    public readonly string LogFileName =
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(pi.ConfigDirectory.FullName)!)!, "Penumbra.log");

    /// <summary> Obtain the path of a collection file given its name.</summary>
    public string CollectionFile(ModCollection collection)
        => CollectionFile(collection.Identifier);

    /// <summary> Obtain the path of a collection file given its name. </summary>
    public string CollectionFile(string collectionName)
        => Path.Combine(CollectionDirectory, $"{collectionName}.json");

    /// <summary> Obtain the path of the local data file given a mod directory. Returns an empty string if the mod is temporary. </summary>
    public string LocalDataFile(Mod mod)
        => LocalDataFile(mod.ModPath.FullName);

    /// <summary> Obtain the path of the local data file given a mod directory. </summary>
    public string LocalDataFile(string modDirectory)
        => Path.Combine(LocalDataDirectory, $"{Path.GetFileName(modDirectory)}.json");

    /// <summary> Enumerate all collection files. </summary>
    public IEnumerable<FileInfo> CollectionFiles
    {
        get
        {
            var directory = new DirectoryInfo(CollectionDirectory);
            return directory.Exists ? directory.EnumerateFiles("*.json") : [];
        }
    }

    /// <summary> Enumerate all local data files. </summary>
    public IEnumerable<FileInfo> LocalDataFiles
    {
        get
        {
            var directory = new DirectoryInfo(LocalDataDirectory);
            return directory.Exists ? directory.EnumerateFiles("*.json") : [];
        }
    }

    /// <summary> Obtain the path of the meta file for a given mod. Returns an empty string if the mod is temporary. </summary>
    public string ModMetaPath(Mod mod)
        => ModMetaPath(mod.ModPath.FullName);

    /// <summary> Obtain the path of the meta file given a mod directory. </summary>
    public string ModMetaPath(string modDirectory)
        => Path.Combine(modDirectory, "meta.json");

    /// <summary> Obtain the path of the file describing a given option group by its index and the mod. If the index is < 0, return the path for the default mod file. </summary>
    public string OptionGroupFile(Mod mod, int index, bool onlyAscii)
        => OptionGroupFile(mod.ModPath.FullName, index, index >= 0 ? mod.Groups[index].Name : string.Empty, onlyAscii);

    /// <summary> Obtain the path of the file describing a given option group by its index, name and basepath. If the index is < 0, return the path for the default mod file. </summary>
    public string OptionGroupFile(string basePath, int index, string name, bool onlyAscii)
    {
        var fileName = index >= 0
            ? $"group_{index + 1:D3}_{ModCreator.ReplaceBadXivSymbols(name.ToLowerInvariant(), onlyAscii)}.json"
            : "default_mod.json";
        return Path.Combine(basePath, fileName);
    }

    /// <summary> Enumerate all group files for a given mod. </summary>
    public IEnumerable<FileInfo> GetOptionGroupFiles(Mod mod)
        => mod.ModPath.EnumerateFiles("group_*.json");
}
