using Dalamud.Interface.Internal.Notifications;
using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Mods.Manager.OptionEditor;

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

public class ModGroupEditor(
    SingleModGroupEditor singleEditor,
    MultiModGroupEditor multiEditor,
    ImcModGroupEditor imcEditor,
    CommunicatorService Communicator,
    SaveService SaveService,
    Configuration Config) : IService
{
    public SingleModGroupEditor SingleEditor
        => singleEditor;

    public MultiModGroupEditor MultiEditor
        => multiEditor;

    public ImcModGroupEditor ImcEditor
        => imcEditor;

    /// <summary> Change the settings stored as default options in a mod.</summary>
    public void ChangeModGroupDefaultOption(IModGroup group, Setting defaultOption)
    {
        if (group.DefaultSettings == defaultOption)
            return;

        group.DefaultSettings = defaultOption;
        SaveService.QueueSave(new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.DefaultOptionChanged, group.Mod, group, null, null, -1);
    }

    /// <summary> Rename an option group if possible. </summary>
    public void RenameModGroup(IModGroup group, string newName)
    {
        var oldName = group.Name;
        if (oldName == newName || !VerifyFileName(group.Mod, group, newName, true))
            return;

        SaveService.ImmediateDelete(new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        group.Name = newName;
        SaveService.ImmediateSave(new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupRenamed, group.Mod, group, null, null, -1);
    }

    /// <summary> Delete a given option group. Fires an event to prepare before actually deleting. </summary>
    public void DeleteModGroup(IModGroup group)
    {
        var mod = group.Mod;
        var idx = group.GetIndex();
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, group, null, null, -1);
        mod.Groups.RemoveAt(idx);
        SaveService.SaveAllOptionGroups(mod, false, Config.ReplaceNonAsciiOnImport);
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupDeleted, mod, null, null, null, idx);
    }

    /// <summary> Move the index of a given option group. </summary>
    public void MoveModGroup(IModGroup group, int groupIdxTo)
    {
        var mod = group.Mod;
        var idxFrom = group.GetIndex();
        if (!mod.Groups.Move(idxFrom, groupIdxTo))
            return;

        SaveService.SaveAllOptionGroups(mod, false, Config.ReplaceNonAsciiOnImport);
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupMoved, mod, group, null, null, idxFrom);
    }

    /// <summary> Change the internal priority of the given option group. </summary>
    public void ChangeGroupPriority(IModGroup group, ModPriority newPriority)
    {
        if (group.Priority == newPriority)
            return;

        group.Priority = newPriority;
        SaveService.QueueSave(new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.PriorityChanged, group.Mod, group, null, null, -1);
    }

    /// <summary> Change the description of the given option group. </summary>
    public void ChangeGroupDescription(IModGroup group, string newDescription)
    {
        if (group.Description == newDescription)
            return;

        group.Description = newDescription;
        SaveService.QueueSave(new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.DisplayChange, group.Mod, group, null, null, -1);
    }

    /// <summary> Rename the given option. </summary>
    public void RenameOption(IModOption option, string newName)
    {
        if (option.Name == newName)
            return;

        option.Name = newName;
        SaveService.QueueSave(new ModSaveGroup(option.Group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.DisplayChange, option.Mod, option.Group, option, null, -1);
    }

    /// <summary> Change the description of the given option. </summary>
    public void ChangeOptionDescription(IModOption option, string newDescription)
    {
        if (option.Description == newDescription)
            return;

        option.Description = newDescription;
        SaveService.QueueSave(new ModSaveGroup(option.Group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.DisplayChange, option.Mod, option.Group, option, null, -1);
    }

    /// <summary> Set the meta manipulations for a given option. Replaces existing manipulations. </summary>
    public void SetManipulations(IModDataContainer subMod, HashSet<MetaManipulation> manipulations, SaveType saveType = SaveType.Queue)
    {
        if (subMod.Manipulations.Count == manipulations.Count
         && subMod.Manipulations.All(m => manipulations.TryGetValue(m, out var old) && old.EntryEquals(m)))
            return;

        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, (Mod)subMod.Mod, subMod.Group, null, subMod, -1);
        subMod.Manipulations.SetTo(manipulations);
        SaveService.Save(saveType, new ModSaveGroup(subMod, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMetaChanged, (Mod)subMod.Mod, subMod.Group, null, subMod, -1);
    }

    /// <summary> Set the file redirections for a given option. Replaces existing redirections. </summary>
    public void SetFiles(IModDataContainer subMod, IReadOnlyDictionary<Utf8GamePath, FullPath> replacements, SaveType saveType = SaveType.Queue)
    {
        if (subMod.Files.SetEquals(replacements))
            return;

        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, (Mod)subMod.Mod, subMod.Group, null, subMod, -1);
        subMod.Files.SetTo(replacements);
        SaveService.Save(saveType, new ModSaveGroup(subMod, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionFilesChanged, (Mod)subMod.Mod, subMod.Group, null, subMod, -1);
    }

    /// <summary> Add additional file redirections to a given option, keeping already existing ones. Only fires an event if anything is actually added.</summary>
    public void AddFiles(IModDataContainer subMod, IReadOnlyDictionary<Utf8GamePath, FullPath> additions)
    {
        var oldCount = subMod.Files.Count;
        subMod.Files.AddFrom(additions);
        if (oldCount != subMod.Files.Count)
        {
            SaveService.QueueSave(new ModSaveGroup(subMod, Config.ReplaceNonAsciiOnImport));
            Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionFilesAdded, (Mod)subMod.Mod, subMod.Group, null, subMod, -1);
        }
    }

    /// <summary> Set the file swaps for a given option. Replaces existing swaps. </summary>
    public void SetFileSwaps(IModDataContainer subMod, IReadOnlyDictionary<Utf8GamePath, FullPath> swaps, SaveType saveType = SaveType.Queue)
    {
        if (subMod.FileSwaps.SetEquals(swaps))
            return;

        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, (Mod)subMod.Mod, subMod.Group, null, subMod, -1);
        subMod.FileSwaps.SetTo(swaps);
        SaveService.Save(saveType, new ModSaveGroup(subMod, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionSwapsChanged, (Mod)subMod.Mod, subMod.Group, null, subMod, -1);
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

    public void DeleteOption(IModOption option)
    {
        switch (option)
        {
            case SingleSubMod s:
                SingleEditor.DeleteOption(s);
                return;
            case MultiSubMod m:
                MultiEditor.DeleteOption(m);
                return;
            case ImcSubMod i:
                ImcEditor.DeleteOption(i);
                return;
        }
    }

    public IModOption? AddOption(IModGroup group, IModOption option)
        => group switch
        {
            SingleModGroup s => SingleEditor.AddOption(s, option),
            MultiModGroup m => MultiEditor.AddOption(m, option),
            ImcModGroup i => ImcEditor.AddOption(i, option),
            _ => null,
        };

    public IModOption? AddOption(IModGroup group, string newName)
        => group switch
        {
            SingleModGroup s => SingleEditor.AddOption(s, newName),
            MultiModGroup m => MultiEditor.AddOption(m, newName),
            ImcModGroup i => ImcEditor.AddOption(i, newName),
            _ => null,
        };

    public IModGroup? AddModGroup(Mod mod, GroupType type, string newName, SaveType saveType = SaveType.ImmediateSync)
        => type switch
        {
            GroupType.Single => SingleEditor.AddModGroup(mod, newName, saveType),
            GroupType.Multi => MultiEditor.AddModGroup(mod, newName, saveType),
            GroupType.Imc => ImcEditor.AddModGroup(mod, newName, default, saveType),
            _ => null,
        };

    public (IModGroup?, int, bool) FindOrAddModGroup(Mod mod, GroupType type, string name, SaveType saveType = SaveType.ImmediateSync)
        => type switch
        {
            GroupType.Single => SingleEditor.FindOrAddModGroup(mod, name, saveType),
            GroupType.Multi => MultiEditor.FindOrAddModGroup(mod, name, saveType),
            GroupType.Imc => ImcEditor.FindOrAddModGroup(mod, name, saveType),
            _ => (null, -1, false),
        };

    public (IModOption?, int, bool) FindOrAddOption(IModGroup group, string name, SaveType saveType = SaveType.ImmediateSync)
        => group switch
        {
            SingleModGroup s => SingleEditor.FindOrAddOption(s, name, saveType),
            MultiModGroup m => MultiEditor.FindOrAddOption(m, name, saveType),
            ImcModGroup i => ImcEditor.FindOrAddOption(i, name, saveType),
            _ => (null, -1, false),
        };

    public void MoveOption(IModOption option, int toIdx)
    {
        switch (option)
        {
            case SingleSubMod s:
                SingleEditor.MoveOption(s, toIdx);
                return;
            case MultiSubMod m:
                MultiEditor.MoveOption(m, toIdx);
                return;
            case ImcSubMod i:
                ImcEditor.MoveOption(i, toIdx);
                return;
        }
    }
}
