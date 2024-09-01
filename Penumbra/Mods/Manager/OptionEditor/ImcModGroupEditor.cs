using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Services;
using Penumbra.GameData.Structs;
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
    public ImcModGroup? AddModGroup(Mod mod, string newName, ImcIdentifier identifier, ImcEntry defaultEntry,
        SaveType saveType = SaveType.ImmediateSync)
    {
        if (!ModGroupEditor.VerifyFileName(mod, null, newName, true))
            return null;

        var maxPriority = mod.Groups.Count == 0 ? ModPriority.Default : mod.Groups.Max(o => o.Priority) + 1;
        var group       = CreateGroup(mod, newName, identifier, defaultEntry, maxPriority);
        mod.Groups.Add(group);
        SaveService.Save(saveType, new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.GroupAdded, mod, group, null, null, -1);
        return group;
    }

    public ImcSubMod? AddOption(ImcModGroup group, in ImcAttributeCache cache, string name, string description = "",
        SaveType saveType = SaveType.Queue)
    {
        if (cache.LowestUnsetMask == 0)
            return null;

        var subMod = new ImcSubMod(group)
        {
            Name          = name,
            Description   = description,
            AttributeMask = cache.LowestUnsetMask,
        };
        group.OptionData.Add(subMod);
        SaveService.Save(saveType, new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionAdded, group.Mod, group, subMod, null, -1);
        return subMod;
    }

    // Hide this method.
    private new ImcSubMod? AddOption(ImcModGroup group, string name, SaveType saveType)
        => null;

    public void ChangeDefaultAttribute(ImcModGroup group, in ImcAttributeCache cache, int idx, bool value, SaveType saveType = SaveType.Queue)
    {
        if (!cache.Set(group, idx, value))
            return;

        SaveService.Save(saveType, new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMetaChanged, group.Mod, group, null, null, -1);
    }

    public void ChangeDefaultEntry(ImcModGroup group, in ImcEntry newEntry, SaveType saveType = SaveType.Queue)
    {
        var entry = newEntry with { AttributeMask = group.DefaultEntry.AttributeMask };
        if (entry.MaterialId == 0 || group.DefaultEntry.Equals(entry))
            return;

        group.DefaultEntry = entry;
        SaveService.Save(saveType, new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMetaChanged, group.Mod, group, null, null, -1);
    }

    public void ChangeOptionAttribute(ImcSubMod option, in ImcAttributeCache cache, int idx, bool value, SaveType saveType = SaveType.Queue)
    {
        if (!cache.Set(option, idx, value))
            return;

        SaveService.Save(saveType, new ModSaveGroup(option.Group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMetaChanged, option.Mod, option.Group, option, null, -1);
    }

    public void ChangeAllVariants(ImcModGroup group, bool allVariants, SaveType saveType = SaveType.Queue)
    {
        if (group.AllVariants == allVariants)
            return;

        group.AllVariants = allVariants;
        SaveService.Save(saveType, new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMetaChanged, group.Mod, group, null, null, -1);
    }

    public void ChangeCanBeDisabled(ImcModGroup group, bool canBeDisabled, SaveType saveType = SaveType.Queue)
    {
        if (group.CanBeDisabled == canBeDisabled)
            return;

        group.CanBeDisabled = canBeDisabled;
        SaveService.Save(saveType, new ModSaveGroup(group, Config.ReplaceNonAsciiOnImport));
        Communicator.ModOptionChanged.Invoke(ModOptionChangeType.OptionMetaChanged, group.Mod, group, null, null, -1);
    }

    protected override ImcModGroup CreateGroup(Mod mod, string newName, ModPriority priority, SaveType saveType = SaveType.ImmediateSync)
        => new(mod)
        {
            Name     = newName,
            Priority = priority,
        };


    private static ImcModGroup CreateGroup(Mod mod, string newName, ImcIdentifier identifier, ImcEntry defaultEntry, ModPriority priority,
        SaveType saveType = SaveType.ImmediateSync)
        => new(mod)
        {
            Name         = newName,
            Priority     = priority,
            Identifier   = identifier,
            DefaultEntry = defaultEntry,
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
