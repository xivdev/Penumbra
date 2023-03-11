using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Plugin;
using OtterGui.Filesystem;

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

    public string CollectionFile(string collectionName)
        => Path.Combine(CollectionDirectory, $"{collectionName.RemoveInvalidPathSymbols()}.json");

    public string LocalDataFile(string modPath)
        => Path.Combine(LocalDataDirectory, $"{modPath}.json");

    public IEnumerable<FileInfo> CollectionFiles
    {
        get
        {
            var directory = new DirectoryInfo(CollectionDirectory);
            return directory.Exists ? directory.EnumerateFiles("*.json") : Array.Empty<FileInfo>();
        }
    }

    public IEnumerable<FileInfo> LocalDataFiles
    {
        get
        {
            var directory = new DirectoryInfo(LocalDataDirectory);
            return directory.Exists ? directory.EnumerateFiles("*.json") : Array.Empty<FileInfo>();
        }
    }
}
