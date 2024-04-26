using Dalamud.Interface.Internal.Notifications;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
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

public class ModOptionEditor(CommunicatorService communicator, SaveService saveService, Configuration config)
{
    /// <summary> Change the type of a group given by mod and index to type, if possible. </summary>
    public void ChangeModGroupType(Mod mod, int groupIdx, GroupType type)
    {
        var group = mod.Groups[groupIdx];
        if (group.Type == type)
            return;

        mod.Groups[groupIdx] = group.Convert(type);
        saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupTypeChanged, mod, groupIdx, -1, -1);
    }

    /// <summary> Change the settings stored as default options in a mod.</summary>
    public void ChangeModGroupDefaultOption(Mod mod, int groupIdx, Setting defaultOption)
    {
        var group = mod.Groups[groupIdx];
        if (group.DefaultSettings == defaultOption)
            return;

        group.DefaultSettings = defaultOption;
        saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.DefaultOptionChanged, mod, groupIdx, -1, -1);
    }

    /// <summary> Rename an option group if possible. </summary>
    public void RenameModGroup(Mod mod, int groupIdx, string newName)
    {
        var group   = mod.Groups[groupIdx];
        var oldName = group.Name;
        if (oldName == newName || !VerifyFileName(mod, group, newName, true))
            return;

        saveService.ImmediateDelete(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        _ = group switch
        {
            SingleModGroup s => s.Name = newName,
            MultiModGroup m  => m.Name = newName,
            _                => newName,
        };

        saveService.ImmediateSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupRenamed, mod, groupIdx, -1, -1);
    }

    /// <summary> Add a new, empty option group of the given type and name. </summary>
    public void AddModGroup(Mod mod, GroupType type, string newName, SaveType saveType = SaveType.ImmediateSync)
    {
        if (!VerifyFileName(mod, null, newName, true))
            return;

        var idx   = mod.Groups.Count;
        var group = ModGroup.Create(mod, type, newName);
        mod.Groups.Add(group);
        saveService.Save(saveType, new ModSaveGroup(mod, idx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupAdded, mod, idx, -1, -1);
    }

    /// <summary> Add a new mod, empty option group of the given type and name if it does not exist already. </summary>
    public (IModGroup, int, bool) FindOrAddModGroup(Mod mod, GroupType type, string newName, SaveType saveType = SaveType.ImmediateSync)
    {
        var idx = mod.Groups.IndexOf(g => g.Name == newName);
        if (idx >= 0)
            return (mod.Groups[idx], idx, false);

        AddModGroup(mod, type, newName, saveType);
        if (mod.Groups[^1].Name != newName)
            throw new Exception($"Could not create new mod group with name {newName}.");

        return (mod.Groups[^1], mod.Groups.Count - 1, true);
    }

    /// <summary> Delete a given option group. Fires an event to prepare before actually deleting. </summary>
    public void DeleteModGroup(Mod mod, int groupIdx)
    {
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, groupIdx, -1, -1);
        mod.Groups.RemoveAt(groupIdx);
        saveService.SaveAllOptionGroups(mod, false, config.ReplaceNonAsciiOnImport);
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupDeleted, mod, groupIdx, -1, -1);
    }

    /// <summary> Move the index of a given option group. </summary>
    public void MoveModGroup(Mod mod, int groupIdxFrom, int groupIdxTo)
    {
        if (!mod.Groups.Move(groupIdxFrom, groupIdxTo))
            return;

        saveService.SaveAllOptionGroups(mod, false, config.ReplaceNonAsciiOnImport);
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupMoved, mod, groupIdxFrom, -1, groupIdxTo);
    }

    /// <summary> Change the description of the given option group. </summary>
    public void ChangeGroupDescription(Mod mod, int groupIdx, string newDescription)
    {
        var group = mod.Groups[groupIdx];
        if (group.Description == newDescription)
            return;

        group.Description = newDescription;
        saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.DisplayChange, mod, groupIdx, -1, -1);
    }

    /// <summary> Change the description of the given option. </summary>
    public void ChangeOptionDescription(Mod mod, int groupIdx, int optionIdx, string newDescription)
    {
        var option = mod.Groups[groupIdx].Options[optionIdx];
        if (option.Description == newDescription)
            return;

        option.Description = newDescription;
        saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.DisplayChange, mod, groupIdx, optionIdx, -1);
    }

    /// <summary> Change the internal priority of the given option group. </summary>
    public void ChangeGroupPriority(Mod mod, int groupIdx, ModPriority newPriority)
    {
        var group = mod.Groups[groupIdx];
        if (group.Priority == newPriority)
            return;

        group.Priority = newPriority;
        saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.PriorityChanged, mod, groupIdx, -1, -1);
    }

    /// <summary> Change the internal priority of the given option. </summary>
    public void ChangeOptionPriority(Mod mod, int groupIdx, int optionIdx, ModPriority newPriority)
    {
        switch (mod.Groups[groupIdx])
        {
            case MultiModGroup multi:
                if (multi.OptionData[optionIdx].Priority == newPriority)
                    return;

                multi.OptionData[optionIdx].Priority = newPriority;
                saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
                communicator.ModOptionChanged.Invoke(ModOptionChangeType.PriorityChanged, mod, groupIdx, optionIdx, -1);
                return;
        }
    }

    /// <summary> Rename the given option. </summary>
    public void RenameOption(Mod mod, int groupIdx, int optionIdx, string newName)
    {
        var option = mod.Groups[groupIdx].Options[optionIdx];
        if (option.Name == newName)
            return;

        option.Name = newName;

        saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.DisplayChange, mod, groupIdx, optionIdx, -1);
    }

    /// <summary> Add a new empty option of the given name for the given group. </summary>
    public int AddOption(Mod mod, int groupIdx, string newName, SaveType saveType = SaveType.Queue)
    {
        var group = mod.Groups[groupIdx];
        var idx   = group.AddOption(mod, newName);
        if (idx < 0)
            return -1;

        saveService.Save(saveType, new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionAdded, mod, groupIdx, idx, -1);
        return idx;
    }

    /// <summary> Add a new empty option of the given name for the given group if it does not exist already. </summary>
    public (IModOption, int, bool) FindOrAddOption(Mod mod, int groupIdx, string newName, SaveType saveType = SaveType.Queue)
    {
        var group = mod.Groups[groupIdx];
        var idx   = group.Options.IndexOf(o => o.Name == newName);
        if (idx >= 0)
            return (group.Options[idx], idx, false);

        idx = group.AddOption(mod, newName);
        if (idx < 0)
            throw new Exception($"Could not create new option with name {newName} in {group.Name}.");

        saveService.Save(saveType, new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionAdded, mod, groupIdx, idx, -1);
        return (group.Options[idx], idx, true);
    }

    /// <summary> Add an existing option to a given group. </summary> 
    public void AddOption(Mod mod, int groupIdx, IModOption option)
    {
        var group = mod.Groups[groupIdx];
        int idx;
        switch (group)
        {
            case MultiModGroup { OptionData.Count: >= IModGroup.MaxMultiOptions }:
                Penumbra.Log.Error(
                    $"Could not add option {option.Name} to {group.Name} for mod {mod.Name}, "
                  + $"since only up to {IModGroup.MaxMultiOptions} options are supported in one group.");
                return;
            case SingleModGroup s:
            {
                idx = s.OptionData.Count;
                var newOption = new SingleSubMod(s.Mod, s)
                {
                    Name        = option.Name,
                    Description = option.Description,
                };
                if (option is IModDataContainer data)
                    SubMod.Clone(data, newOption);
                s.OptionData.Add(newOption);
                break;
            }
            case MultiModGroup m:
            {
                idx = m.OptionData.Count;
                var newOption = new MultiSubMod(m.Mod, m)
                {
                    Name        = option.Name,
                    Description = option.Description,
                    Priority    = option is MultiSubMod s ? s.Priority : ModPriority.Default,
                };
                if (option is IModDataContainer data)
                    SubMod.Clone(data, newOption);
                m.OptionData.Add(newOption);
                break;
            }
            default: return;
        }

        saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionAdded, mod, groupIdx, idx, -1);
    }

    /// <summary> Delete the given option from the given group. </summary>
    public void DeleteOption(Mod mod, int groupIdx, int optionIdx)
    {
        var group = mod.Groups[groupIdx];
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, groupIdx, optionIdx, -1);
        switch (group)
        {
            case SingleModGroup s:
                s.OptionData.RemoveAt(optionIdx);

                break;
            case MultiModGroup m:
                m.OptionData.RemoveAt(optionIdx);
                break;
        }

        saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionDeleted, mod, groupIdx, optionIdx, -1);
    }

    /// <summary> Move an option inside the given option group. </summary>
    public void MoveOption(Mod mod, int groupIdx, int optionIdxFrom, int optionIdxTo)
    {
        var group = mod.Groups[groupIdx];
        if (!group.MoveOption(optionIdxFrom, optionIdxTo))
            return;

        saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMoved, mod, groupIdx, optionIdxFrom, optionIdxTo);
    }

    /// <summary> Set the meta manipulations for a given option. Replaces existing manipulations. </summary>
    public void OptionSetManipulations(Mod mod, int groupIdx, int dataContainerIdx, HashSet<MetaManipulation> manipulations,
        SaveType saveType = SaveType.Queue)
    {
        var subMod = GetSubMod(mod, groupIdx, dataContainerIdx);
        if (subMod.Manipulations.Count == manipulations.Count
         && subMod.Manipulations.All(m => manipulations.TryGetValue(m, out var old) && old.EntryEquals(m)))
            return;

        communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, groupIdx, dataContainerIdx, -1);
        subMod.Manipulations.SetTo(manipulations);
        saveService.Save(saveType, new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMetaChanged, mod, groupIdx, dataContainerIdx, -1);
    }

    /// <summary> Set the file redirections for a given option. Replaces existing redirections. </summary>
    public void OptionSetFiles(Mod mod, int groupIdx, int dataContainerIdx, IReadOnlyDictionary<Utf8GamePath, FullPath> replacements,
        SaveType saveType = SaveType.Queue)
    {
        var subMod = GetSubMod(mod, groupIdx, dataContainerIdx);
        if (subMod.Files.SetEquals(replacements))
            return;

        communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, groupIdx, dataContainerIdx, -1);
        subMod.Files.SetTo(replacements);
        saveService.Save(saveType, new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionFilesChanged, mod, groupIdx, dataContainerIdx, -1);
    }

    /// <summary> Add additional file redirections to a given option, keeping already existing ones. Only fires an event if anything is actually added.</summary>
    public void OptionAddFiles(Mod mod, int groupIdx, int dataContainerIdx, IReadOnlyDictionary<Utf8GamePath, FullPath> additions)
    {
        var subMod   = GetSubMod(mod, groupIdx, dataContainerIdx);
        var oldCount = subMod.Files.Count;
        subMod.Files.AddFrom(additions);
        if (oldCount != subMod.Files.Count)
        {
            saveService.QueueSave(new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
            communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionFilesAdded, mod, groupIdx, dataContainerIdx, -1);
        }
    }

    /// <summary> Set the file swaps for a given option. Replaces existing swaps. </summary>
    public void OptionSetFileSwaps(Mod mod, int groupIdx, int dataContainerIdx, IReadOnlyDictionary<Utf8GamePath, FullPath> swaps,
        SaveType saveType = SaveType.Queue)
    {
        var subMod = GetSubMod(mod, groupIdx, dataContainerIdx);
        if (subMod.FileSwaps.SetEquals(swaps))
            return;

        communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, groupIdx, dataContainerIdx, -1);
        subMod.FileSwaps.SetTo(swaps);
        saveService.Save(saveType, new ModSaveGroup(mod, groupIdx, config.ReplaceNonAsciiOnImport));
        communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionSwapsChanged, mod, groupIdx, dataContainerIdx, -1);
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
            Penumbra.Messager.NotificationMessage(
                $"Could not name option {newName} because option with same filename {path} already exists.",
                NotificationType.Warning, false);

        return false;
    }

    /// <summary> Get the correct option for the given group and option index. </summary>
    private static IModDataContainer GetSubMod(Mod mod, int groupIdx, int dataContainerIdx)
    {
        if (groupIdx == -1 && dataContainerIdx == 0)
            return mod.Default;

        return mod.Groups[groupIdx].DataContainers[dataContainerIdx];
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
