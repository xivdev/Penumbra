using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Utility;
using OtterGui;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods.Manager;
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

    public Mod?   MergeFromMod { get; private set; }
    public Mod?   MergeToMod;
    public string OptionGroupName = "Merges";
    public string OptionName      = string.Empty;


    private readonly Dictionary<string, string> _fileToFile         = new();
    private readonly HashSet<string>            _createdDirectories = new();
    public readonly  HashSet<SubMod>            SelectedOptions     = new();

    private int        _createdGroup = -1;
    private SubMod?    _createdOption;
    public  Exception? Error { get; private set; }

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
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.Api);
    }

    public void Dispose()
    {
        _selector.SelectionChanged -= OnSelectionChange;
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
    }

    public IEnumerable<Mod> ModsWithoutCurrent
        => _mods.Where(m => m != MergeFromMod);

    public bool CanMerge
        => MergeToMod != null && MergeToMod != MergeFromMod && !MergeFromMod!.HasOptions;

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
            Penumbra.ChatService.NotificationMessage(
                $"Could not merge {MergeFromMod!.Name} into {MergeToMod!.Name}, cleaning up changes.:\n{ex}", "Failure",
                NotificationType.Error);
            FailureCleanup();
            DataCleanup();
        }
    }

    private void MergeWithOptions()
    {
        // Not supported
    }

    private void MergeIntoOption(string groupName, string optionName)
    {
        if (groupName.Length == 0 && optionName.Length == 0)
        {
            CopyFiles(MergeToMod!.ModPath);
            MergeIntoOption(MergeFromMod!.AllSubMods.Reverse(), MergeToMod!.Default);
        }
        else if (groupName.Length * optionName.Length == 0)
        {
            return;
        }

        var (group, groupIdx, groupCreated) = _editor.FindOrAddModGroup(MergeToMod!, GroupType.Multi, groupName);
        if (groupCreated)
            _createdGroup = groupIdx;
        var (option, optionCreated) = _editor.FindOrAddOption(MergeToMod!, groupIdx, optionName);
        if (optionCreated)
            _createdOption = option;
        var dir = ModCreator.NewOptionDirectory(MergeToMod!.ModPath, groupName);
        if (!dir.Exists)
            _createdDirectories.Add(dir.FullName);
        dir = ModCreator.NewOptionDirectory(dir, optionName);
        if (!dir.Exists)
            _createdDirectories.Add(dir.FullName);
        CopyFiles(dir);
        MergeIntoOption(MergeFromMod!.AllSubMods.Reverse(), option);
    }

    private void MergeIntoOption(IEnumerable<SubMod> mergeOptions, SubMod option)
    {
        var redirections = option.FileData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var swaps        = option.FileSwapData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var manips       = option.ManipulationData.ToHashSet();
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

            foreach (var (gamePath, relPath) in originalOption.Files)
            {
                if (!_fileToFile.TryGetValue(relPath.FullName, out var newFile))
                    throw new Exception(
                        $"Could not add file redirection {relPath} -> {gamePath} from {originalOption.FullName} to {option.FullName} because the file does not exist in the new mod.");
                if (!redirections.TryAdd(gamePath, new FullPath(newFile)))
                    throw new Exception(
                        $"Could not add file redirection {relPath} -> {gamePath} from {originalOption.FullName} to {option.FullName} because a redirection for the game path already exists.");
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
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file.FullName, target);
            Penumbra.Log.Verbose($"[Splitter] Copied file {file.FullName} to {target}.");
            ret.Add(path, new FullPath(target));
        }

        return ret;
    }

    private void DataCleanup()
    {
        _fileToFile.Clear();
        _createdDirectories.Clear();
        _createdOption = null;
        _createdGroup  = -1;
    }

    private void FailureCleanup()
    {
        if (_createdGroup >= 0 && _createdGroup < MergeToMod!.Groups.Count)
            _editor.DeleteModGroup(MergeToMod!, _createdGroup);
        else if (_createdOption != null)
            _editor.DeleteOption(MergeToMod!, _createdOption.GroupIdx, _createdOption.OptionIdx);

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
        MergeFromMod = newSelection;
    }

    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? _1, DirectoryInfo? _2)
    {
        switch (type)
        {
            case ModPathChangeType.Deleted:
            {
                if (mod == MergeFromMod)
                {
                    SelectedOptions.Clear();
                    MergeFromMod = null;
                }

                if (mod == MergeToMod)
                    MergeToMod = null;
                break;
            }
            case ModPathChangeType.StartingReload:
                SelectedOptions.Clear();
                MergeFromMod = null;
                MergeToMod   = null;
                break;
            case ModPathChangeType.Reloaded:
                MergeFromMod = _selector.Selected;
                break;
        }
    }
}
