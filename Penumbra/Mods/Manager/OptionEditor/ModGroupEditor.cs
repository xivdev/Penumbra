using Dalamud.Interface.ImGuiNotification;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Files;
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
    GroupIdentifierChanged,
    GroupTypeChanged,
    PriorityChanged,
    OptionRenamed,
    OptionAdded,
    OptionDeleted,
    OptionMoved,
    OptionFilesChanged,
    OptionFilesAdded,
    OptionSwapsChanged,
    OptionMetaChanged,
    OptionIdentifierChanged,
    DisplayChange,
    PrepareChange,
    PrepareGroupDeletion,
    DefaultOptionChanged,
    ConditionChanged,
}

public class ModGroupEditor(
    SingleModGroupEditor singleEditor,
    MultiModGroupEditor multiEditor,
    ImcModGroupEditor imcEditor,
    CombiningModGroupEditor combiningEditor,
    CommunicatorService communicator,
    SaveService saveService) : IService
{
    public SingleModGroupEditor SingleEditor
        => singleEditor;

    public MultiModGroupEditor MultiEditor
        => multiEditor;

    public ImcModGroupEditor ImcEditor
        => imcEditor;

    public CombiningModGroupEditor CombiningEditor
        => combiningEditor;

    /// <summary> Change the settings stored as default options in a mod.</summary>
    public void ChangeModGroupDefaultOption(IModGroup group, Setting defaultOption)
    {
        if (group.DefaultSettings == defaultOption)
            return;

        group.DefaultSettings = defaultOption;
        saveService.QueueSave(group);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.DefaultOptionChanged, group.Mod, group, null,
            null, group.Id, -1));
    }

    /// <summary> Rename an option group if possible. </summary>
    public void RenameModGroup(IModGroup group, string newName)
    {
        var oldName = group.Name;
        if (oldName == newName || !VerifyFileName(group.Mod, group, newName, true))
            return;

        group.Name = newName;
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.GroupRenamed, group.Mod, group, null, null,
            group.Id, -1));
        saveService.ImmediateSave(group);
    }

    /// <summary> Delete a given option group. Fires an event to prepare before actually deleting. </summary>
    public void DeleteModGroup(IModGroup group)
    {
        var mod = group.Mod;
        var idx = group.Index;
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.PrepareGroupDeletion, mod, group, null, null,
            group.Id, -1));
        mod.Groups.RemoveAt(idx);
        saveService.ImmediateSaveSync(mod);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.GroupDeleted, mod, null, null, null, group.Id,
            idx));
    }

    /// <summary> Move the index of a given option group. </summary>
    public void MoveModGroup(IModGroup group, int groupIdxTo)
    {
        var mod     = group.Mod;
        var idxFrom = group.Index;
        if (!mod.Groups.Move(ref idxFrom, ref groupIdxTo))
            return;

        saveService.ImmediateSaveSync(group);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.GroupMoved, mod, group, null, null, group.Id,
            idxFrom));
    }

    /// <summary> Force the GUID of an object to a specific value. Empty GUIDs are not allowed. </summary>
    /// <param name="object"> The object to edit. </param>
    /// <param name="newGuid"> The desired new GUID. </param>
    /// <returns> False if the new GUID is already used in this mod or if it is empty, true otherwise. </returns>
    public bool ForceIdentifier(IModObject @object, Guid newGuid)
    {
        if (newGuid == Guid.Empty || @object.Mod.StableIdentifier == newGuid || @object.Mod.SubObjects.ContainsKey(newGuid))
            return false;

        var oldGuid = @object.Id;
        @object.Id = newGuid;
        saveService.ImmediateSaveSync(@object);
        var changeType = @object is IModGroup ? ModOptionChangeType.GroupIdentifierChanged : ModOptionChangeType.OptionIdentifierChanged;
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(changeType, @object.Mod, @object.Group, @object as IModOption, null,
            oldGuid, -1));
        return true;
    }

    public void SetCondition(IModObject @object, ICondition<ModSettingContext>? condition, bool force)
    {
        if (!force && (condition?.Equals(@object.Condition) ?? @object.Condition is null))
            return;

        @object.Condition = condition;
        saveService.QueueSave(@object);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.ConditionChanged, @object.Mod, @object.Group,
            @object as IModOption, null, @object.Id, -1));
    }

    public void SetPage(IModGroup group, int page)
    {
        if (group.Page == page)
            return;

        group.Page = page;
        saveService.QueueSave(group);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.DisplayChange, group.Mod, group,
            null, null, group.Id, -1));
    }

    public void SetParent(IModGroup group, IModObject? parent)
    {
        if (ReferenceEquals(group.ParentSetting, parent))
            return;

        group.ParentSetting = parent;
        saveService.QueueSave(group);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.DisplayChange, group.Mod, group.Group, null,
            null, group.Id, -1));
    }

    public void SetLayout(IModObject @object, ModSettingsLayout layout)
    {
        layout = layout.Reduce(@object);
        if (@object.Layout == layout)
            return;

        @object.Layout = layout;
        saveService.QueueSave(@object);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.DisplayChange, @object.Mod, @object.Group,
            @object as IModOption, null, @object.Id, -1));
    }

    public void SetColor(IModOption option, int colorType)
    {
        var color = IModOption.ConvertColor(colorType);
        if (color == option.Color)
            return;

        option.Color = color;
        saveService.QueueSave(option);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.DisplayChange, option.Mod, option.Group,
            option, null, option.Id, -1));
    }

    /// <summary> Change the internal priority of the given option group. </summary>
    public void ChangeGroupPriority(IModGroup group, ModPriority newPriority)
    {
        if (group.Priority == newPriority)
            return;

        group.Priority = newPriority;
        saveService.QueueSave(group);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.PriorityChanged, group.Mod, group, null, null,
            group.Id, -1));
    }

    /// <summary> Change the description of the given option group. </summary>
    public void ChangeGroupDescription(IModGroup group, string newDescription)
    {
        if (group.Description == newDescription)
            return;

        group.Description = newDescription;
        saveService.QueueSave(group);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.DisplayChange, group.Mod, group, null, null,
            group.Id, -1));
    }

    /// <summary> Rename the given option. </summary>
    public void RenameOption(IModOption option, string newName)
    {
        var oldName = option.Name;
        if (oldName == newName)
            return;

        option.Name = newName;
        saveService.QueueSave(option);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.OptionRenamed, option.Mod, option.Group, option,
            null, option.Id, -1));
    }

    /// <summary> Change the description of the given option. </summary>
    public void ChangeOptionDescription(IModOption option, string newDescription)
    {
        if (option.Description == newDescription)
            return;

        option.Description = newDescription;
        saveService.QueueSave(option);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.DisplayChange, option.Mod, option.Group, option,
            null, option.Id, -1));
    }

    /// <summary> Set the meta manipulations for a given option. Replaces existing manipulations. </summary>
    public void SetManipulations(IModDataContainer subMod, MetaDictionary manipulations, SaveType saveType = SaveType.Queue)
    {
        if (subMod.Manipulations.Equals(manipulations))
            return;

        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.PrepareChange, (Mod)subMod.Mod, subMod.Group,
            null, subMod, Guid.Empty, -1));
        subMod.Manipulations.SetTo(manipulations);
        saveService.Save(saveType, subMod);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.OptionMetaChanged, (Mod)subMod.Mod,
            subMod.Group, null, subMod, Guid.Empty, -1));
    }

    /// <summary> Set the file redirections for a given option. Replaces existing redirections. </summary>
    public void SetFiles(IModDataContainer subMod, IReadOnlyDictionary<Utf8GamePath, FullPath> replacements, SaveType saveType = SaveType.Queue)
    {
        if (subMod.Files.SetEquals(replacements))
            return;

        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.PrepareChange, (Mod)subMod.Mod, subMod.Group,
            null, subMod, Guid.Empty, -1));
        subMod.Files.SetTo(replacements);
        saveService.Save(saveType, subMod);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.OptionFilesChanged, (Mod)subMod.Mod,
            subMod.Group, null, subMod, Guid.Empty, -1));
    }

    /// <summary> Forces a file save of the given container's group. </summary>
    public void ForceSave(IModDataContainer subMod, SaveType saveType = SaveType.Queue)
        => saveService.Save(saveType, subMod);

    /// <summary> Add additional file redirections to a given option, keeping already existing ones. Only fires an event if anything is actually added.</summary>
    public void AddFiles(IModDataContainer subMod, IReadOnlyDictionary<Utf8GamePath, FullPath> additions)
    {
        var oldCount = subMod.Files.Count;
        subMod.Files.AddFrom(additions);
        if (oldCount != subMod.Files.Count)
        {
            saveService.QueueSave(subMod);
            communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.OptionFilesAdded, (Mod)subMod.Mod,
                subMod.Group, null, subMod, Guid.Empty, -1));
        }
    }

    /// <summary> Set the file swaps for a given option. Replaces existing swaps. </summary>
    public void SetFileSwaps(IModDataContainer subMod, IReadOnlyDictionary<Utf8GamePath, FullPath> swaps, SaveType saveType = SaveType.Queue)
    {
        if (subMod.FileSwaps.SetEquals(swaps))
            return;

        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.PrepareChange, (Mod)subMod.Mod, subMod.Group,
            null, subMod, Guid.Empty, -1));
        subMod.FileSwaps.SetTo(swaps);
        saveService.Save(saveType, subMod);
        communicator.ModOptionChanged.Invoke(new ModOptionChanged.Arguments(ModOptionChangeType.OptionSwapsChanged, (Mod)subMod.Mod,
            subMod.Group, null, subMod, Guid.Empty, -1));
    }

    /// <summary> Verify that a new option group name is unique in this mod. </summary>
    public static bool VerifyFileName(Mod mod, IModGroup? group, string newName, bool message)
    {
        var path = newName.RemoveInvalidPathSymbols();
        if (path.Length is not 0
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
            case CombiningSubMod c:
                CombiningEditor.DeleteOption(c);
                return;
        }
    }

    public IModOption? AddOption(IModGroup group, IModOption option)
        => group switch
        {
            SingleModGroup s    => SingleEditor.AddOption(s, option),
            MultiModGroup m     => MultiEditor.AddOption(m, option),
            ImcModGroup i       => ImcEditor.AddOption(i, option),
            CombiningModGroup c => CombiningEditor.AddOption(c, option),
            _                   => null,
        };

    public IModOption? AddOption(IModGroup group, string newName)
        => group switch
        {
            SingleModGroup s    => SingleEditor.AddOption(s, newName),
            MultiModGroup m     => MultiEditor.AddOption(m, newName),
            ImcModGroup i       => ImcEditor.AddOption(i, newName),
            CombiningModGroup c => CombiningEditor.AddOption(c, newName),
            _                   => null,
        };

    public IModGroup? AddModGroup(Mod mod, GroupType type, string newName, SaveType saveType = SaveType.ImmediateSync)
        => type switch
        {
            GroupType.Single    => SingleEditor.AddModGroup(mod, newName, saveType),
            GroupType.Multi     => MultiEditor.AddModGroup(mod, newName, saveType),
            GroupType.Imc       => ImcEditor.AddModGroup(mod, newName, default, default, saveType),
            GroupType.Combining => CombiningEditor.AddModGroup(mod, newName, saveType),
            _                   => null,
        };

    public (IModGroup?, bool) FindOrAddModGroup(Mod mod, GroupType type, string name, SaveType saveType = SaveType.ImmediateSync)
        => type switch
        {
            GroupType.Single    => SingleEditor.FindOrAddModGroup(mod, name, saveType),
            GroupType.Multi     => MultiEditor.FindOrAddModGroup(mod, name, saveType),
            GroupType.Imc       => ImcEditor.FindOrAddModGroup(mod, name, saveType),
            GroupType.Combining => CombiningEditor.FindOrAddModGroup(mod, name, saveType),
            _                   => (null, false),
        };

    public (IModOption?, bool) FindOrAddOption(IModGroup group, string name, SaveType saveType = SaveType.ImmediateSync)
        => group switch
        {
            SingleModGroup s    => SingleEditor.FindOrAddOption(s, name, saveType),
            MultiModGroup m     => MultiEditor.FindOrAddOption(m, name, saveType),
            ImcModGroup i       => ImcEditor.FindOrAddOption(i, name, saveType),
            CombiningModGroup c => CombiningEditor.FindOrAddOption(c, name, saveType),
            _                   => (null, false),
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
            case CombiningSubMod c:
                CombiningEditor.MoveOption(c, toIdx);
                return;
        }
    }
}
