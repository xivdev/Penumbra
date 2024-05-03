using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Services;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Mods.Manager.OptionEditor;

public sealed class SingleModGroupEditor(CommunicatorService communicator, SaveService saveService, Configuration config)
    : ModOptionEditor<SingleModGroup, SingleSubMod>(communicator, saveService, config), IService
{
    public void ChangeToMulti(SingleModGroup group)
    {
        var idx = group.GetIndex();
        var multiGroup = group.ConvertToMulti();
        group.Mod.Groups[idx] = multiGroup;
        SaveService.QueueSave(new ModSaveGroup(multiGroup, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupTypeChanged, multiGroup.Mod, multiGroup, null, null, -1);
    }

    protected override SingleModGroup CreateGroup(Mod mod, string newName, ModPriority priority, SaveType saveType = SaveType.ImmediateSync)
        => new(mod)
        {
            Name = newName,
            Priority = priority,
        };

    protected override SingleSubMod CloneOption(SingleModGroup group, IModOption option)
    {
        var newOption = new SingleSubMod(group)
        {
            Name = option.Name,
            Description = option.Description,
        };
        if (option is IModDataContainer data)
            SubMod.Clone(data, newOption);
        group.OptionData.Add(newOption);
        return newOption;
    }

    protected override void RemoveOption(SingleModGroup group, int optionIndex)
    {
        group.OptionData.RemoveAt(optionIndex);
        group.DefaultSettings = group.DefaultSettings.RemoveSingle(optionIndex);
    }

    protected override bool MoveOption(SingleModGroup group, int optionIdxFrom, int optionIdxTo)
    {
        if (!group.OptionData.Move(optionIdxFrom, optionIdxTo))
            return false;

        group.DefaultSettings = group.DefaultSettings.MoveSingle(optionIdxFrom, optionIdxTo);
        return true;
    }
}
