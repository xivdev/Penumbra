using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.ModsTab.Groups;
using Penumbra.Util;

namespace Penumbra.Mods.Groups;

/// <summary> Groups that allow only one of their available options to be selected. </summary>
public sealed class SingleModGroup(Mod mod) : IModGroup, ITexToolsGroup
{
    public GroupType Type
        => GroupType.Single;

    public GroupDrawBehaviour Behaviour
        => GroupDrawBehaviour.SingleSelection;

    public Mod         Mod             { get; }      = mod;
    public string      Name            { get; set; } = "Option";
    public string      Description     { get; set; } = string.Empty;
    public string      Image           { get; set; } = string.Empty;
    public ModPriority Priority        { get; set; }
    public int         Page            { get; set; }
    public Setting     DefaultSettings { get; set; }

    public readonly List<SingleSubMod> OptionData = [];

    public FullPath? FindBestMatch(Utf8GamePath gamePath)
    {
        foreach (var path in OptionData
                     .SelectWhere(m => (m.Files.TryGetValue(gamePath, out var file) || m.FileSwaps.TryGetValue(gamePath, out file), file)))
            return path;

        return null;
    }

    public IModOption AddOption(string name, string description = "")
    {
        var subMod = new SingleSubMod(this)
        {
            Name        = name,
            Description = description,
        };
        OptionData.Add(subMod);
        return subMod;
    }

    public IReadOnlyList<IModOption> Options
        => OptionData;

    public IReadOnlyList<IModDataContainer> DataContainers
        => OptionData;

    public bool IsOption
        => OptionData.Count > 1;

    public static SingleModGroup? Load(Mod mod, JObject json)
    {
        var options = json["Options"];
        var ret     = new SingleModGroup(mod);
        if (!ModSaveGroup.ReadJsonBase(json, ret))
            return null;

        if (options != null)
            foreach (var child in options.Children())
            {
                var subMod = new SingleSubMod(ret, child);
                ret.OptionData.Add(subMod);
            }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);
        return ret;
    }

    public MultiModGroup ConvertToMulti()
    {
        var multi = new MultiModGroup(Mod)
        {
            Name            = Name,
            Description     = Description,
            Priority        = Priority,
            Image           = Image,
            Page            = Page,
            DefaultSettings = Setting.Multi((int)DefaultSettings.Value),
        };
        multi.OptionData.AddRange(OptionData.Select((o, i) => o.ConvertToMulti(multi, new ModPriority(i))));
        return multi;
    }

    public int GetIndex()
        => ModGroup.GetIndex(this);

    public IModGroupEditDrawer EditDrawer(ModGroupEditDrawer editDrawer)
        => new SingleModGroupEditDrawer(editDrawer, this);

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, MetaDictionary manipulations)
    {
        if (OptionData.Count == 0)
            return;

        OptionData[setting.AsIndex].AddDataTo(redirections, manipulations);
    }

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData> changedItems)
    {
        foreach (var container in DataContainers)
            identifier.AddChangedItems(container, changedItems);
    }

    public Setting FixSetting(Setting setting)
        => OptionData.Count == 0 ? Setting.Zero : new Setting(Math.Min(setting.Value, (ulong)(OptionData.Count - 1)));

    public (int Redirections, int Swaps, int Manips) GetCounts()
        => ModGroup.GetCountsBase(this);

    public void WriteJson(JsonTextWriter jWriter, JsonSerializer serializer, DirectoryInfo? basePath = null)
    {
        ModSaveGroup.WriteJsonBase(jWriter, this);
        jWriter.WritePropertyName("Options");
        jWriter.WriteStartArray();
        foreach (var option in OptionData)
        {
            jWriter.WriteStartObject();
            SubMod.WriteModOption(jWriter, option);
            SubMod.WriteModContainer(jWriter, serializer, option, basePath ?? Mod.ModPath);
            jWriter.WriteEndObject();
        }

        jWriter.WriteEndArray();
    }

    /// <summary> Create a group without a mod only for saving it in the creator. </summary>
    internal static SingleModGroup CreateForSaving(string name)
        => new(null!)
        {
            Name = name,
        };

    IReadOnlyList<OptionSubMod> ITexToolsGroup.OptionData
        => OptionData;
}
