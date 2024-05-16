using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Services;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Mods.Manager.OptionEditor;

public sealed class ImcModGroupEditor(CommunicatorService communicator, SaveService saveService, Configuration config)
    : ModOptionEditor<ImcModGroup, ImcSubMod>(communicator, saveService, config), IService
{
    /// <summary> Add a new, empty imc group with the given manipulation data. </summary>
    public ImcModGroup? AddModGroup(Mod mod, string newName, ImcManipulation manip, SaveType saveType = SaveType.ImmediateSync)
    {
        if (!ModGroupEditor.VerifyFileName(mod, null, newName, true))
            return null;

        var maxPriority = mod.Groups.Count == 0 ? ModPriority.Default : mod.Groups.Max(o => o.Priority) + 1;
        var group       = CreateGroup(mod, newName, manip, maxPriority);
        mod.Groups.Add(group);
        SaveService.Save(saveType, new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupAdded, mod, group, null, null, -1);
        return group;
    }

    protected override ImcModGroup CreateGroup(Mod mod, string newName, ModPriority priority, SaveType saveType = SaveType.ImmediateSync)
        => new(mod)
        {
            Name     = newName,
            Priority = priority,
        };


    private static ImcModGroup CreateGroup(Mod mod, string newName, ImcManipulation manip, ModPriority priority,
        SaveType saveType = SaveType.ImmediateSync)
        => new(mod)
        {
            Name         = newName,
            Priority     = priority,
            ObjectType   = manip.ObjectType,
            EquipSlot    = manip.EquipSlot,
            BodySlot     = manip.BodySlot,
            PrimaryId    = manip.PrimaryId,
            SecondaryId  = manip.SecondaryId.Id,
            Variant      = manip.Variant,
            DefaultEntry = manip.Entry,
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
