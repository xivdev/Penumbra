using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Services;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public sealed class ImcModGroupEditor(CommunicatorService communicator, SaveService saveService, Configuration config)
    : ModOptionEditor<ImcModGroup, ImcSubMod>(communicator, saveService, config), IService
{
    protected override ImcModGroup CreateGroup(Mod mod, string newName, ModPriority priority, SaveType saveType = SaveType.ImmediateSync)
        => new(mod)
        {
            Name     = newName,
            Priority = priority,
        };

    protected override ImcSubMod? CloneOption(ImcModGroup group, IModOption option)
        => null;

    protected override void RemoveOption(ImcModGroup group, int optionIndex)
    {
        group.OptionData.RemoveAt(optionIndex);
        group.DefaultSettings = group.FixSetting(group.DefaultSettings);
    }

    protected override bool MoveOption(ImcModGroup group, int optionIdxFrom, int optionIdxTo)
    {
        if (!group.OptionData.Move(optionIdxFrom, optionIdxTo))
            return false;

        group.DefaultSettings = group.DefaultSettings.MoveBit(optionIdxFrom, optionIdxTo);
        return true;
    }
}
