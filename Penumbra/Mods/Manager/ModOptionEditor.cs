using Dalamud.Interface.Internal.Notifications;
using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Mods.Manager;

public enum ModOptionChangeType
{
    GroupRenamed,
    GroupAdded,
    GroupDeleted,
    GroupMoved,
    GroupTypeChanged,
    PriorityChanged,
    OptionAdded,
    OptionDeleted,
    OptionMoved,
    OptionFilesChanged,
    OptionFilesAdded,
    OptionSwapsChanged,
    OptionMetaChanged,
    DisplayChange,
    PrepareChange,
    DefaultOptionChanged,
}

public class ModOptionEditor
{
    private readonly CommunicatorService _communicator;
    private readonly SaveService         _saveService;

    public ModOptionEditor(CommunicatorService communicator, SaveService saveService)
    {
        _communicator = communicator;
        _saveService  = saveService;
    }

    /// <summary> Change the type of a group given by mod and index to type, if possible. </summary>
    public void ChangeModGroupType(Mod mod, int groupIdx, GroupType type)
    {
        var group = mod.Groups[groupIdx];
        if (group.Type == type)
            return;

        mod.Groups[groupIdx] = group.Convert(type);
        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupTypeChanged, mod, groupIdx, -1, -1);
    }

    /// <summary> Change the settings stored as default options in a mod.</summary>
    public void ChangeModGroupDefaultOption(Mod mod, int groupIdx, uint defaultOption)
    {
        var group = mod.Groups[groupIdx];
        if (group.DefaultSettings == defaultOption)
            return;

        group.DefaultSettings = defaultOption;
        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.DefaultOptionChanged, mod, groupIdx, -1, -1);
    }

    /// <summary> Rename an option group if possible. </summary>
    public void RenameModGroup(Mod mod, int groupIdx, string newName)
    {
        var group   = mod.Groups[groupIdx];
        var oldName = group.Name;
        if (oldName == newName || !VerifyFileName(mod, group, newName, true))
            return;

        _saveService.ImmediateDelete(new ModSaveGroup(mod, groupIdx));
        var _ = group switch
        {
            SingleModGroup s => s.Name = newName,
            MultiModGroup m  => m.Name = newName,
            _                => newName,
        };

        _saveService.ImmediateSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupRenamed, mod, groupIdx, -1, -1);
    }

    /// <summary> Add a new, empty option group of the given type and name. </summary>
    public void AddModGroup(Mod mod, GroupType type, string newName)
    {
        if (!VerifyFileName(mod, null, newName, true))
            return;

        var maxPriority = mod.Groups.Count == 0 ? 0 : mod.Groups.Max(o => o.Priority) + 1;

        mod.Groups.Add(type == GroupType.Multi
            ? new MultiModGroup
            {
                Name     = newName,
                Priority = maxPriority,
            }
            : new SingleModGroup
            {
                Name     = newName,
                Priority = maxPriority,
            });
        _saveService.ImmediateSave(new ModSaveGroup(mod, mod.Groups.Count - 1));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupAdded, mod, mod.Groups.Count - 1, -1, -1);
    }

    /// <summary> Add a new mod, empty option group of the given type and name if it does not exist already. </summary>
    public (IModGroup, int, bool) FindOrAddModGroup(Mod mod, GroupType type, string newName)
    {
        var idx = mod.Groups.IndexOf(g => g.Name == newName);
        if (idx >= 0)
            return (mod.Groups[idx], idx, false);

        AddModGroup(mod, type, newName);
        if (mod.Groups[^1].Name != newName)
            throw new Exception($"Could not create new mod group with name {newName}.");

        return (mod.Groups[^1], mod.Groups.Count - 1, true);
    }

    /// <summary> Delete a given option group. Fires an event to prepare before actually deleting. </summary>
    public void DeleteModGroup(Mod mod, int groupIdx)
    {
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, groupIdx, -1, -1);
        mod.Groups.RemoveAt(groupIdx);
        UpdateSubModPositions(mod, groupIdx);
        _saveService.SaveAllOptionGroups(mod, false);
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupDeleted, mod, groupIdx, -1, -1);
    }

    /// <summary> Move the index of a given option group. </summary>
    public void MoveModGroup(Mod mod, int groupIdxFrom, int groupIdxTo)
    {
        if (!mod.Groups.Move(groupIdxFrom, groupIdxTo))
            return;

        UpdateSubModPositions(mod, Math.Min(groupIdxFrom, groupIdxTo));
        _saveService.SaveAllOptionGroups(mod, false);
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupMoved, mod, groupIdxFrom, -1, groupIdxTo);
    }

    /// <summary> Change the description of the given option group. </summary>
    public void ChangeGroupDescription(Mod mod, int groupIdx, string newDescription)
    {
        var group = mod.Groups[groupIdx];
        if (group.Description == newDescription)
            return;

        var _ = group switch
        {
            SingleModGroup s => s.Description = newDescription,
            MultiModGroup m  => m.Description = newDescription,
            _                => newDescription,
        };
        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.DisplayChange, mod, groupIdx, -1, -1);
    }

    /// <summary> Change the description of the given option. </summary>
    public void ChangeOptionDescription(Mod mod, int groupIdx, int optionIdx, string newDescription)
    {
        var group  = mod.Groups[groupIdx];
        var option = group[optionIdx];
        if (option.Description == newDescription || option is not SubMod s)
            return;

        s.Description = newDescription;
        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.DisplayChange, mod, groupIdx, optionIdx, -1);
    }

    /// <summary> Change the internal priority of the given option group. </summary>
    public void ChangeGroupPriority(Mod mod, int groupIdx, int newPriority)
    {
        var group = mod.Groups[groupIdx];
        if (group.Priority == newPriority)
            return;

        var _ = group switch
        {
            SingleModGroup s => s.Priority = newPriority,
            MultiModGroup m  => m.Priority = newPriority,
            _                => newPriority,
        };
        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.PriorityChanged, mod, groupIdx, -1, -1);
    }

    /// <summary> Change the internal priority of the given option. </summary>
    public void ChangeOptionPriority(Mod mod, int groupIdx, int optionIdx, int newPriority)
    {
        switch (mod.Groups[groupIdx])
        {
            case SingleModGroup:
                ChangeGroupPriority(mod, groupIdx, newPriority);
                break;
            case MultiModGroup m:
                if (m.PrioritizedOptions[optionIdx].Priority == newPriority)
                    return;

                m.PrioritizedOptions[optionIdx] = (m.PrioritizedOptions[optionIdx].Mod, newPriority);
                _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
                _communicator.ModOptionChanged.Invoke(ModOptionChangeType.PriorityChanged, mod, groupIdx, optionIdx, -1);
                return;
        }
    }

    /// <summary> Rename the given option. </summary>
    public void RenameOption(Mod mod, int groupIdx, int optionIdx, string newName)
    {
        switch (mod.Groups[groupIdx])
        {
            case SingleModGroup s:
                if (s.OptionData[optionIdx].Name == newName)
                    return;

                s.OptionData[optionIdx].Name = newName;
                break;
            case MultiModGroup m:
                var option = m.PrioritizedOptions[optionIdx].Mod;
                if (option.Name == newName)
                    return;

                option.Name = newName;
                break;
        }

        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.DisplayChange, mod, groupIdx, optionIdx, -1);
    }

    /// <summary> Add a new empty option of the given name for the given group. </summary>
    public void AddOption(Mod mod, int groupIdx, string newName)
    {
        var group  = mod.Groups[groupIdx];
        var subMod = new SubMod(mod) { Name = newName };
        subMod.SetPosition(groupIdx, group.Count);
        switch (group)
        {
            case SingleModGroup s:
                s.OptionData.Add(subMod);
                break;
            case MultiModGroup m:
                m.PrioritizedOptions.Add((subMod, 0));
                break;
        }

        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionAdded, mod, groupIdx, group.Count - 1, -1);
    }

    /// <summary> Add a new empty option of the given name for the given group if it does not exist already. </summary>
    public (SubMod, bool) FindOrAddOption(Mod mod, int groupIdx, string newName)
    {
        var group = mod.Groups[groupIdx];
        var idx   = group.IndexOf(o => o.Name == newName);
        if (idx >= 0)
            return ((SubMod)group[idx], false);

        AddOption(mod, groupIdx, newName);
        if (group[^1].Name != newName)
            throw new Exception($"Could not create new option with name {newName} in {group.Name}.");

        return ((SubMod)group[^1], true);
    }

    /// <summary> Add an existing option to a given group with a given priority. </summary>
    public void AddOption(Mod mod, int groupIdx, ISubMod option, int priority = 0)
    {
        if (option is not SubMod o)
            return;

        var group = mod.Groups[groupIdx];
        if (group.Type is GroupType.Multi && group.Count >= IModGroup.MaxMultiOptions)
        {
            Penumbra.Log.Error(
                $"Could not add option {option.Name} to {group.Name} for mod {mod.Name}, "
              + $"since only up to {IModGroup.MaxMultiOptions} options are supported in one group.");
            return;
        }

        o.SetPosition(groupIdx, group.Count);

        switch (group)
        {
            case SingleModGroup s:
                s.OptionData.Add(o);
                break;
            case MultiModGroup m:
                m.PrioritizedOptions.Add((o, priority));
                break;
        }

        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionAdded, mod, groupIdx, group.Count - 1, -1);
    }

    /// <summary> Delete the given option from the given group. </summary>
    public void DeleteOption(Mod mod, int groupIdx, int optionIdx)
    {
        var group = mod.Groups[groupIdx];
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, groupIdx, optionIdx, -1);
        switch (group)
        {
            case SingleModGroup s:
                s.OptionData.RemoveAt(optionIdx);

                break;
            case MultiModGroup m:
                m.PrioritizedOptions.RemoveAt(optionIdx);
                break;
        }

        group.UpdatePositions(optionIdx);
        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionDeleted, mod, groupIdx, optionIdx, -1);
    }

    /// <summary> Move an option inside the given option group. </summary>
    public void MoveOption(Mod mod, int groupIdx, int optionIdxFrom, int optionIdxTo)
    {
        var group = mod.Groups[groupIdx];
        if (!group.MoveOption(optionIdxFrom, optionIdxTo))
            return;

        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMoved, mod, groupIdx, optionIdxFrom, optionIdxTo);
    }

    /// <summary> Set the meta manipulations for a given option. Replaces existing manipulations. </summary>
    public void OptionSetManipulations(Mod mod, int groupIdx, int optionIdx, HashSet<MetaManipulation> manipulations)
    {
        var subMod = GetSubMod(mod, groupIdx, optionIdx);
        if (subMod.Manipulations.Count == manipulations.Count
         && subMod.Manipulations.All(m => manipulations.TryGetValue(m, out var old) && old.EntryEquals(m)))
            return;

        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, groupIdx, optionIdx, -1);
        subMod.ManipulationData.SetTo(manipulations);
        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMetaChanged, mod, groupIdx, optionIdx, -1);
    }

    /// <summary> Set the file redirections for a given option. Replaces existing redirections. </summary>
    public void OptionSetFiles(Mod mod, int groupIdx, int optionIdx, IReadOnlyDictionary<Utf8GamePath, FullPath> replacements)
    {
        var subMod = GetSubMod(mod, groupIdx, optionIdx);
        if (subMod.FileData.SetEquals(replacements))
            return;

        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, groupIdx, optionIdx, -1);
        subMod.FileData.SetTo(replacements);
        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionFilesChanged, mod, groupIdx, optionIdx, -1);
    }

    /// <summary> Add additional file redirections to a given option, keeping already existing ones. Only fires an event if anything is actually added.</summary>
    public void OptionAddFiles(Mod mod, int groupIdx, int optionIdx, IReadOnlyDictionary<Utf8GamePath, FullPath> additions)
    {
        var subMod   = GetSubMod(mod, groupIdx, optionIdx);
        var oldCount = subMod.FileData.Count;
        subMod.FileData.AddFrom(additions);
        if (oldCount != subMod.FileData.Count)
        {
            _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
            _communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionFilesAdded, mod, groupIdx, optionIdx, -1);
        }
    }

    /// <summary> Set the file swaps for a given option. Replaces existing swaps. </summary>
    public void OptionSetFileSwaps(Mod mod, int groupIdx, int optionIdx, IReadOnlyDictionary<Utf8GamePath, FullPath> swaps)
    {
        var subMod = GetSubMod(mod, groupIdx, optionIdx);
        if (subMod.FileSwapData.SetEquals(swaps))
            return;

        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, groupIdx, optionIdx, -1);
        subMod.FileSwapData.SetTo(swaps);
        _saveService.QueueSave(new ModSaveGroup(mod, groupIdx));
        _communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionSwapsChanged, mod, groupIdx, optionIdx, -1);
    }


    /// <summary> Verify that a new option group name is unique in this mod. </summary>
    public static bool VerifyFileName(Mod mod, IModGroup? group, string newName, bool message)
    {
        var path = newName.RemoveInvalidPathSymbols();
        if (path.Length != 0
         && !mod.Groups.Any(o => !ReferenceEquals(o, group)
             && string.Equals(o.Name.RemoveInvalidPathSymbols(), path, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (message)
            Penumbra.Chat.NotificationMessage(
                $"Could not name option {newName} because option with same filename {path} already exists.",
                "Warning", NotificationType.Warning);

        return false;
    }

    /// <summary> Update the indices stored in options from a given group on. </summary>
    private static void UpdateSubModPositions(Mod mod, int fromGroup)
    {
        foreach (var (group, groupIdx) in mod.Groups.WithIndex().Skip(fromGroup))
        {
            foreach (var (o, optionIdx) in group.OfType<SubMod>().WithIndex())
                o.SetPosition(groupIdx, optionIdx);
        }
    }

    /// <summary> Get the correct option for the given group and option index. </summary>
    private static SubMod GetSubMod(Mod mod, int groupIdx, int optionIdx)
    {
        if (groupIdx == -1 && optionIdx == 0)
            return mod.Default;

        return mod.Groups[groupIdx] switch
        {
            SingleModGroup s => s.OptionData[optionIdx],
            MultiModGroup m  => m.PrioritizedOptions[optionIdx].Mod,
            _                => throw new InvalidOperationException(),
        };
    }
}

public static class ModOptionChangeTypeExtension
{
    /// <summary>
    /// Give information for each type of change.
    /// If requiresSaving, collections need to be re-saved after this change.
    /// If requiresReloading, caches need to be manipulated after this change.
    /// If wasPrepared, caches have already removed the mod beforehand, then need add it again when this event is fired.
    /// Otherwise, caches need to reload the mod itself.
    /// </summary>
    public static void HandlingInfo(this ModOptionChangeType type, out bool requiresSaving, out bool requiresReloading, out bool wasPrepared)
    {
        (requiresSaving, requiresReloading, wasPrepared) = type switch
        {
            ModOptionChangeType.GroupRenamed         => (true, false, false),
            ModOptionChangeType.GroupAdded           => (true, false, false),
            ModOptionChangeType.GroupDeleted         => (true, true, false),
            ModOptionChangeType.GroupMoved           => (true, false, false),
            ModOptionChangeType.GroupTypeChanged     => (true, true, true),
            ModOptionChangeType.PriorityChanged      => (true, true, true),
            ModOptionChangeType.OptionAdded          => (true, true, true),
            ModOptionChangeType.OptionDeleted        => (true, true, false),
            ModOptionChangeType.OptionMoved          => (true, false, false),
            ModOptionChangeType.OptionFilesChanged   => (false, true, false),
            ModOptionChangeType.OptionFilesAdded     => (false, true, true),
            ModOptionChangeType.OptionSwapsChanged   => (false, true, false),
            ModOptionChangeType.OptionMetaChanged    => (false, true, false),
            ModOptionChangeType.DisplayChange        => (false, false, false),
            ModOptionChangeType.DefaultOptionChanged => (true, false, false),
            _                                        => (false, false, false),
        };
    }
}
