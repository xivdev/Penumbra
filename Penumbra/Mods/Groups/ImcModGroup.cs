using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ModsTab.Groups;
using Penumbra.Util;

namespace Penumbra.Mods.Groups;

public class ImcModGroup(Mod mod) : IModGroup
{
    public const int DisabledIndex = 60;

    public Mod    Mod         { get; }      = mod;
    public string Name        { get; set; } = "Option";
    public string Description { get; set; } = string.Empty;

    public GroupType Type
        => GroupType.Imc;

    public GroupDrawBehaviour Behaviour
        => GroupDrawBehaviour.MultiSelection;

    public ModPriority Priority        { get; set; } = ModPriority.Default;
    public Setting     DefaultSettings { get; set; } = Setting.Zero;

    public PrimaryId   PrimaryId;
    public SecondaryId SecondaryId;
    public ObjectType  ObjectType;
    public BodySlot    BodySlot;
    public EquipSlot   EquipSlot;
    public Variant     Variant;

    public ImcEntry DefaultEntry;

    public FullPath? FindBestMatch(Utf8GamePath gamePath)
        => null;

    private bool _canBeDisabled = false;

    public bool CanBeDisabled
    {
        get => _canBeDisabled;
        set
        {
            _canBeDisabled = value;
            if (!value)
                DefaultSettings = FixSetting(DefaultSettings);
        }
    }

    public bool DefaultDisabled
        => _canBeDisabled && DefaultSettings.HasFlag(DisabledIndex);

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
        => CanBeDisabled || OptionData.Count > 0;

    public int GetIndex()
        => ModGroup.GetIndex(this);

    public IModGroupEditDrawer EditDrawer(ModGroupEditDrawer editDrawer)
        => new ImcModGroupEditDrawer(editDrawer, this);

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

    private ushort GetFullMask()
        => GetCurrentMask(Setting.AllBits(63));

    public ImcManipulation GetManip(ushort mask)
        => new(ObjectType, BodySlot, PrimaryId, SecondaryId.Id, Variant.Id, EquipSlot,
            DefaultEntry with { AttributeMask = mask });

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
    {
        if (CanBeDisabled && setting.HasFlag(DisabledIndex))
            return;

        var mask = GetCurrentMask(setting);
        var imc  = GetManip(mask);
        manipulations.Add(imc);
    }

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, object?> changedItems)
        => identifier.MetaChangedItems(changedItems, GetManip(0));

    public Setting FixSetting(Setting setting)
        => new(setting.Value & (((1ul << OptionData.Count) - 1) | (CanBeDisabled ? 1ul << DisabledIndex : 0)));

    public void WriteJson(JsonTextWriter jWriter, JsonSerializer serializer, DirectoryInfo? basePath = null)
    {
        ModSaveGroup.WriteJsonBase(jWriter, this);
        jWriter.WritePropertyName(nameof(ObjectType));
        jWriter.WriteValue(ObjectType.ToString());
        jWriter.WritePropertyName(nameof(BodySlot));
        jWriter.WriteValue(BodySlot.ToString());
        jWriter.WritePropertyName(nameof(EquipSlot));
        jWriter.WriteValue(EquipSlot.ToString());
        jWriter.WritePropertyName(nameof(PrimaryId));
        jWriter.WriteValue(PrimaryId.Id);
        jWriter.WritePropertyName(nameof(SecondaryId));
        jWriter.WriteValue(SecondaryId.Id);
        jWriter.WritePropertyName(nameof(Variant));
        jWriter.WriteValue(Variant.Id);
        jWriter.WritePropertyName(nameof(DefaultEntry));
        serializer.Serialize(jWriter, DefaultEntry);
        jWriter.WritePropertyName("Options");
        jWriter.WriteStartArray();
        foreach (var option in OptionData)
        {
            jWriter.WriteStartObject();
            SubMod.WriteModOption(jWriter, option);
            jWriter.WritePropertyName(nameof(option.AttributeMask));
            jWriter.WriteValue(option.AttributeMask);
            jWriter.WriteEndObject();
        }

        jWriter.WriteEndArray();
    }

    public (int Redirections, int Swaps, int Manips) GetCounts()
        => (0, 0, 1);

    public static ImcModGroup? Load(Mod mod, JObject json)
    {
        var options = json["Options"];
        var ret = new ImcModGroup(mod)
        {
            Name            = json[nameof(Name)]?.ToObject<string>() ?? string.Empty,
            Description     = json[nameof(Description)]?.ToObject<string>() ?? string.Empty,
            Priority        = json[nameof(Priority)]?.ToObject<ModPriority>() ?? ModPriority.Default,
            DefaultSettings = json[nameof(DefaultSettings)]?.ToObject<Setting>() ?? Setting.Zero,
            ObjectType      = json[nameof(ObjectType)]?.ToObject<ObjectType>() ?? ObjectType.Unknown,
            BodySlot        = json[nameof(BodySlot)]?.ToObject<BodySlot>() ?? BodySlot.Unknown,
            EquipSlot       = json[nameof(EquipSlot)]?.ToObject<EquipSlot>() ?? EquipSlot.Unknown,
            PrimaryId       = new PrimaryId(json[nameof(PrimaryId)]?.ToObject<ushort>() ?? 0),
            SecondaryId     = new SecondaryId(json[nameof(SecondaryId)]?.ToObject<ushort>() ?? 0),
            Variant         = new Variant(json[nameof(Variant)]?.ToObject<byte>() ?? 0),
            CanBeDisabled   = json[nameof(CanBeDisabled)]?.ToObject<bool>() ?? false,
            DefaultEntry    = json[nameof(DefaultEntry)]?.ToObject<ImcEntry>() ?? new ImcEntry(),
        };
        if (ret.Name.Length == 0)
            return null;

        if (options != null)
            foreach (var child in options.Children())
            {
                var subMod = new ImcSubMod(ret, child);
                ret.OptionData.Add(subMod);
            }

        if (!new ImcManipulation(ret.ObjectType, ret.BodySlot, ret.PrimaryId, ret.SecondaryId.Id, ret.Variant.Id, ret.EquipSlot,
                ret.DefaultEntry).Validate(true))
        {
            Penumbra.Messager.NotificationMessage($"Could not add IMC group because the associated IMC Entry is invalid.",
                NotificationType.Warning);
            return null;
        }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);
        return ret;
    }
}
