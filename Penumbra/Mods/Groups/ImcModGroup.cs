using Newtonsoft.Json;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Groups;

public class ImcModGroup(Mod mod) : IModGroup
{
    public const int DisabledIndex = 30;
    public const int NumAttributes = 10;

    public Mod    Mod         { get; }      = mod;
    public string Name        { get; set; } = "Option";
    public string Description { get; set; } = "A single IMC manipulation.";

    public GroupType Type
        => GroupType.Imc;

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

    public IModOption? AddOption(string name, string description = "")
    {
        uint fullMask   = GetFullMask();
        var  firstUnset = (byte)BitOperations.TrailingZeroCount(~fullMask);
        // All attributes handled.
        if (firstUnset >= NumAttributes)
            return null;

        var groupIdx = Mod.Groups.IndexOf(this);
        if (groupIdx < 0)
            return null;

        var subMod = new ImcSubMod(this)
        {
            Name           = name,
            Description    = description,
            AttributeIndex = firstUnset,
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

    private ushort GetCurrentMask(Setting setting)
    {
        var mask = DefaultEntry.AttributeMask;
        for (var i = 0; i < OptionData.Count; ++i)
        {
            if (!setting.HasFlag(i))
                continue;

            var option = OptionData[i];
            mask |= option.Attribute;
        }

        return mask;
    }

    private ushort GetFullMask()
        => GetCurrentMask(Setting.AllBits(63));

    private ImcManipulation GetManip(ushort mask)
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
            jWriter.WritePropertyName(nameof(option.AttributeIndex));
            jWriter.WriteValue(option.AttributeIndex);
            jWriter.WriteEndObject();
        }

        jWriter.WriteEndArray();
        jWriter.WriteEndObject();
    }

    public (int Redirections, int Swaps, int Manips) GetCounts()
        => (0, 0, 1);
}
