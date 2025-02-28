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
public sealed class CombiningModGroup : IModGroup
{
    public GroupType Type
        => GroupType.Combining;

    public GroupDrawBehaviour Behaviour
        => GroupDrawBehaviour.MultiSelection;

    public          Mod                         Mod             { get; }
    public          string                      Name            { get; set; } = "Group";
    public          string                      Description     { get; set; } = string.Empty;
    public          string                      Image           { get; set; } = string.Empty;
    public          ModPriority                 Priority        { get; set; }
    public          int                         Page            { get; set; }
    public          Setting                     DefaultSettings { get; set; }
    public readonly List<CombiningSubMod>       OptionData = [];
    public          List<CombinedDataContainer> Data { get; private set; }

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
        var groupIdx = Mod.Groups.IndexOf(this);
        if (groupIdx < 0)
            return null;

        var subMod = new CombiningSubMod(this)
        {
            Name        = name,
            Description = description,
        };
        return OptionData.AddNewWithPowerSet(Data, subMod, () => new CombinedDataContainer(this), IModGroup.MaxCombiningOptions)
            ? subMod
            : null;
    }

    public static CombiningModGroup? Load(Mod mod, JObject json)
    {
        var ret = new CombiningModGroup(mod, true);
        if (!ModSaveGroup.ReadJsonBase(json, ret))
            return null;

        var options = json["Options"];
        if (options != null)
            foreach (var child in options.Children())
            {
                if (ret.OptionData.Count == IModGroup.MaxCombiningOptions)
                {
                    Penumbra.Messager.NotificationMessage(
                        $"Combining Group {ret.Name} in {mod.Name} has more than {IModGroup.MaxCombiningOptions} options, ignoring excessive options.",
                        NotificationType.Warning);
                    break;
                }

                var subMod = new CombiningSubMod(ret, child);
                ret.OptionData.Add(subMod);
            }

        var requiredContainers = 1 << ret.OptionData.Count;
        var containers         = json["Containers"];
        if (containers != null)
            foreach (var child in containers.Children())
            {
                if (requiredContainers <= ret.Data.Count)
                {
                    Penumbra.Messager.NotificationMessage(
                        $"Combining Group {ret.Name} in {mod.Name} has more data containers than it can support with {ret.OptionData.Count} options, ignoring excessive containers.",
                        NotificationType.Warning);
                    break;
                }

                var container = new CombinedDataContainer(ret, child);
                ret.Data.Add(container);
            }

        if (requiredContainers > ret.Data.Count)
        {
            Penumbra.Messager.NotificationMessage(
                $"Combining Group {ret.Name} in {mod.Name} has not enough data containers for its {ret.OptionData.Count} options, filling with empty containers.",
                NotificationType.Warning);
            ret.Data.EnsureCapacity(requiredContainers);
            ret.Data.AddRange(Enumerable.Repeat(0, requiredContainers - ret.Data.Count).Select(_ => new CombinedDataContainer(ret)));
        }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);

        return ret;
    }

    public int GetIndex()
        => ModGroup.GetIndex(this);

    public IModGroupEditDrawer EditDrawer(ModGroupEditDrawer editDrawer)
        => new CombiningModGroupEditDrawer(editDrawer, this);

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, MetaDictionary manipulations)
        => Data[setting.AsIndex].AddDataTo(redirections, manipulations);

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
            jWriter.WriteEndObject();
        }

        jWriter.WriteEndArray();

        jWriter.WritePropertyName("Containers");
        jWriter.WriteStartArray();
        foreach (var container in Data)
        {
            jWriter.WriteStartObject();
            if (container.Name.Length > 0)
            {
                jWriter.WritePropertyName("Name");
                jWriter.WriteValue(container.Name);
            }

            SubMod.WriteModContainer(jWriter, serializer, container, basePath ?? Mod.ModPath);
            jWriter.WriteEndObject();
        }

        jWriter.WriteEndArray();
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
    private CombiningModGroup(Mod mod, bool _)
    {
        Mod  = mod;
        Data = [];
    }
}
