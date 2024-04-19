using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Subclasses;

/// <summary> Groups that allow only one of their available options to be selected. </summary>
public sealed class SingleModGroup : IModGroup
{
    public GroupType Type
        => GroupType.Single;

    public string      Name            { get; set; } = "Option";
    public string      Description     { get; set; } = "A mutually exclusive group of settings.";
    public ModPriority Priority        { get; set; }
    public Setting     DefaultSettings { get; set; }

    public readonly List<SubMod> OptionData = [];

    public ModPriority OptionPriority(Index _)
        => Priority;

    public ISubMod this[Index idx]
        => OptionData[idx];

    public bool IsOption
        => Count > 1;

    [JsonIgnore]
    public int Count
        => OptionData.Count;

    public IEnumerator<ISubMod> GetEnumerator()
        => OptionData.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public static SingleModGroup? Load(Mod mod, JObject json, int groupIdx)
    {
        var options = json["Options"];
        var ret = new SingleModGroup
        {
            Name            = json[nameof(Name)]?.ToObject<string>() ?? string.Empty,
            Description     = json[nameof(Description)]?.ToObject<string>() ?? string.Empty,
            Priority        = json[nameof(Priority)]?.ToObject<ModPriority>() ?? ModPriority.Default,
            DefaultSettings = json[nameof(DefaultSettings)]?.ToObject<Setting>() ?? Setting.Zero,
        };
        if (ret.Name.Length == 0)
            return null;

        if (options != null)
            foreach (var child in options.Children())
            {
                var subMod = new SubMod(mod);
                subMod.SetPosition(groupIdx, ret.OptionData.Count);
                subMod.Load(mod.ModPath, child, out _);
                ret.OptionData.Add(subMod);
            }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);
        return ret;
    }

    public IModGroup Convert(GroupType type)
    {
        switch (type)
        {
            case GroupType.Single: return this;
            case GroupType.Multi:
                var multi = new MultiModGroup()
                {
                    Name            = Name,
                    Description     = Description,
                    Priority        = Priority,
                    DefaultSettings = Setting.Multi((int)DefaultSettings.Value),
                };
                multi.PrioritizedOptions.AddRange(OptionData.Select((o, i) => (o, new ModPriority(i))));
                return multi;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public bool MoveOption(int optionIdxFrom, int optionIdxTo)
    {
        if (!OptionData.Move(optionIdxFrom, optionIdxTo))
            return false;

        var currentIndex = DefaultSettings.AsIndex;
        // Update default settings with the move.
        if (currentIndex == optionIdxFrom)
        {
            DefaultSettings = Setting.Single(optionIdxTo);
        }
        else if (optionIdxFrom < optionIdxTo)
        {
            if (currentIndex > optionIdxFrom && currentIndex <= optionIdxTo)
                DefaultSettings = Setting.Single(currentIndex - 1);
        }
        else if (currentIndex < optionIdxFrom && currentIndex >= optionIdxTo)
        {
            DefaultSettings = Setting.Single(currentIndex + 1);
        }

        UpdatePositions(Math.Min(optionIdxFrom, optionIdxTo));
        return true;
    }

    public void UpdatePositions(int from = 0)
    {
        foreach (var (o, i) in OptionData.WithIndex().Skip(from))
            o.SetPosition(o.GroupIdx, i);
    }

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
        => this[setting.AsIndex].AddData(redirections, manipulations);

    public Setting FixSetting(Setting setting)
        => Count == 0 ? Setting.Zero : new Setting(Math.Min(setting.Value, (ulong)(Count - 1)));
}
