using Dalamud.Interface.ImGuiNotification;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;
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
public sealed class MultiModGroup(Mod mod) : IModGroup, ITexToolsGroup
{
    public GroupType Type
        => GroupType.Multi;

    public GroupDrawBehaviour Behaviour
        => GroupDrawBehaviour.MultiSelection;

    public          Mod               Mod             { get; }      = mod;
    public          string            Name            { get; set; } = "Group";
    public          string            Description     { get; set; } = string.Empty;
    public          string            Image           { get; set; } = string.Empty;
    public          ModPriority       Priority        { get; set; }
    public          int               Page            { get; set; }
    public          Setting           DefaultSettings { get; set; }
    public readonly List<MultiSubMod> OptionData = [];

    public IReadOnlyList<IModOption> Options
        => OptionData;

    public IReadOnlyList<IModDataContainer> DataContainers
        => OptionData;

    public bool IsOption
        => OptionData.Count > 0;

    public FullPath? FindBestMatch(Utf8GamePath gamePath)
    {
        foreach (var path in OptionData.OrderByDescending(o => o.Priority)
                     .SelectWhere(o => (o.Files.TryGetValue(gamePath, out var file) || o.FileSwaps.TryGetValue(gamePath, out file), file)))
            return path;

        return null;
    }

    public IModOption? AddOption(string name, string description = "")
    {
        var groupIdx = Mod.Groups.IndexOf(this);
        if (groupIdx < 0)
            return null;

        var subMod = new MultiSubMod(this)
        {
            Name        = name,
            Description = description,
        };
        OptionData.Add(subMod);
        return subMod;
    }

    public static MultiModGroup? Load(Mod mod, JObject json)
    {
        var ret = new MultiModGroup(mod);
        if (!ModSaveGroup.ReadJsonBase(json, ret))
            return null;

        var options = json["Options"];
        if (options != null)
            foreach (var child in options.Children())
            {
                if (ret.OptionData.Count == IModGroup.MaxMultiOptions)
                {
                    Penumbra.Messager.NotificationMessage(
                        $"Multi Group {ret.Name} in {mod.Name} has more than {IModGroup.MaxMultiOptions} options, ignoring excessive options.",
                        NotificationType.Warning);
                    break;
                }

                var subMod = new MultiSubMod(ret, child);
                ret.OptionData.Add(subMod);
            }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);

        return ret;
    }

    public SingleModGroup ConvertToSingle()
    {
        var single = new SingleModGroup(Mod)
        {
            Name            = Name,
            Description     = Description,
            Priority        = Priority,
            Image           = Image,
            Page            = Page,
            DefaultSettings = DefaultSettings.TurnMulti(OptionData.Count),
        };
        single.OptionData.AddRange(OptionData.Select(o => o.ConvertToSingle(single)));
        return single;
    }

    public int GetIndex()
        => ModGroup.GetIndex(this);

    public IModGroupEditDrawer EditDrawer(ModGroupEditDrawer editDrawer)
        => new MultiModGroupEditDrawer(editDrawer, this);

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, MetaDictionary manipulations)
    {
        foreach (var (option, index) in OptionData.WithIndex().OrderByDescending(o => o.Value.Priority))
        {
            if (setting.HasFlag(index))
                option.AddDataTo(redirections, manipulations);
        }
    }

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData> changedItems)
    {
        foreach (var container in DataContainers)
            identifier.AddChangedItems(container, changedItems);
    }

    public void WriteJson(JsonTextWriter jWriter, JsonSerializer serializer, DirectoryInfo? basePath = null)
    {
        ModSaveGroup.WriteJsonBase(jWriter, this);
        jWriter.WritePropertyName("Options");
        jWriter.WriteStartArray();
        foreach (var option in OptionData)
        {
            jWriter.WriteStartObject();
            SubMod.WriteModOption(jWriter, option);
            jWriter.WritePropertyName(nameof(option.Priority));
            jWriter.WriteValue(option.Priority.Value);
            SubMod.WriteModContainer(jWriter, serializer, option, basePath ?? Mod.ModPath);
            jWriter.WriteEndObject();
        }

        jWriter.WriteEndArray();
    }

    public (int Redirections, int Swaps, int Manips) GetCounts()
        => ModGroup.GetCountsBase(this);

    public Setting FixSetting(Setting setting)
        => new(setting.Value & ((1ul << OptionData.Count) - 1));

    /// <summary> Create a group without a mod only for saving it in the creator. </summary>
    internal static MultiModGroup WithoutMod(string name)
        => new(null!)
        {
            Name = name,
        };

    IReadOnlyList<OptionSubMod> ITexToolsGroup.OptionData
        => OptionData;
}
