using Dalamud.Interface.Internal.Notifications;
using Dalamud.Utility;
using OtterGui;
using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.UI.ModsTab;

namespace Penumbra.Mods.Editor;

public class ModMerger : IDisposable
{
    private readonly CommunicatorService   _communicator;
    private readonly ModOptionEditor       _editor;
    private readonly ModFileSystemSelector _selector;
    private readonly DuplicateManager      _duplicates;
    private readonly ModManager            _mods;
    private readonly ModCreator            _creator;

    public Mod? MergeFromMod
        => _selector.Selected;

    public Mod?   MergeToMod;
    public string OptionGroupName = "Merges";
    public string OptionName      = string.Empty;

    private readonly Dictionary<string, string> _fileToFile         = new();
    private readonly HashSet<string>            _createdDirectories = new();
    private readonly HashSet<int>               _createdGroups      = new();
    private readonly HashSet<SubMod>            _createdOptions     = new();

    public readonly HashSet<SubMod> SelectedOptions = new();

    public readonly IReadOnlyList<string> Warnings = new List<string>();
    public          Exception?            Error { get; private set; }

    public ModMerger(ModManager mods, ModOptionEditor editor, ModFileSystemSelector selector, DuplicateManager duplicates,
        CommunicatorService communicator, ModCreator creator)
    {
        _editor                    =  editor;
        _selector                  =  selector;
        _duplicates                =  duplicates;
        _communicator              =  communicator;
        _creator                   =  creator;
        _mods                      =  mods;
        _selector.SelectionChanged += OnSelectionChange;
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModMerger);
    }

    public void Dispose()
    {
        _selector.SelectionChanged -= OnSelectionChange;
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
    }

    public IEnumerable<Mod> ModsWithoutCurrent
        => _mods.Where(m => m != MergeFromMod);

    public bool CanMerge
        => MergeToMod != null && MergeToMod != MergeFromMod;

    public void Merge()
    {
        if (MergeFromMod == null || MergeToMod == null || MergeFromMod == MergeToMod)
            return;

        try
        {
            Error = null;
            DataCleanup();
            if (MergeFromMod.HasOptions)
                MergeWithOptions();
            else
                MergeIntoOption(OptionGroupName, OptionName);
            _duplicates.DeduplicateMod(MergeToMod.ModPath);
        }
        catch (Exception ex)
        {
            Error = ex;
            Penumbra.Messager.NotificationMessage(ex, $"Could not merge {MergeFromMod!.Name} into {MergeToMod!.Name}, cleaning up changes.", NotificationType.Error, false);
            FailureCleanup();
            DataCleanup();
        }
    }

    private void MergeWithOptions()
    {
        MergeIntoOption(Enumerable.Repeat(MergeFromMod!.Default, 1), MergeToMod!.Default, false);

        foreach (var originalGroup in MergeFromMod!.Groups)
        {
            var (group, groupIdx, groupCreated) = _editor.FindOrAddModGroup(MergeToMod!, originalGroup.Type, originalGroup.Name);
            if (groupCreated)
                _createdGroups.Add(groupIdx);
            if (group.Type != originalGroup.Type)
                ((List<string>)Warnings).Add(
                    $"The merged group {group.Name} already existed, but has a different type {group.Type} than the original group of type {originalGroup.Type}.");

            foreach (var originalOption in originalGroup)
            {
                var (option, optionCreated) = _editor.FindOrAddOption(MergeToMod!, groupIdx, originalOption.Name);
                if (optionCreated)
                {
                    _createdOptions.Add(option);
                    MergeIntoOption(Enumerable.Repeat(originalOption, 1), option, false);
                }
                else
                {
                    throw new Exception(
                        $"Could not merge {MergeFromMod!.Name} into {MergeToMod!.Name}: The option {option.FullName} already existed.");
                }
            }
        }

        CopyFiles(MergeToMod!.ModPath);
    }

    private void MergeIntoOption(string groupName, string optionName)
    {
        if (groupName.Length == 0 && optionName.Length == 0)
        {
            CopyFiles(MergeToMod!.ModPath);
            MergeIntoOption(MergeFromMod!.AllSubMods.Reverse(), MergeToMod!.Default, true);
        }
        else if (groupName.Length * optionName.Length == 0)
        {
            return;
        }

        var (group, groupIdx, groupCreated) = _editor.FindOrAddModGroup(MergeToMod!, GroupType.Multi, groupName);
        if (groupCreated)
            _createdGroups.Add(groupIdx);
        var (option, optionCreated) = _editor.FindOrAddOption(MergeToMod!, groupIdx, optionName);
        if (optionCreated)
            _createdOptions.Add(option);
        var dir = ModCreator.NewOptionDirectory(MergeToMod!.ModPath, groupName);
        if (!dir.Exists)
            _createdDirectories.Add(dir.FullName);
        dir = ModCreator.NewOptionDirectory(dir, optionName);
        if (!dir.Exists)
            _createdDirectories.Add(dir.FullName);
        CopyFiles(dir);
        MergeIntoOption(MergeFromMod!.AllSubMods.Reverse(), option, true);
    }

    private void MergeIntoOption(IEnumerable<ISubMod> mergeOptions, SubMod option, bool fromFileToFile)
    {
        var redirections = option.FileData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var swaps        = option.FileSwapData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var manips       = option.ManipulationData.ToHashSet();

        bool GetFullPath(FullPath input, out FullPath ret)
        {
            if (fromFileToFile)
            {
                if (!_fileToFile.TryGetValue(input.FullName, out var s))
                {
                    ret = input;
                    return false;
                }

                ret = new FullPath(s);
                return true;
            }

            if (!Utf8RelPath.FromFile(input, MergeFromMod!.ModPath, out var relPath))
                throw new Exception($"Could not create relative path from {input} and {MergeFromMod!.ModPath}.");

            ret = new FullPath(MergeToMod!.ModPath, relPath);
            return true;
        }

        foreach (var originalOption in mergeOptions)
        {
            foreach (var manip in originalOption.Manipulations)
            {
                if (!manips.Add(manip))
                    throw new Exception(
                        $"Could not add meta manipulation {manip} from {originalOption.FullName} to {option.FullName} because another manipulation of the same data already exists in this option.");
            }

            foreach (var (swapA, swapB) in originalOption.FileSwaps)
            {
                if (!swaps.TryAdd(swapA, swapB))
                    throw new Exception(
                        $"Could not add file swap {swapB} -> {swapA} from {originalOption.FullName} to {option.FullName} because another swap of the key already exists.");
            }

            foreach (var (gamePath, path) in originalOption.Files)
            {
                if (!GetFullPath(path, out var newFile))
                    throw new Exception(
                        $"Could not add file redirection {path} -> {gamePath} from {originalOption.FullName} to {option.FullName} because the file does not exist in the new mod.");
                if (!redirections.TryAdd(gamePath, newFile))
                    throw new Exception(
                        $"Could not add file redirection {path} -> {gamePath} from {originalOption.FullName} to {option.FullName} because a redirection for the game path already exists.");
            }
        }

        _editor.OptionSetFiles(MergeToMod!, option.GroupIdx, option.OptionIdx, redirections);
        _editor.OptionSetFileSwaps(MergeToMod!, option.GroupIdx, option.OptionIdx, swaps);
        _editor.OptionSetManipulations(MergeToMod!, option.GroupIdx, option.OptionIdx, manips);
    }

    private void CopyFiles(DirectoryInfo directory)
    {
        directory = Directory.CreateDirectory(directory.FullName);
        foreach (var file in MergeFromMod!.ModPath.EnumerateDirectories()
                     .Where(d => !d.IsHidden())
                     .SelectMany(FileExtensions.EnumerateNonHiddenFiles))
        {
            var path = Path.GetRelativePath(MergeFromMod.ModPath.FullName, file.FullName);
            path = Path.Combine(directory.FullName, path);
            var finalDir = Path.GetDirectoryName(path)!;
            var dir      = finalDir;
            while (!dir.IsNullOrEmpty())
            {
                if (!Directory.Exists(dir))
                    _createdDirectories.Add(dir);
                else
                    break;

                dir = Path.GetDirectoryName(dir);
            }

            Directory.CreateDirectory(finalDir);
            file.CopyTo(path);
            Penumbra.Log.Verbose($"[Merger] Copied file {file.FullName} to {path}.");
            _fileToFile.Add(file.FullName, path);
        }
    }

    public void SplitIntoMod(string modName)
    {
        var mods = SelectedOptions.ToList();
        if (mods.Count == 0)
            return;

        ((List<string>)Warnings).Clear();
        Error = null;
        DirectoryInfo? dir    = null;
        Mod?           result = null;
        try
        {
            dir = _creator.CreateEmptyMod(_mods.BasePath, modName, $"Split off from {mods[0].ParentMod.Name}.");
            if (dir == null)
                throw new Exception($"Could not split off mods, unable to create new mod with name {modName}.");

            _mods.AddMod(dir);
            result = _mods[^1];
            if (mods.Count == 1)
            {
                var files = CopySubModFiles(mods[0], dir);
                _editor.OptionSetFiles(result, -1, 0, files);
                _editor.OptionSetFileSwaps(result, -1, 0, mods[0].FileSwapData);
                _editor.OptionSetManipulations(result, -1, 0, mods[0].ManipulationData);
            }
            else
            {
                foreach (var originalOption in mods)
                {
                    var originalGroup = originalOption.ParentMod.Groups[originalOption.GroupIdx];
                    if (originalOption.IsDefault)
                    {
                        var files = CopySubModFiles(mods[0], dir);
                        _editor.OptionSetFiles(result, -1, 0, files);
                        _editor.OptionSetFileSwaps(result, -1, 0, mods[0].FileSwapData);
                        _editor.OptionSetManipulations(result, -1, 0, mods[0].ManipulationData);
                    }
                    else
                    {
                        var (group, groupIdx, _) = _editor.FindOrAddModGroup(result, originalGroup.Type, originalGroup.Name);
                        var (option, _)          = _editor.FindOrAddOption(result, groupIdx, originalOption.Name);
                        var folder = Path.Combine(dir.FullName, group.Name, option.Name);
                        var files  = CopySubModFiles(originalOption, new DirectoryInfo(folder));
                        _editor.OptionSetFiles(result, groupIdx, option.OptionIdx, files);
                        _editor.OptionSetFileSwaps(result, groupIdx, option.OptionIdx, originalOption.FileSwapData);
                        _editor.OptionSetManipulations(result, groupIdx, option.OptionIdx, originalOption.ManipulationData);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Error = e;
            if (result != null)
                _mods.DeleteMod(result);
            else if (dir != null)
                try
                {
                    Directory.Delete(dir.FullName);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"Could not clean up after failure to split options into new mod {modName}:\n{ex}");
                }
        }
    }

    private static Dictionary<Utf8GamePath, FullPath> CopySubModFiles(SubMod option, DirectoryInfo newMod)
    {
        var ret        = new Dictionary<Utf8GamePath, FullPath>(option.FileData.Count);
        var parentPath = ((Mod)option.ParentMod).ModPath.FullName;
        foreach (var (path, file) in option.FileData)
        {
            var target = Path.GetRelativePath(parentPath, file.FullName);
            target = Path.Combine(newMod.FullName, target);
            var targetPath = new FullPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            // Copy throws if the file exists, which we want.
            // This copies if the target does not exist, throws if it exists and is different, or does nothing if it exists and is identical.
            if (!File.Exists(target) || !DuplicateManager.CompareFilesDirectly(targetPath, file))
                File.Copy(file.FullName, target);
            Penumbra.Log.Verbose($"[Splitter] Copied file {file.FullName} to {target}.");
            ret.Add(path, targetPath);
        }

        return ret;
    }

    private void DataCleanup()
    {
        _fileToFile.Clear();
        _createdDirectories.Clear();
        _createdGroups.Clear();
        _createdOptions.Clear();
    }

    private void FailureCleanup()
    {
        foreach (var option in _createdOptions)
        {
            _editor.DeleteOption(MergeToMod!, option.GroupIdx, option.OptionIdx);
            Penumbra.Log.Verbose($"[Merger] Removed option {option.FullName}.");
        }

        foreach (var group in _createdGroups)
        {
            var groupName = MergeToMod!.Groups[group];
            _editor.DeleteModGroup(MergeToMod!, group);
            Penumbra.Log.Verbose($"[Merger] Removed option group {groupName}.");
        }

        foreach (var dir in _createdDirectories)
        {
            if (!Directory.Exists(dir))
                continue;

            try
            {
                Directory.Delete(dir, true);
                Penumbra.Log.Verbose($"[Merger] Deleted {dir}.");
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error(
                    $"Could not clean up after failing to merge {MergeFromMod!.Name} into {MergeToMod!.Name}, unable to delete {dir}:\n{ex}");
            }
        }

        foreach (var (_, file) in _fileToFile)
        {
            if (!File.Exists(file))
                continue;

            try
            {
                File.Delete(file);
                Penumbra.Log.Verbose($"[Merger] Deleted {file}.");
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error(
                    $"Could not clean up after failing to merge {MergeFromMod!.Name} into {MergeToMod!.Name}, unable to delete {file}:\n{ex}");
            }
        }
    }

    private void OnSelectionChange(Mod? oldSelection, Mod? newSelection, in ModFileSystemSelector.ModState state)
    {
        if (OptionGroupName == "Merges" && OptionName.Length == 0 || OptionName == oldSelection?.Name.Text)
            OptionName = newSelection?.Name.Text ?? string.Empty;

        if (MergeToMod == newSelection)
            MergeToMod = null;

        SelectedOptions.Clear();
    }

    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? _1, DirectoryInfo? _2)
    {
        switch (type)
        {
            case ModPathChangeType.Deleted:
            {
                if (mod == MergeFromMod)
                    SelectedOptions.Clear();

                if (mod == MergeToMod)
                    MergeToMod = null;
                break;
            }
            case ModPathChangeType.StartingReload:
                SelectedOptions.Clear();
                MergeToMod = null;
                break;
        }
    }
}
