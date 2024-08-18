using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Services;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Mods.Manager.OptionEditor;

public sealed class MultiModGroupEditor(CommunicatorService communicator, SaveService saveService, Configuration config)
    : ModOptionEditor<MultiModGroup, MultiSubMod>(communicator, saveService, config), IService
{
    public void ChangeToSingle(MultiModGroup group)
    {
        var idx = group.GetIndex();
        var singleGroup = group.ConvertToSingle();
        group.Mod.Groups[idx] = singleGroup;
        SaveService.QueueSave(new ModSaveGroup(singleGroup, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupTypeChanged, singleGroup.Mod, singleGroup, null, null, -1);
    }

    /// <summary> Change the internal priority of the given option. </summary>
    public void ChangeOptionPriority(MultiSubMod option, ModPriority newPriority)
    {
        if (option.Priority == newPriority)
            return;

        option.Priority = newPriority;
        SaveService.QueueSave(new ModSaveGroup(option.Group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.PriorityChanged, option.Mod, option.Group, option, null, -1);
    }

    protected override MultiModGroup CreateGroup(Mod mod, string newName, ModPriority priority, SaveType saveType = SaveType.ImmediateSync)
        => new(mod)
        {
            Name = newName,
            Priority = priority,
        };

    protected override MultiSubMod? CloneOption(MultiModGroup group, IModOption option)
    {
        if (group.OptionData.Count >= IModGroup.MaxMultiOptions)
        {
            Penumbra.Log.Error(
                $"Could not add option {option.Name} to {group.Name} for mod {group.Mod.Name}, "
              + $"since only up to {IModGroup.MaxMultiOptions} options are supported in one group.");
            return null;
        }

        var newOption = new MultiSubMod(group)
        {
            Name = option.Name,
            Description = option.Description,
        };

        if (option is IModDataContainer data)
        {
            SubMod.Clone(data, newOption);
            if (option is MultiSubMod m)
                newOption.Priority = m.Priority;
            else
                newOption.Priority = new ModPriority(group.OptionData.Max(o => o.Priority.Value) + 1);
        }

        group.OptionData.Add(newOption);
        return newOption;
    }

    protected override void RemoveOption(MultiModGroup group, int optionIndex)
    {
        group.OptionData.RemoveAt(optionIndex);
        group.DefaultSettings = group.DefaultSettings.RemoveBit(optionIndex);
    }

    protected override bool MoveOption(MultiModGroup group, int optionIdxFrom, int optionIdxTo)
    {
        if (!group.OptionData.Move(optionIdxFrom, optionIdxTo))
            return false;

        group.DefaultSettings = group.DefaultSettings.MoveBit(optionIdxFrom, optionIdxTo);
        return true;
    }
}
