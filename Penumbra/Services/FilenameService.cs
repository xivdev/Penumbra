using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Plugin;
using OtterGui.Filesystem;
using Penumbra.Collections;
using Penumbra.Mods;

namespace Penumbra.Services;

public class FilenameService
{
    public readonly string ConfigDirectory;
    public readonly string CollectionDirectory;
    public readonly string LocalDataDirectory;
    public readonly string ConfigFile;
    public readonly string FilesystemFile;
    public readonly string ActiveCollectionsFile;

    public FilenameService(DalamudPluginInterface pi)
    {
        ConfigDirectory       = pi.ConfigDirectory.FullName;
        CollectionDirectory   = Path.Combine(pi.GetPluginConfigDirectory(), "collections");
        LocalDataDirectory    = Path.Combine(pi.ConfigDirectory.FullName,   "mod_data");
        ConfigFile            = pi.ConfigFile.FullName;
        FilesystemFile        = Path.Combine(pi.GetPluginConfigDirectory(), "sort_order.json");
        ActiveCollectionsFile = Path.Combine(pi.ConfigDirectory.FullName,   "active_collections.json");
    }

    /// <summary> Obtain the path of a collection file given its name. Returns an empty string if the collection is temporary. </summary>
    public string CollectionFile(ModCollection collection)
        => collection.Index >= 0 ? Path.Combine(CollectionDirectory, $"{collection.Name.RemoveInvalidPathSymbols()}.json") : string.Empty;

    /// <summary> Obtain the path of a collection file given its name. </summary>
    public string CollectionFile(string collectionName)
        => Path.Combine(CollectionDirectory, $"{collectionName.RemoveInvalidPathSymbols()}.json");


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
            return directory.Exists ? directory.EnumerateFiles("*.json") : Array.Empty<FileInfo>();
        }
    }

    /// <summary> Enumerate all local data files. </summary>
    public IEnumerable<FileInfo> LocalDataFiles
    {
        get
        {
            var directory = new DirectoryInfo(LocalDataDirectory);
            return directory.Exists ? directory.EnumerateFiles("*.json") : Array.Empty<FileInfo>();
        }
    }

    /// <summary> Obtain the path of the meta file for a given mod. Returns an empty string if the mod is temporary. </summary>
    public string ModMetaPath(Mod mod)
        => ModMetaPath(mod.ModPath.FullName);

    /// <summary> Obtain the path of the meta file given a mod directory. </summary>
    public string ModMetaPath(string modDirectory)
        => Path.Combine(modDirectory, "meta.json");

    /// <summary> Obtain the path of the file describing a given option group by its index and the mod. If the index is < 0, return the path for the default mod file. </summary>
    public string OptionGroupFile(Mod mod, int index)
        => OptionGroupFile(mod.ModPath.FullName, index, index >= 0 ? mod.Groups[index].Name : string.Empty);

    /// <summary> Obtain the path of the file describing a given option group by its index, name and basepath. If the index is < 0, return the path for the default mod file. </summary>
    public string OptionGroupFile(string basePath, int index, string name)
    {
        var fileName = index >= 0
            ? $"group_{index + 1:D3}_{name.RemoveInvalidPathSymbols().ToLowerInvariant()}.json"
            : "default_mod.json";
        return Path.Combine(basePath, fileName);
    }

    /// <summary> Enumerate all group files for a given mod. </summary>
    public IEnumerable<FileInfo> GetOptionGroupFiles(Mod mod)
        => mod.ModPath.EnumerateFiles("group_*.json");
}
