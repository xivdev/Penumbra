using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Subclasses;

/// <summary> Groups that allow all available options to be selected at once. </summary>
public sealed class MultiModGroup : IModGroup
{
    public GroupType Type
        => GroupType.Multi;

    public string      Name            { get; set; } = "Group";
    public string      Description     { get; set; } = "A non-exclusive group of settings.";
    public ModPriority Priority        { get; set; }
    public Setting     DefaultSettings { get; set; }

    public FullPath? FindBestMatch(Utf8GamePath gamePath)
        => PrioritizedOptions.OrderByDescending(o => o.Priority)
            .SelectWhere(o => (o.Mod.FileData.TryGetValue(gamePath, out var file) || o.Mod.FileSwapData.TryGetValue(gamePath, out file), file))
            .FirstOrDefault();

    public SubMod this[Index idx]
        => PrioritizedOptions[idx].Mod;

    public bool IsOption
        => Count > 0;

    [JsonIgnore]
    public int Count
        => PrioritizedOptions.Count;

    public readonly List<(SubMod Mod, ModPriority Priority)> PrioritizedOptions = [];

    public IEnumerator<SubMod> GetEnumerator()
        => PrioritizedOptions.Select(o => o.Mod).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public static MultiModGroup? Load(Mod mod, JObject json, int groupIdx)
    {
        var ret = new MultiModGroup()
        {
            Name            = json[nameof(Name)]?.ToObject<string>() ?? string.Empty,
            Description     = json[nameof(Description)]?.ToObject<string>() ?? string.Empty,
            Priority        = json[nameof(Priority)]?.ToObject<ModPriority>() ?? ModPriority.Default,
            DefaultSettings = json[nameof(DefaultSettings)]?.ToObject<Setting>() ?? Setting.Zero,
        };
        if (ret.Name.Length == 0)
            return null;

        var options = json["Options"];
        if (options != null)
            foreach (var child in options.Children())
            {
                if (ret.PrioritizedOptions.Count == IModGroup.MaxMultiOptions)
                {
                    Penumbra.Messager.NotificationMessage(
                        $"Multi Group {ret.Name} in {mod.Name} has more than {IModGroup.MaxMultiOptions} options, ignoring excessive options.",
                        NotificationType.Warning);
                    break;
                }

                var subMod = new SubMod(mod);
                subMod.SetPosition(groupIdx, ret.PrioritizedOptions.Count);
                subMod.Load(mod.ModPath, child, out var priority);
                ret.PrioritizedOptions.Add((subMod, priority));
            }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);

        return ret;
    }

    public IModGroup Convert(GroupType type)
    {
        switch (type)
        {
            case GroupType.Multi: return this;
            case GroupType.Single:
                var multi = new SingleModGroup()
                {
                    Name            = Name,
                    Description     = Description,
                    Priority        = Priority,
                    DefaultSettings = DefaultSettings.TurnMulti(Count),
                };
                multi.OptionData.AddRange(PrioritizedOptions.Select(p => p.Mod));
                return multi;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public bool MoveOption(int optionIdxFrom, int optionIdxTo)
    {
        if (!PrioritizedOptions.Move(optionIdxFrom, optionIdxTo))
            return false;

        DefaultSettings = DefaultSettings.MoveBit(optionIdxFrom, optionIdxTo);
        UpdatePositions(Math.Min(optionIdxFrom, optionIdxTo));
        return true;
    }

    public void UpdatePositions(int from = 0)
    {
        foreach (var ((o, _), i) in PrioritizedOptions.WithIndex().Skip(from))
            o.SetPosition(o.GroupIdx, i);
    }

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
    {
        foreach (var (option, index) in PrioritizedOptions.WithIndex().OrderByDescending(o => o.Value.Priority))
        {
            if (setting.HasFlag(index))
                option.Mod.AddData(redirections, manipulations);
        }
    }

    public Setting FixSetting(Setting setting)
        => new(setting.Value & ((1ul << Count) - 1));
}
