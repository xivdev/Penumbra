using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Editor;

public class ModMerger : IDisposable, IService
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    private readonly ModGroupEditor      _editor;
    private readonly ModSelection        _selection;
    private readonly DuplicateManager    _duplicates;
    private readonly ModManager          _mods;
    private readonly ModCreator          _creator;

    public Mod? MergeFromMod
        => _selection.Mod;

    public Mod?   MergeToMod;
    public string OptionGroupName = "Merges";
    public string OptionName      = string.Empty;

    private readonly Dictionary<string, string> _fileToFile         = [];
    private readonly HashSet<string>            _createdDirectories = [];
    private readonly HashSet<int>               _createdGroups      = [];
    private readonly HashSet<IModOption>        _createdOptions     = [];

    public readonly HashSet<IModDataContainer> SelectedOptions = [];

    public readonly IReadOnlyList<string> Warnings = new List<string>();
    public          Exception?            Error { get; private set; }

    public ModMerger(ModManager mods, ModGroupEditor editor, ModSelection selection, DuplicateManager duplicates,
        CommunicatorService communicator, ModCreator creator, Configuration config)
    {
        _editor                     =  editor;
        _selection                  =  selection;
        _duplicates                 =  duplicates;
        _communicator               =  communicator;
        _creator                    =  creator;
        _config                     =  config;
        _mods                       =  mods;
        _selection.Subscribe(OnSelectionChange, ModSelection.Priority.ModMerger);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModMerger);
    }

    public void Dispose()
    {
        _selection.Unsubscribe(OnSelectionChange);
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

            _duplicates.DeduplicateMod(MergeToMod.ModPath, true);
        }
        catch (Exception ex)
        {
            Error = ex;
            Penumbra.Messager.NotificationMessage(ex, $"Could not merge {MergeFromMod!.Name} into {MergeToMod!.Name}, cleaning up changes.",
                NotificationType.Error, false);
            FailureCleanup();
            DataCleanup();
        }
    }

    private void MergeWithOptions()
    {
        MergeIntoOption([MergeFromMod!.Default], MergeToMod!.Default, false);

        foreach (var originalGroup in MergeFromMod!.Groups)
        {
            var (group, groupIdx, groupCreated) = _editor.FindOrAddModGroup(MergeToMod!, originalGroup.Type, originalGroup.Name);
            if (groupCreated)
                _createdGroups.Add(groupIdx);
            if (group == null)
                throw new Exception(
                    $"The merged group {originalGroup.Name} already existed, but had a different type than the original group of type {originalGroup.Type}.");

            foreach (var originalOption in originalGroup.DataContainers)
            {
                var (option, _, optionCreated) = _editor.FindOrAddOption(group, originalOption.GetName());
                if (optionCreated)
                {
                    _createdOptions.Add(option!);
                    // #TODO DataContainer <> Option.
                    MergeIntoOption([originalOption], (IModDataContainer)option!, false);
                }
                else
                {
                    throw new Exception(
                        $"Could not merge {MergeFromMod!.Name} into {MergeToMod!.Name}: The option {option!.FullName} already existed.");
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
            MergeIntoOption(MergeFromMod!.AllDataContainers.Reverse(), MergeToMod!.Default, true);
        }
        else if (groupName.Length * optionName.Length == 0)
        {
            return;
        }

        var (group, groupIdx, groupCreated) = _editor.FindOrAddModGroup(MergeToMod!, GroupType.Multi, groupName, SaveType.None);
        if (groupCreated)
            _createdGroups.Add(groupIdx);
        var (option, _, optionCreated) = _editor.FindOrAddOption(group!, optionName, SaveType.None);
        if (optionCreated)
            _createdOptions.Add(option!);
        var dir = ModCreator.NewOptionDirectory(MergeToMod!.ModPath, groupName, _config.ReplaceNonAsciiOnImport);
        if (!dir.Exists)
            _createdDirectories.Add(dir.FullName);
        dir = ModCreator.NewOptionDirectory(dir, optionName, _config.ReplaceNonAsciiOnImport);
        if (!dir.Exists)
            _createdDirectories.Add(dir.FullName);
        CopyFiles(dir);
        // #TODO DataContainer <> Option.
        MergeIntoOption(MergeFromMod!.AllDataContainers.Reverse(), (IModDataContainer)option!, true);
    }

    private void MergeIntoOption(IEnumerable<IModDataContainer> mergeOptions, IModDataContainer option, bool fromFileToFile)
    {
        var redirections = option.Files.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var swaps        = option.FileSwaps.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var manips       = option.Manipulations.Clone();

        foreach (var originalOption in mergeOptions)
        {
            if (!manips.MergeForced(originalOption.Manipulations, out var failed))
                throw new Exception(
                    $"Could not add meta manipulation {failed} from {originalOption.GetFullName()} to {option.GetFullName()} because another manipulation of the same data already exists in this option.");

            foreach (var (swapA, swapB) in originalOption.FileSwaps)
            {
                if (!swaps.TryAdd(swapA, swapB))
                    throw new Exception(
                        $"Could not add file swap {swapB} -> {swapA} from {originalOption.GetFullName()} to {option.GetFullName()} because another swap of the key already exists.");
            }

            foreach (var (gamePath, path) in originalOption.Files)
            {
                if (!GetFullPath(path, out var newFile))
                    throw new Exception(
                        $"Could not add file redirection {path} -> {gamePath} from {originalOption.GetFullName()} to {option.GetFullName()} because the file does not exist in the new mod.");
                if (!redirections.TryAdd(gamePath, newFile))
                    throw new Exception(
                        $"Could not add file redirection {path} -> {gamePath} from {originalOption.GetFullName()} to {option.GetFullName()} because a redirection for the game path already exists.");
            }
        }

        _editor.SetFiles(option, redirections, SaveType.None);
        _editor.SetFileSwaps(option, swaps, SaveType.None);
        _editor.SetManipulations(option, manips, SaveType.None);
        _editor.ForceSave(option, SaveType.ImmediateSync);
        return;

        bool GetFullPath(FullPath input, out FullPath ret)
        {
            if (fromFileToFile)
            {
                if (!_fileToFile.TryGetValue(input.FullName.ToLowerInvariant(), out var s))
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
            _fileToFile.Add(file.FullName.ToLowerInvariant(), path);
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
            dir = _creator.CreateEmptyMod(_mods.BasePath, modName, $"Split off from {mods[0].Mod.Name}.");
            if (dir == null)
                throw new Exception($"Could not split off mods, unable to create new mod with name {modName}.");

            _mods.AddMod(dir, false);
            result = _mods[^1];
            if (mods.Count == 1)
            {
                var files = CopySubModFiles(mods[0], dir);
                _editor.SetFiles(result.Default, files, SaveType.None);
                _editor.SetFileSwaps(result.Default, mods[0].FileSwaps, SaveType.None);
                _editor.SetManipulations(result.Default, mods[0].Manipulations, SaveType.None);
                _editor.ForceSave(result.Default);
            }
            else
            {
                foreach (var originalOption in mods)
                {
                    if (originalOption.Group is not { } originalGroup)
                    {
                        var files = CopySubModFiles(mods[0], dir);
                        _editor.SetFiles(result.Default, files);
                        _editor.SetFileSwaps(result.Default, mods[0].FileSwaps);
                        _editor.SetManipulations(result.Default, mods[0].Manipulations);
                        _editor.ForceSave(result.Default);
                    }
                    else
                    {
                        // TODO DataContainer <> Option.
                        var (group, _, _)  = _editor.FindOrAddModGroup(result, originalGroup.Type, originalGroup.Name);
                        var (option, _, _) = _editor.FindOrAddOption(group!, originalOption.GetName());
                        var folder = Path.Combine(dir.FullName, group!.Name, option!.Name);
                        var files  = CopySubModFiles(originalOption, new DirectoryInfo(folder));
                        _editor.SetFiles((IModDataContainer)option, files, SaveType.None);
                        _editor.SetFileSwaps((IModDataContainer)option, originalOption.FileSwaps, SaveType.None);
                        _editor.SetManipulations((IModDataContainer)option, originalOption.Manipulations, SaveType.None);
                        _editor.ForceSave((IModDataContainer)option);
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

    private static Dictionary<Utf8GamePath, FullPath> CopySubModFiles(IModDataContainer option, DirectoryInfo newMod)
    {
        var ret        = new Dictionary<Utf8GamePath, FullPath>(option.Files.Count);
        var parentPath = ((Mod)option.Mod).ModPath.FullName;
        foreach (var (path, file) in option.Files)
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
            _editor.DeleteOption(option);
            Penumbra.Log.Verbose($"[Merger] Removed option {option.FullName}.");
        }

        foreach (var group in _createdGroups)
        {
            var groupName = MergeToMod!.Groups[group];
            _editor.DeleteModGroup(groupName);
            Penumbra.Log.Verbose($"[Merger] Removed option group {groupName.Name}.");
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

    private void OnSelectionChange(Mod? oldSelection, Mod? newSelection)
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
