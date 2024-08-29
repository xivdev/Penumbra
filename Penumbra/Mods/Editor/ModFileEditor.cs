using OtterGui.Services;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Editor;

public class ModFileEditor(ModFileCollection files, ModManager modManager, CommunicatorService communicator) : IService
{
    public bool Changes { get; private set; }

    public void Clear()
    {
        Changes = false;
    }

    public int Apply(Mod mod, IModDataContainer option)
    {
        var dict = new Dictionary<Utf8GamePath, FullPath>();
        var num  = 0;
        foreach (var file in files.Available)
        {
            foreach (var path in file.SubModUsage.Where(p => p.Item1 == option))
                num += dict.TryAdd(path.Item2, file.File) ? 0 : 1;
        }

        modManager.OptionEditor.SetFiles(option, dict);
        files.UpdatePaths(mod, option);
        Changes = false;
        return num;
    }

    public void Revert(Mod mod, IModDataContainer option)
    {
        files.UpdateAll(mod, option);
        Changes = false;
    }

    /// <summary> Remove all path redirections where the pointed-to file does not exist. </summary>
    public void RemoveMissingPaths(Mod mod, IModDataContainer option)
    {
        void HandleSubMod(IModDataContainer subMod)
        {
            var newDict = subMod.Files.Where(kvp => CheckAgainstMissing(mod, subMod, kvp.Value, kvp.Key, subMod == option))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            if (newDict.Count != subMod.Files.Count)
                modManager.OptionEditor.SetFiles(subMod, newDict);
        }

        ModEditor.ApplyToAllContainers(mod, HandleSubMod);
        files.ClearMissingFiles();
    }

    /// <summary> Return whether the given path is already used in the current option. </summary>
    public bool CanAddGamePath(Utf8GamePath path)
        => !files.UsedPaths.Contains(path);

    /// <summary>
    /// Try to set a given path for a given file.
    /// Returns false if this is not possible.
    /// If path is empty, it    will be deleted instead.
    /// If pathIdx is equal to the  total number of paths, path will be added, otherwise replaced.
    /// </summary>
    public bool SetGamePath(IModDataContainer option, int fileIdx, int pathIdx, Utf8GamePath path)
    {
        if (!CanAddGamePath(path) || fileIdx < 0 || fileIdx > files.Available.Count)
            return false;

        var registry = files.Available[fileIdx];
        if (pathIdx > registry.SubModUsage.Count)
            return false;

        if ((pathIdx == -1 || pathIdx == registry.SubModUsage.Count) && !path.IsEmpty)
            files.AddUsedPath(option, registry, path);
        else
            files.ChangeUsedPath(registry, pathIdx, path);

        Changes = true;

        return true;
    }

    /// <summary>
    /// Transform a set of files to the appropriate game paths with the given number of folders skipped,
    /// and add them to the given option.
    /// </summary>
    public int AddPathsToSelected(IModDataContainer option, IEnumerable<FileRegistry> files1, int skipFolders = 0)
    {
        var failed = 0;
        foreach (var file in files1)
        {
            var gamePath = file.RelPath.ToGamePath(skipFolders);
            if (gamePath.IsEmpty)
            {
                ++failed;
                continue;
            }

            if (CanAddGamePath(gamePath))
            {
                files.AddUsedPath(option, file, gamePath);
                Changes = true;
            }
            else
            {
                ++failed;
            }
        }

        return failed;
    }

    /// <summary> Remove all paths in the current option from the given files. </summary>
    public void RemovePathsFromSelected(IModDataContainer option, IEnumerable<FileRegistry> files1)
    {
        foreach (var file in files1)
        {
            for (var i = 0; i < file.SubModUsage.Count; ++i)
            {
                var (opt, path) = file.SubModUsage[i];
                if (option != opt)
                    continue;

                files.RemoveUsedPath(option, file, path);
                Changes = true;
                --i;
            }
        }
    }

    /// <summary> Delete all given files from your filesystem </summary>
    public void DeleteFiles(Mod mod, IModDataContainer option, IEnumerable<FileRegistry> files1)
    {
        var deletions = 0;
        foreach (var file in files1)
        {
            try
            {
                File.Delete(file.File.FullName);
                communicator.ModFileChanged.Invoke(mod, file);
                Penumbra.Log.Debug($"[DeleteFiles] Deleted {file.File.FullName} from {mod.Name}.");
                ++deletions;
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"[DeleteFiles] Could not delete {file.File.FullName} from {mod.Name}:\n{e}");
            }
        }

        if (deletions <= 0)
            return;

        modManager.Creator.ReloadMod(mod, false, false, out _);
        files.UpdateAll(mod, option);
    }


    private bool CheckAgainstMissing(Mod mod, IModDataContainer option, FullPath file, Utf8GamePath key, bool removeUsed)
    {
        if (!files.Missing.Contains(file))
            return true;

        if (removeUsed)
            files.RemoveUsedPath(option, file, key);

        Penumbra.Log.Debug($"[RemoveMissingPaths] Removing {key} -> {file} from {mod.Name}.");
        return false;
    }
}
