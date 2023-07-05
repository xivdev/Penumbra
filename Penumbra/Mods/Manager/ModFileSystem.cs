using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OtterGui.Filesystem;
using Penumbra.Communication;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.Util;

namespace Penumbra.Mods;

public sealed class ModFileSystem : FileSystem<Mod>, IDisposable, ISavable
{
    private readonly ModManager          _modManager;
    private readonly CommunicatorService _communicator;
    private readonly SaveService         _saveService;

    // Create a new ModFileSystem from the currently loaded mods and the current sort order file.
    public ModFileSystem(ModManager modManager, CommunicatorService communicator, SaveService saveService)
    {
        _modManager   = modManager;
        _communicator = communicator;
        _saveService  = saveService;
        Reload();
        Changed += OnChange;
        _communicator.ModDiscoveryFinished.Subscribe(Reload, ModDiscoveryFinished.Priority.ModFileSystem);
        _communicator.ModDataChanged.Subscribe(OnDataChange, ModDataChanged.Priority.ModFileSystem);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModFileSystem);
    }

    public void Dispose()
    {
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _communicator.ModDiscoveryFinished.Unsubscribe(Reload);
        _communicator.ModDataChanged.Unsubscribe(OnDataChange);
    }

    public struct ImportDate : ISortMode<Mod>
    {
        public string Name
            => "Import Date (Older First)";

        public string Description
            => "In each folder, sort all subfolders lexicographically, then sort all leaves using their import date.";

        public IEnumerable<IPath> GetChildren(Folder f)
            => f.GetSubFolders().Cast<IPath>().Concat(f.GetLeaves().OrderBy(l => l.Value.ImportDate));
    }

    public struct InverseImportDate : ISortMode<Mod>
    {
        public string Name
            => "Import Date (Newer First)";

        public string Description
            => "In each folder, sort all subfolders lexicographically, then sort all leaves using their inverse import date.";

        public IEnumerable<IPath> GetChildren(Folder f)
            => f.GetSubFolders().Cast<IPath>().Concat(f.GetLeaves().OrderByDescending(l => l.Value.ImportDate));
    }

    // Reload the whole filesystem from currently loaded mods and the current sort order file.
    // Used on construction and on mod rediscoveries.
    private void Reload()
    {
        // TODO
        if (Load(new FileInfo(_saveService.FileNames.FilesystemFile), _modManager, ModToIdentifier, ModToName))
            _saveService.ImmediateSave(this);

        Penumbra.Log.Debug("Reloaded mod filesystem.");
    }

    // Save the filesystem on every filesystem change except full reloading.
    private void OnChange(FileSystemChangeType type, IPath _1, IPath? _2, IPath? _3)
    {
        if (type != FileSystemChangeType.Reload)
            _saveService.DelaySave(this);
    }

    // Update sort order when defaulted mod names change.
    private void OnDataChange(ModDataChangeType type, Mod mod, string? oldName)
    {
        if (!type.HasFlag(ModDataChangeType.Name) || oldName == null || !FindLeaf(mod, out var leaf))
            return;

        var old = oldName.FixName();
        if (old == leaf.Name || leaf.Name.IsDuplicateName(out var baseName, out _) && baseName == old)
            RenameWithDuplicates(leaf, mod.Name.Text);
    }

    // Update the filesystem if a mod has been added or removed.
    // Save it, if the mod directory has been moved, since this will change the save format.
    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? oldPath, DirectoryInfo? newPath)
    {
        switch (type)
        {
            case ModPathChangeType.Added:
                CreateDuplicateLeaf(Root, mod.Name.Text, mod);
                break;
            case ModPathChangeType.Deleted:
                if (FindLeaf(mod, out var leaf))
                    Delete(leaf);

                break;
            case ModPathChangeType.Moved:
                _saveService.DelaySave(this);
                break;
            case ModPathChangeType.Reloaded:
                // Nothing
                break;
        }
    }

    // Search the entire filesystem for the leaf corresponding to a mod.
    public bool FindLeaf(Mod mod, [NotNullWhen(true)] out Leaf? leaf)
    {
        leaf = Root.GetAllDescendants(ISortMode<Mod>.Lexicographical)
            .OfType<Leaf>()
            .FirstOrDefault(l => l.Value == mod);
        return leaf != null;
    }


    // Used for saving and loading.
    private static string ModToIdentifier(Mod mod)
        => mod.ModPath.Name;

    private static string ModToName(Mod mod)
        => mod.Name.Text.FixName();

    // Return whether a mod has a custom path or is just a numbered default path.
    public static bool ModHasDefaultPath(Mod mod, string fullPath)
    {
        var regex = new Regex($@"^{Regex.Escape(ModToName(mod))}( \(\d+\))?$");
        return regex.IsMatch(fullPath);
    }

    private static (string, bool) SaveMod(Mod mod, string fullPath)
        // Only save pairs with non-default paths.
        => ModHasDefaultPath(mod, fullPath)
            ? (string.Empty, false)
            : (ModToIdentifier(mod), true);

    public string ToFilename(FilenameService fileNames)
        => fileNames.FilesystemFile;

    public void Save(StreamWriter writer)
        => SaveToFile(writer, SaveMod, true);

    public string TypeName
        => "Mod File System";

    public string LogName(string _)
        => "to file";
}
