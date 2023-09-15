using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

public class ModFileEditor
{
    private readonly ModFileCollection _files;
    private readonly ModManager       _modManager;

    public bool Changes { get; private set; }

    public ModFileEditor(ModFileCollection files, ModManager modManager)
    {
        _files      = files;
        _modManager = modManager;
    }

    public void Clear()
    {
        Changes = false;
    }

    public int Apply(Mod mod, SubMod option)
    {
        var dict = new Dictionary<Utf8GamePath, FullPath>();
        var num  = 0;
        foreach (var file in _files.Available)
        {
            foreach (var path in file.SubModUsage.Where(p => p.Item1 == option))
                num += dict.TryAdd(path.Item2, file.File) ? 0 : 1;
        }

        _modManager.OptionEditor.OptionSetFiles(mod, option.GroupIdx, option.OptionIdx, dict);
        _files.UpdatePaths(mod, option);
        Changes = false;
        return num;
    }

    public void Revert(Mod mod, ISubMod option)
    {
        _files.UpdateAll(mod, option);
        Changes = false;
    }

    /// <summary> Remove all path redirections where the pointed-to file does not exist. </summary>
    public void RemoveMissingPaths(Mod mod, ISubMod option)
    {
        void HandleSubMod(ISubMod subMod, int groupIdx, int optionIdx)
        {
            var newDict = subMod.Files.Where(kvp => CheckAgainstMissing(mod, subMod, kvp.Value, kvp.Key, subMod == option))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            if (newDict.Count != subMod.Files.Count)
                _modManager.OptionEditor.OptionSetFiles(mod, groupIdx, optionIdx, newDict);
        }

        ModEditor.ApplyToAllOptions(mod, HandleSubMod);
        _files.ClearMissingFiles();
    }

    /// <summary> Return whether the given path is already used in the current option. </summary>
    public bool CanAddGamePath(Utf8GamePath path)
        => !_files.UsedPaths.Contains(path);

    /// <summary>
    /// Try to set a given path for a given file.
    /// Returns false if this is not possible.
    /// If path is empty, it    will be deleted instead.
    /// If pathIdx is equal to the  total number of paths, path will be added, otherwise replaced.
    /// </summary>
    public bool SetGamePath(ISubMod option, int fileIdx, int pathIdx, Utf8GamePath path)
    {
        if (!CanAddGamePath(path) || fileIdx < 0 || fileIdx > _files.Available.Count)
            return false;

        var registry = _files.Available[fileIdx];
        if (pathIdx > registry.SubModUsage.Count)
            return false;

        if ((pathIdx == -1 || pathIdx == registry.SubModUsage.Count) && !path.IsEmpty)
            _files.AddUsedPath(option, registry, path);
        else
            _files.ChangeUsedPath(registry, pathIdx, path);

        Changes = true;

        return true;
    }

    /// <summary>
    /// Transform a set of files to the appropriate game paths with the given number of folders skipped,
    /// and add them to the given option.
    /// </summary>
    public int AddPathsToSelected(ISubMod option, IEnumerable<FileRegistry> files, int skipFolders = 0)
    {
        var failed = 0;
        foreach (var file in files)
        {
            var gamePath = file.RelPath.ToGamePath(skipFolders);
            if (gamePath.IsEmpty)
            {
                ++failed;
                continue;
            }

            if (CanAddGamePath(gamePath))
            {
                _files.AddUsedPath(option, file, gamePath);
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
    public void RemovePathsFromSelected(ISubMod option, IEnumerable<FileRegistry> files)
    {
        foreach (var file in files)
        {
            for (var i = 0; i < file.SubModUsage.Count; ++i)
            {
                var (opt, path) = file.SubModUsage[i];
                if (option != opt)
                    continue;

                _files.RemoveUsedPath(option, file, path);
                Changes = true;
                --i;
            }
        }
    }

    /// <summary> Delete all given files from your filesystem </summary>
    public void DeleteFiles(Mod mod, ISubMod option, IEnumerable<FileRegistry> files)
    {
        var deletions = 0;
        foreach (var file in files)
        {
            try
            {
                File.Delete(file.File.FullName);
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

        _modManager.Creator.ReloadMod(mod, false, out _);
        _files.UpdateAll(mod, option);
    }


    private bool CheckAgainstMissing(Mod mod, ISubMod option, FullPath file, Utf8GamePath key, bool removeUsed)
    {
        if (!_files.Missing.Contains(file))
            return true;

        if (removeUsed)
            _files.RemoveUsedPath(option, file, key);

        Penumbra.Log.Debug($"[RemoveMissingPaths] Removing {key} -> {file} from {mod.Name}.");
        return false;
    }
}
