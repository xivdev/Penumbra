using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.ModsTab.Groups;
using Penumbra.Util;

namespace Penumbra.Mods.Groups;

/// <summary> Groups that allow all available options to be selected at once. </summary>
public sealed class CombiningModGroup : IModGroup
{
    public GroupType Type
        => GroupType.Combining;

    public GroupDrawBehaviour Behaviour
        => GroupDrawBehaviour.MultiSelection;

    public int Index { get; private set; } = -1;

    public void SetIndex(int index)
        => Index = index;

    public          Mod                              Mod             { get; }
    public          Guid                             Id              { get; set; } = Guid.NewGuid();
    public          string                           Name            { get; set; } = "Group";
    public          string                           Description     { get; set; } = string.Empty;
    public          string                           Image           { get; set; } = string.Empty;
    public          ModPriority                      Priority        { get; set; }
    public          int                              Page            { get; set; }
    public          Setting                          DefaultSettings { get; set; }
    public          ModSettingsLayout                Layout          { get; set; }
    public          IModObject?                      ParentSetting   { get; set; }
    public          ICondition<ModSettingContext>?   Condition       { get; set; }
    public readonly IndexList<CombiningSubMod>       OptionData = [];
    public          IndexList<CombinedDataContainer> Data { get; private set; }

    /// <summary> Groups that allow all available options to be selected at once. </summary>
    public CombiningModGroup(Mod mod)
    {
        Mod  = mod;
        Data = [new CombinedDataContainer(this)];
    }

    IReadOnlyList<IModOption> IModGroup.Options
        => OptionData;

    public IReadOnlyList<IModDataContainer> DataContainers
        => Data;

    public bool IsOption
        => OptionData.Count > 0;

    public FullPath? FindBestMatch(Utf8GamePath gamePath)
    {
        foreach (var path in Data.SelectWhere(o
                     => (o.Files.TryGetValue(gamePath, out var file) || o.FileSwaps.TryGetValue(gamePath, out file), file)))
            return path;

        return null;
    }

    public IModOption? AddOption(string name, string description = "")
    {
        var subMod = new CombiningSubMod(this)
        {
            Name        = name,
            Description = description,
        };
        return OptionData.AddNewWithPowerSet(Data, subMod, () => new CombinedDataContainer(this), IModGroup.MaxCombiningOptions)
            ? subMod
            : null;
    }

    public IModGroupEditDrawer EditDrawer(ModGroupEditDrawer editDrawer)
        => new CombiningModGroupEditDrawer(editDrawer, this);

    public void AddData(ModSettings settings, Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, MetaDictionary manipulations)
    {
        var context = new ModSettingContext(Mod, settings);
        if (Condition is not null && !Condition.Evaluate(context))
            return;

        var mask = GetAvailableMask(context);
        setting = new Setting(mask & setting.Value);
        Data[setting.AsIndex].AddDataTo(redirections, manipulations);
    }

    private ulong GetAvailableMask(in ModSettingContext context)
    {
        var mask = 0ul;
        foreach (var (idx, option) in OptionData.Index())
        {
            if (option.Condition is null || option.Condition.Evaluate(context))
                mask |= 1ul << idx;
        }

        return mask;
    }

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData> changedItems)
    {
        foreach (var container in DataContainers)
            identifier.AddChangedItems(container, changedItems);
    }

    public (int Redirections, int Swaps, int Manips) GetCounts()
        => ModGroup.GetCountsBase(this);

    public Setting FixSetting(Setting setting)
        => new(Math.Min(setting.Value, (ulong)(Data.Count - 1)));

    /// <summary> Create a group without a mod only for saving it in the creator. </summary>
    internal static CombiningModGroup WithoutMod(string name)
        => new(null!)
        {
            Name = name,
        };

    /// <summary> For loading when no empty container should be created. </summary>
    internal static CombiningModGroup EmptyData(Mod mod)
        => new(mod, false);

    private CombiningModGroup(Mod mod, bool _)
    {
        Mod  = mod;
        Data = [];
    }
}
