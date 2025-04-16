using OtterGui;
using OtterGui.Classes;
using OtterGui.Extensions;
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
        => throw new NotImplementedException();

    protected override void RemoveOption(CombiningModGroup group, int optionIndex)
    {
        if (group.OptionData.RemoveWithPowerSet(group.Data, optionIndex))
            group.DefaultSettings.RemoveBit(optionIndex);
    }

    protected override bool MoveOption(CombiningModGroup group, int optionIdxFrom, int optionIdxTo)
    {
        if (!group.OptionData.MoveWithPowerSet(group.Data, ref optionIdxFrom, ref optionIdxTo))
            return false;

        group.DefaultSettings.MoveBit(optionIdxFrom, optionIdxTo);
        return true;
    }

    public void SetDisplayName(CombinedDataContainer container, string name, SaveType saveType = SaveType.Queue)
    {
        if (container.Name == name)
            return;

        container.Name = name;
        SaveService.Save(saveType, new ModSaveGroup(container.Group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.DisplayChange, container.Group.Mod, container.Group, null, null, -1);
    }
}
