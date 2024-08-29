using OtterGui.Services;
using Penumbra.Communication;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

/// <summary> Describes the state of a potential move-target for a mod. </summary>
public enum NewDirectoryState
{
    NonExisting,
    ExistsEmpty,
    ExistsNonEmpty,
    ExistsAsFile,
    ContainsInvalidSymbols,
    Identical,
    Empty,
}

/// <summary> Describes the state of a changed mod event. </summary>
public enum ModPathChangeType
{
    Added,
    Deleted,
    Moved,
    Reloaded,
    StartingReload,
}

public sealed class ModManager : ModStorage, IDisposable, IService
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;

    public readonly ModCreator     Creator;
    public readonly ModDataEditor  DataEditor;
    public readonly ModGroupEditor OptionEditor;

    public DirectoryInfo BasePath { get; private set; } = null!;
    public bool          Valid    { get; private set; }

    public ModManager(Configuration config, CommunicatorService communicator, ModDataEditor dataEditor, ModGroupEditor optionEditor,
        ModCreator creator)
    {
        _config       = config;
        _communicator = communicator;
        DataEditor    = dataEditor;
        OptionEditor  = optionEditor;
        Creator       = creator;
        SetBaseDirectory(config.ModDirectory, true, out _);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModManager);
        DiscoverMods();
    }

    /// <summary> Change the mod base directory and discover available mods. </summary>
    public void DiscoverMods(string newDir, out string resultNewDir)
    {
        SetBaseDirectory(newDir, false, out resultNewDir);
        DiscoverMods();
    }

    /// <summary>
    /// Discover mods without changing the root directory.
    /// </summary>
    public void DiscoverMods()
    {
        _communicator.ModDiscoveryStarted.Invoke();
        ClearNewMods();
        Mods.Clear();
        BasePath.Refresh();

        if (Valid && BasePath.Exists)
            ScanMods();

        _communicator.ModDiscoveryFinished.Invoke();
        Penumbra.Log.Information($"Rediscovered {Mods.Count} mods.");

        if (ModBackup.MigrateModBackups)
            ModBackup.MigrateZipToPmp(this);
    }

    /// <summary> Load a new mod and add it to the manager if successful. </summary>
    public void AddMod(DirectoryInfo modFolder, bool deleteDefaultMeta)
    {
        if (this.Any(m => m.ModPath.Name == modFolder.Name))
            return;

        Creator.SplitMultiGroups(modFolder);
        var mod = Creator.LoadMod(modFolder, true, deleteDefaultMeta);
        if (mod == null)
            return;

        mod.Index = Count;
        Mods.Add(mod);
        _communicator.ModPathChanged.Invoke(ModPathChangeType.Added, mod, null, mod.ModPath);
        Penumbra.Log.Debug($"Added new mod {mod.Name} from {modFolder.FullName}.");
    }

    /// <summary>
    /// Delete a mod. The event is invoked before the mod is removed from the list.
    /// Deletes from filesystem as well as from internal data.
    /// Updates indices of later mods.
    /// </summary>
    public void DeleteMod(Mod mod)
    {
        if (Directory.Exists(mod.ModPath.FullName))
            try
            {
                Directory.Delete(mod.ModPath.FullName, true);
                Penumbra.Log.Debug($"Deleted directory {mod.ModPath.FullName} for {mod.Name}.");
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not delete the mod {mod.ModPath.Name}:\n{e}");
            }

        RemoveMod(mod);
    }

    /// <summary>
    /// Remove a loaded mod. The event is invoked before the mod is removed from the list.
    /// Does not delete the mod from the filesystem.
    /// Updates indices of later mods.
    /// </summary>
    public void RemoveMod(Mod mod)
    {
        _communicator.ModPathChanged.Invoke(ModPathChangeType.Deleted, mod, mod.ModPath, null);
        foreach (var remainingMod in Mods.Skip(mod.Index + 1))
            --remainingMod.Index;
        Mods.RemoveAt(mod.Index);
        Penumbra.Log.Debug($"Removed loaded mod {mod.Name} from list.");
    }

    /// <summary>
    /// Reload a mod without changing its base directory.
    /// If the base directory does not exist anymore, the mod will be deleted.
    /// </summary>
    public void ReloadMod(Mod mod)
    {
        var oldName = mod.Name;

        _communicator.ModPathChanged.Invoke(ModPathChangeType.StartingReload, mod, mod.ModPath, mod.ModPath);
        if (!Creator.ReloadMod(mod, true, false, out var metaChange))
        {
            Penumbra.Log.Warning(mod.Name.Length == 0
                ? $"Reloading mod {oldName} has failed, new name is empty. Removing from loaded mods instead."
                : $"Reloading mod {oldName} failed, {mod.ModPath.FullName} does not exist anymore or it has invalid data. Removing from loaded mods instead.");
            RemoveMod(mod);
            return;
        }

        _communicator.ModPathChanged.Invoke(ModPathChangeType.Reloaded, mod, mod.ModPath, mod.ModPath);
        if (metaChange != ModDataChangeType.None)
            _communicator.ModDataChanged.Invoke(metaChange, mod, oldName);
    }


    /// <summary>
    /// Rename/Move a mod directory.
    /// Updates all collection settings and sort order settings.
    /// </summary>
    public void MoveModDirectory(Mod mod, string newName)
    {
        var oldName      = mod.Name;
        var oldDirectory = mod.ModPath;

        switch (NewDirectoryValid(oldDirectory.Name, newName, out var dir))
        {
            case NewDirectoryState.NonExisting:
                // Nothing to do
                break;
            case NewDirectoryState.ExistsEmpty:
                try
                {
                    Directory.Delete(dir!.FullName);
                }
                catch (Exception e)
                {
                    Penumbra.Log.Error($"Could not delete empty directory {dir!.FullName} to move {mod.Name} to it:\n{e}");
                    return;
                }

                break;
            // Should be caught beforehand.
            case NewDirectoryState.ExistsNonEmpty:
            case NewDirectoryState.ExistsAsFile:
            case NewDirectoryState.ContainsInvalidSymbols:
            // Nothing to do at all.
            case NewDirectoryState.Identical:
            default:
                return;
        }

        try
        {
            Directory.Move(oldDirectory.FullName, dir!.FullName);
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not move {mod.Name} from {oldDirectory.Name} to {dir!.Name}:\n{e}");
            return;
        }

        DataEditor.MoveDataFile(oldDirectory, dir);

        dir.Refresh();
        mod.ModPath = dir;
        if (!Creator.ReloadMod(mod, false, false, out var metaChange))
        {
            Penumbra.Log.Error($"Error reloading moved mod {mod.Name}.");
            return;
        }

        _communicator.ModPathChanged.Invoke(ModPathChangeType.Moved, mod, oldDirectory, dir);
        if (metaChange != ModDataChangeType.None)
            _communicator.ModDataChanged.Invoke(metaChange, mod, oldName);
    }

    /// <summary> Return the state of the new potential name of a directory. </summary>
    public NewDirectoryState NewDirectoryValid(string oldName, string newName, out DirectoryInfo? directory)
    {
        directory = null;
        if (newName.Length == 0)
            return NewDirectoryState.Empty;

        if (oldName == newName)
            return NewDirectoryState.Identical;

        var fixedNewName = ModCreator.ReplaceBadXivSymbols(newName, _config.ReplaceNonAsciiOnImport);
        if (fixedNewName != newName)
            return NewDirectoryState.ContainsInvalidSymbols;

        directory = new DirectoryInfo(Path.Combine(BasePath.FullName, fixedNewName));
        if (File.Exists(directory.FullName))
            return NewDirectoryState.ExistsAsFile;

        if (!Directory.Exists(directory.FullName))
            return NewDirectoryState.NonExisting;

        if (directory.EnumerateFileSystemInfos().Any())
            return NewDirectoryState.ExistsNonEmpty;

        return NewDirectoryState.ExistsEmpty;
    }


    /// <summary> Add new mods to NewMods and remove deleted mods from NewMods. </summary>
    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? oldDirectory,
        DirectoryInfo? newDirectory)
    {
        switch (type)
        {
            case ModPathChangeType.Added:
                SetNew(mod);
                break;
            case ModPathChangeType.Deleted:
                SetKnown(mod);
                break;
            case ModPathChangeType.Moved:
                if (oldDirectory != null && newDirectory != null)
                    DataEditor.MoveDataFile(oldDirectory, newDirectory);

                break;
        }
    }

    public void Dispose()
        => _communicator.ModPathChanged.Unsubscribe(OnModPathChange);

    /// <summary>
    /// Set the mod base directory.
    /// If it's not the first time, check if it is the same directory as before.
    /// Also checks if the directory is available and tries to create it if it is not.
    /// </summary>
    private void SetBaseDirectory(string newPath, bool firstTime, out string resultNewDir)
    {
        resultNewDir = newPath;
        if (!firstTime && string.Equals(newPath, _config.ModDirectory, StringComparison.Ordinal))
            return;

        if (newPath.Length == 0)
        {
            Valid    = false;
            BasePath = new DirectoryInfo(".");
            if (_config.ModDirectory != BasePath.FullName)
                TriggerModDirectoryChange(string.Empty, false);
        }
        else
        {
            var newDir = new DirectoryInfo(Path.TrimEndingDirectorySeparator(newPath));
            if (!newDir.Exists)
                try
                {
                    Directory.CreateDirectory(newDir.FullName);
                    newDir.Refresh();
                }
                catch (Exception e)
                {
                    Penumbra.Log.Error($"Could not create specified mod directory {newDir.FullName}:\n{e}");
                }

            BasePath     = newDir;
            Valid        = Directory.Exists(newDir.FullName);
            resultNewDir = BasePath.FullName;
            if (!firstTime && _config.ModDirectory != BasePath.FullName)
                TriggerModDirectoryChange(BasePath.FullName, Valid);
        }
    }

    private void TriggerModDirectoryChange(string newPath, bool valid)
    {
        _config.ModDirectory = newPath;
        _config.Save();
        Penumbra.Log.Information($"Set new mod base directory from {_config.ModDirectory} to {newPath}.");
        _communicator.ModDirectoryChanged.Invoke(newPath, valid);
    }


    /// <summary>
    /// Iterate through available mods with multiple threads and queue their loads,
    /// then add the mods from the queue.
    /// </summary>
    private void ScanMods()
    {
        try
        {
            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
            };
            var queue = new ConcurrentQueue<Mod>();
            Parallel.ForEach(BasePath.EnumerateDirectories(), options, dir =>
            {
                var mod = Creator.LoadMod(dir, false, false);
                if (mod != null)
                    queue.Enqueue(mod);
            });

            foreach (var mod in queue)
            {
                mod.Index = Count;
                Mods.Add(mod);
            }
        }
        catch (Exception ex)
        {
            Valid = false;
            _communicator.ModDirectoryChanged.Invoke(BasePath.FullName, false);
            Penumbra.Log.Error($"Could not scan for mods:\n{ex}");
        }
    }

    public bool TryIdentifyPath(string path, [NotNullWhen(true)] out Mod? mod, [NotNullWhen(true)] out string? relativePath)
    {
        var relPath = Path.GetRelativePath(BasePath.FullName, path);
        if (relPath != "." && (relPath.StartsWith('.') || Path.IsPathRooted(relPath)))
        {
            mod          = null;
            relativePath = null;
            return false;
        }

        var modDirectorySeparator = relPath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);

        var modDirectory = modDirectorySeparator < 0 ? relPath : relPath[..modDirectorySeparator];
        relativePath = modDirectorySeparator < 0 ? string.Empty : relPath[(modDirectorySeparator + 1)..];

        return TryGetMod(modDirectory, "\0", out mod);
    }
}
