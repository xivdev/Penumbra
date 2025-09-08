using Dalamud.Interface.ImGuiNotification;
using Luna;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.ModsTab.Groups;
using Penumbra.Util;

namespace Penumbra.Mods.Groups;

public sealed class ComplexModGroup(Mod mod) : IModGroup
{
    public Mod    Mod         { get; }      = mod;
    public string Name        { get; set; } = "Option";
    public string Description { get; set; } = string.Empty;
    public string Image       { get; set; } = string.Empty;

    public GroupType Type
        => GroupType.Complex;

    public GroupDrawBehaviour Behaviour
        => GroupDrawBehaviour.Complex;

    public ModPriority Priority        { get; set; }
    public int         Page            { get; set; }
    public Setting     DefaultSettings { get; set; }

    public readonly List<ComplexSubMod>        Options    = [];
    public readonly List<ComplexDataContainer> Containers = [];


    public FullPath? FindBestMatch(Utf8GamePath gamePath)
        => throw new NotImplementedException();

    public IModOption? AddOption(string name, string description = "")
        => throw new NotImplementedException();

    IReadOnlyList<IModOption> IModGroup.Options
        => Options;

    IReadOnlyList<IModDataContainer> IModGroup.DataContainers
        => Containers;

    public bool IsOption
        => Options.Count > 0;

    public int GetIndex()
        => ModGroup.GetIndex(this);

    public IModGroupEditDrawer EditDrawer(ModGroupEditDrawer editDrawer)
        => throw new NotImplementedException();

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, MetaDictionary manipulations)
    {
        foreach (var container in Containers.Where(c => c.Association.IsEnabled(setting)))
            SubMod.AddContainerTo(container, redirections, manipulations);
    }

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData> changedItems)
    {
        foreach (var container in Containers)
            identifier.AddChangedItems(container, changedItems);
    }

    public Setting FixSetting(Setting setting)
        => new(setting.Value & ((1ul << Options.Count) - 1));

    public void WriteJson(JsonTextWriter jWriter, JsonSerializer serializer, DirectoryInfo? basePath = null)
    {
        ModSaveGroup.WriteJsonBase(jWriter, this);
        jWriter.WritePropertyName("Options");
        jWriter.WriteStartArray();
        foreach (var option in Options)
        {
            jWriter.WriteStartObject();
            SubMod.WriteModOption(jWriter, option);
            if (!option.Conditions.IsZero)
            {
                jWriter.WritePropertyName("ConditionMask");
                jWriter.WriteValue(option.Conditions.Mask.Value);
                jWriter.WritePropertyName("ConditionValue");
                jWriter.WriteValue(option.Conditions.Value.Value);
            }

            if (option.Indentation > 0)
            {
                jWriter.WritePropertyName("Indentation");
                jWriter.WriteValue(option.Indentation);
            }

            if (option.SubGroupLabel.Length > 0)
            {
                jWriter.WritePropertyName("SubGroup");
                jWriter.WriteValue(option.SubGroupLabel);
            }

            jWriter.WriteEndObject();
        }

        jWriter.WriteEndArray();

        jWriter.WritePropertyName("Containers");
        jWriter.WriteStartArray();
        foreach (var container in Containers)
        {
            jWriter.WriteStartObject();
            if (container.Name.Length > 0)
            {
                jWriter.WritePropertyName("Name");
                jWriter.WriteValue(container.Name);
            }

            if (!container.Association.IsZero)
            {
                jWriter.WritePropertyName("AssociationMask");
                jWriter.WriteValue(container.Association.Mask.Value);

                jWriter.WritePropertyName("AssociationValue");
                jWriter.WriteValue(container.Association.Value.Value);
            }

            SubMod.WriteModContainer(jWriter, serializer, container, basePath ?? Mod.ModPath);
            jWriter.WriteEndObject();
        }

        jWriter.WriteEndArray();
    }

    public (int Redirections, int Swaps, int Manips) GetCounts()
        => ModGroup.GetCountsBase(this);

    public static ComplexModGroup? Load(Mod mod, JObject json)
    {
        var ret = new ComplexModGroup(mod);
        if (!ModSaveGroup.ReadJsonBase(json, ret))
            return null;

        var options = json["Options"];
        if (options != null)
            foreach (var child in options.Children())
            {
                if (ret.Options.Count == IModGroup.MaxComplexOptions)
                {
                    Penumbra.Messager.NotificationMessage(
                        $"Complex Group {ret.Name} in {mod.Name} has more than {IModGroup.MaxComplexOptions} options, ignoring excessive options.",
                        NotificationType.Warning);
                    break;
                }

                var subMod = new ComplexSubMod(ret, child);
                ret.Options.Add(subMod);
            }

        // Fix up conditions: No condition on itself.
        foreach (var (index, option) in ret.Options.Index())
        {
            option.Conditions = option.Conditions.Limit(ret.Options.Count);
            option.Conditions = new MaskedSetting(option.Conditions.Mask.SetBit(index, false), option.Conditions.Value);
        }

        var containers = json["Containers"];
        if (containers != null)
            foreach (var child in containers.Children())
            {
                var container = new ComplexDataContainer(ret, child);
                container.Association = container.Association.Limit(ret.Options.Count);
                ret.Containers.Add(container);
            }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);

        return ret;
    }
}
