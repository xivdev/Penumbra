using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Services;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Mods.Manager.OptionEditor;

public sealed class CombiningModGroupEditor(CommunicatorService communicator, SaveService saveService, Configuration config)
    : ModOptionEditor<CombiningModGroup, CombiningSubMod>(communicator, saveService, config), IService
{
    protected override CombiningModGroup CreateGroup(Mod mod, string newName, ModPriority priority, SaveType saveType = SaveType.ImmediateSync)
        => new(mod)
        {
            Name     = newName,
            Priority = priority,
        };

    protected override CombiningSubMod? CloneOption(CombiningModGroup group, IModOption option)
    {
        if (group.OptionData.Count >= IModGroup.MaxCombiningOptions)
        {
            Penumbra.Log.Error(
                $"Could not add option {option.Name} to {group.Name} for mod {group.Mod.Name}, "
              + $"since only up to {IModGroup.MaxCombiningOptions} options are supported in one group.");
            return null;
        }

        var newOption = new CombiningSubMod(group)
        {
            Name        = option.Name,
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

    protected override void RemoveOption(CombiningModGroup group, int optionIndex)
    {
        var optionFlag = 1 << optionIndex;
        for (var i = group.Data.Count - 1; i >= 0; --i)
        {
            group.Data.RemoveAll()
            if ((i & optionFlag) == optionFlag)
                group.Data.RemoveAt(i);
        }

        group.OptionData.RemoveAt(optionIndex);
        group.DefaultSettings = group.DefaultSettings.RemoveBit(optionIndex);
    }

    protected override bool MoveOption(MultiModGroup group, int optionIdxFrom, int optionIdxTo)
    {
        if (!group.OptionData.Move(ref optionIdxFrom, ref optionIdxTo))
            return false;

        group.DefaultSettings = group.DefaultSettings.MoveBit(optionIdxFrom, optionIdxTo);
        return true;
    }
}
