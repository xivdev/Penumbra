using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.ModsTab.Groups;

namespace Penumbra.Mods.Groups;

public class ImcModGroup(Mod mod) : IModGroup
{
    public Mod    Mod         { get; }      = mod;
    public string Name        { get; set; } = "Option";
    public string Description { get; set; } = string.Empty;

    public GroupType Type
        => GroupType.Imc;

    public GroupDrawBehaviour Behaviour
        => GroupDrawBehaviour.MultiSelection;

    public ModPriority Priority        { get; set; } = ModPriority.Default;
    public Setting     DefaultSettings { get; set; } = Setting.Zero;

    public ImcIdentifier Identifier;
    public ImcEntry      DefaultEntry;

    public FullPath? FindBestMatch(Utf8GamePath gamePath)
        => null;

    private bool _canBeDisabled;

    public bool CanBeDisabled
    {
        get => OptionData.Any(m => m.IsDisableSubMod);
        set
        {
            _canBeDisabled = value;
            if (!value)
            {
                OptionData.RemoveAll(m => m.IsDisableSubMod);
                DefaultSettings = FixSetting(DefaultSettings);
            }
            else
            {
                if (!OptionData.Any(m => m.IsDisableSubMod))
                    OptionData.Add(ImcSubMod.DisableSubMod(this));
            }
        }
    }

    public bool DefaultDisabled
        => IsDisabled(DefaultSettings);

    public IModOption? AddOption(string name, string description = "")
    {
        var groupIdx = Mod.Groups.IndexOf(this);
        if (groupIdx < 0)
            return null;

        var subMod = new ImcSubMod(this)
        {
            Name          = name,
            Description   = description,
            AttributeMask = 0,
        };
        OptionData.Add(subMod);
        return subMod;
    }

    public readonly List<ImcSubMod> OptionData = [];

    public IReadOnlyList<IModOption> Options
        => OptionData;

    public IReadOnlyList<IModDataContainer> DataContainers
        => [];

    public bool IsOption
        => OptionData.Count > 0;

    public int GetIndex()
        => ModGroup.GetIndex(this);

    public IModGroupEditDrawer EditDrawer(ModGroupEditDrawer editDrawer)
        => new ImcModGroupEditDrawer(editDrawer, this);

    public ImcManipulation GetManip(ushort mask)
        => new(Identifier.ObjectType, Identifier.BodySlot, Identifier.PrimaryId, Identifier.SecondaryId.Id, Identifier.Variant.Id,
            Identifier.EquipSlot, DefaultEntry with { AttributeMask = mask });

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
    {
        if (IsDisabled(setting))
            return;

        var mask = GetCurrentMask(setting);
        var imc  = GetManip(mask);
        manipulations.Add(imc);
    }

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, object?> changedItems)
        => Identifier.AddChangedItems(identifier, changedItems);

    public Setting FixSetting(Setting setting)
        => new(setting.Value & ((1ul << OptionData.Count) - 1));

    public void WriteJson(JsonTextWriter jWriter, JsonSerializer serializer, DirectoryInfo? basePath = null)
    {
        ModSaveGroup.WriteJsonBase(jWriter, this);
        var jObj = Identifier.AddToJson(new JObject());
        jWriter.WritePropertyName(nameof(Identifier));
        jObj.WriteTo(jWriter);
        jWriter.WritePropertyName(nameof(DefaultEntry));
        serializer.Serialize(jWriter, DefaultEntry);
        jWriter.WritePropertyName("Options");
        jWriter.WriteStartArray();
        foreach (var option in OptionData)
        {
            jWriter.WriteStartObject();
            SubMod.WriteModOption(jWriter, option);
            if (option.IsDisableSubMod)
            {
                jWriter.WritePropertyName(nameof(option.IsDisableSubMod));
                jWriter.WriteValue(true);
            }
            else
            {
                jWriter.WritePropertyName(nameof(option.AttributeMask));
                jWriter.WriteValue(option.AttributeMask);
            }

            jWriter.WriteEndObject();
        }

        jWriter.WriteEndArray();
    }

    public (int Redirections, int Swaps, int Manips) GetCounts()
        => (0, 0, 1);

    public static ImcModGroup? Load(Mod mod, JObject json)
    {
        var options    = json["Options"];
        var identifier = ImcIdentifier.FromJson(json[nameof(Identifier)] as JObject);
        var ret = new ImcModGroup(mod)
        {
            Name         = json[nameof(Name)]?.ToObject<string>() ?? string.Empty,
            Description  = json[nameof(Description)]?.ToObject<string>() ?? string.Empty,
            Priority     = json[nameof(Priority)]?.ToObject<ModPriority>() ?? ModPriority.Default,
            DefaultEntry = json[nameof(DefaultEntry)]?.ToObject<ImcEntry>() ?? new ImcEntry(),
        };
        if (ret.Name.Length == 0)
            return null;

        if (!identifier.HasValue || ret.DefaultEntry.MaterialId == 0)
        {
            Penumbra.Messager.NotificationMessage($"Could not add IMC group {ret.Name} because the associated IMC Entry is invalid.",
                NotificationType.Warning);
            return null;
        }

        var rollingMask = ret.DefaultEntry.AttributeMask;
        if (options != null)
            foreach (var child in options.Children())
            {
                var subMod = new ImcSubMod(ret, child);

                if (subMod.IsDisableSubMod)
                    ret._canBeDisabled = true;

                if (subMod.IsDisableSubMod && ret.OptionData.FirstOrDefault(m => m.IsDisableSubMod) is { } disable)
                {
                    Penumbra.Messager.NotificationMessage(
                        $"Could not add IMC option {subMod.Name} to {ret.Name} because it already contains {disable.Name} as disable option.",
                        NotificationType.Warning);
                }
                else if ((subMod.AttributeMask & rollingMask) != 0)
                {
                    Penumbra.Messager.NotificationMessage(
                        $"Could not add IMC option {subMod.Name} to {ret.Name} because it contains attributes already in use.",
                        NotificationType.Warning);
                }
                else
                {
                    rollingMask |= subMod.AttributeMask;
                    ret.OptionData.Add(subMod);
                }
            }

        ret.Identifier      = identifier.Value;
        ret.DefaultSettings = json[nameof(DefaultSettings)]?.ToObject<Setting>() ?? Setting.Zero;
        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);
        return ret;
    }

    private bool IsDisabled(Setting setting)
    {
        if (!CanBeDisabled)
            return false;

        var idx = OptionData.IndexOf(m => m.IsDisableSubMod);
        if (idx >= 0)
            return setting.HasFlag(idx);

        Penumbra.Log.Warning($"A IMC Group should be able to be disabled, but does not contain a disable option.");
        return false;
    }

    private ushort GetCurrentMask(Setting setting)
    {
        var mask = DefaultEntry.AttributeMask;
        for (var i = 0; i < OptionData.Count; ++i)
        {
            if (!setting.HasFlag(i))
                continue;

            var option = OptionData[i];
            mask |= option.AttributeMask;
        }

        return mask;
    }
}
