using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Subclasses;

/// <summary> Groups that allow only one of their available options to be selected. </summary>
public sealed class SingleModGroup(Mod mod) : IModGroup
{
    public GroupType Type
        => GroupType.Single;

    public Mod         Mod             { get; set; } = mod;
    public string      Name            { get; set; } = "Option";
    public string      Description     { get; set; } = "A mutually exclusive group of settings.";
    public ModPriority Priority        { get; set; }
    public Setting     DefaultSettings { get; set; }

    public readonly List<SubMod> OptionData = [];

    public FullPath? FindBestMatch(Utf8GamePath gamePath)
        => OptionData
            .SelectWhere(m => (m.FileData.TryGetValue(gamePath, out var file) || m.FileSwapData.TryGetValue(gamePath, out file), file))
            .FirstOrDefault();

    public int AddOption(Mod mod, string name, string description = "")
    {
        var subMod = new SubMod(mod, this)
        {
            Name        = name,
            Description = description,
        };
        OptionData.Add(subMod);
        return OptionData.Count - 1;
    }

    public bool ChangeOptionDescription(int optionIndex, string newDescription)
    {
        if (optionIndex < 0 || optionIndex >= OptionData.Count)
            return false;

        var option = OptionData[optionIndex];
        if (option.Description == newDescription)
            return false;

        option.Description = newDescription;
        return true;
    }

    public bool ChangeOptionName(int optionIndex, string newName)
    {
        if (optionIndex < 0 || optionIndex >= OptionData.Count)
            return false;

        var option = OptionData[optionIndex];
        if (option.Name == newName)
            return false;

        option.Name = newName;
        return true;
    }

    public IReadOnlyList<IModOption> Options
        => OptionData;

    public bool IsOption
        => OptionData.Count > 1;

    public static SingleModGroup? Load(Mod mod, JObject json, int groupIdx)
    {
        var options = json["Options"];
        var ret = new SingleModGroup(mod)
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
                var subMod = new SubMod(mod, ret);
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
                var multi = new MultiModGroup(Mod)
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

        return true;
    }

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
        => OptionData[setting.AsIndex].AddData(redirections, manipulations);

    public Setting FixSetting(Setting setting)
        => OptionData.Count == 0 ? Setting.Zero : new Setting(Math.Min(setting.Value, (ulong)(OptionData.Count - 1)));

    /// <summary> Create a group without a mod only for saving it in the creator. </summary>
    internal static SingleModGroup CreateForSaving(string name)
        => new(null!)
        {
            Name = name,
        };
}
