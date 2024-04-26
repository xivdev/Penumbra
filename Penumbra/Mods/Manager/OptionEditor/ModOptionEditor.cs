using OtterGui;
using OtterGui.Classes;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Mods.Manager.OptionEditor;

public abstract class ModOptionEditor<TGroup, TOption>(
    CommunicatorService communicator,
    SaveService saveService,
    Configuration config)
    where TGroup : class, IModGroup
    where TOption : class, IModOption
{
    protected readonly CommunicatorService Communicator = communicator;
    protected readonly SaveService SaveService = saveService;
    protected readonly Configuration Config = config;

    /// <summary> Add a new, empty option group of the given type and name. </summary>
    public TGroup? AddModGroup(Mod mod, string newName, SaveType saveType = SaveType.ImmediateSync)
    {
        if (!ModGroupEditor.VerifyFileName(mod, null, newName, true))
            return null;

        var maxPriority = mod.Groups.Count == 0 ? ModPriority.Default : mod.Groups.Max(o => o.Priority) + 1;
        var group = CreateGroup(mod, newName, maxPriority);
        mod.Groups.Add(group);
        SaveService.Save(saveType, new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupAdded, mod, group, null, null, -1);
        return group;
    }

    /// <summary> Add a new mod, empty option group of the given type and name if it does not exist already. </summary>
    public (TGroup, int, bool) FindOrAddModGroup(Mod mod, string newName, SaveType saveType = SaveType.ImmediateSync)
    {
        var idx = mod.Groups.IndexOf(g => g.Name == newName);
        if (idx >= 0)
        {
            var existingGroup = mod.Groups[idx] as TGroup
             ?? throw new Exception($"Mod group with name {newName} exists, but is of the wrong type.");
            return (existingGroup, idx, false);
        }

        idx = mod.Groups.Count;
        if (AddModGroup(mod, newName, saveType) is not { } group)
            throw new Exception($"Could not create new mod group with name {newName}.");

        return (group, idx, true);
    }

    /// <summary> Add a new empty option of the given name for the given group. </summary>
    public TOption? AddOption(TGroup group, string newName, SaveType saveType = SaveType.Queue)
    {
        if (group.AddOption(newName) is not TOption option)
            return null;

        SaveService.Save(saveType, new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionAdded, group.Mod, group, option, null, -1);
        return option;
    }

    /// <summary> Add a new empty option of the given name for the given group if it does not exist already. </summary>
    public (TOption, int, bool) FindOrAddOption(TGroup group, string newName, SaveType saveType = SaveType.Queue)
    {
        var idx = group.Options.IndexOf(o => o.Name == newName);
        if (idx >= 0)
        {
            var existingOption = group.Options[idx] as TOption
             ?? throw new Exception($"Mod option with name {newName} exists, but is of the wrong type."); // Should never happen.
            return (existingOption, idx, false);
        }

        if (AddOption(group, newName, saveType) is not { } option)
            throw new Exception($"Could not create new option with name {newName} in {group.Name}.");

        return (option, idx, true);
    }

    /// <summary> Add an existing option to a given group. </summary> 
    public TOption? AddOption(TGroup group, IModOption option)
    {
        if (CloneOption(group, option) is not { } clonedOption)
            return null;

        SaveService.QueueSave(new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionAdded, group.Mod, group, clonedOption, null, -1);
        return clonedOption;
    }

    /// <summary> Delete the given option from the given group. </summary>
    public void DeleteOption(TOption option)
    {
        var mod = option.Mod;
        var group = option.Group;
        var optionIdx = option.GetIndex();
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.PrepareChange, mod, group, option, null, -1);
        RemoveOption((TGroup)group, optionIdx);
        SaveService.QueueSave(new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionDeleted, mod, group, null, null, optionIdx);
    }

    /// <summary> Move an option inside the given option group. </summary>
    public void MoveOption(TOption option, int optionIdxTo)
    {
        var idx = option.GetIndex();
        var group = (TGroup)option.Group;
        if (!MoveOption(group, idx, optionIdxTo))
            return;

        SaveService.QueueSave(new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMoved, group.Mod, group, option, null, idx);
    }

    protected abstract TGroup CreateGroup(Mod mod, string newName, ModPriority priority, SaveType saveType = SaveType.ImmediateSync);
    protected abstract TOption? CloneOption(TGroup group, IModOption option);
    protected abstract void RemoveOption(TGroup group, int optionIndex);
    protected abstract bool MoveOption(TGroup group, int optionIdxFrom, int optionIdxTo);
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
            ModOptionChangeType.GroupRenamed => (true, false, false),
            ModOptionChangeType.GroupAdded => (true, false, false),
            ModOptionChangeType.GroupDeleted => (true, true, false),
            ModOptionChangeType.GroupMoved => (true, false, false),
            ModOptionChangeType.GroupTypeChanged => (true, true, true),
            ModOptionChangeType.PriorityChanged => (true, true, true),
            ModOptionChangeType.OptionAdded => (true, true, true),
            ModOptionChangeType.OptionDeleted => (true, true, false),
            ModOptionChangeType.OptionMoved => (true, false, false),
            ModOptionChangeType.OptionFilesChanged => (false, true, false),
            ModOptionChangeType.OptionFilesAdded => (false, true, true),
            ModOptionChangeType.OptionSwapsChanged => (false, true, false),
            ModOptionChangeType.OptionMetaChanged => (false, true, false),
            ModOptionChangeType.DisplayChange => (false, false, false),
            ModOptionChangeType.DefaultOptionChanged => (true, false, false),
            _ => (false, false, false),
        };
    }
}
