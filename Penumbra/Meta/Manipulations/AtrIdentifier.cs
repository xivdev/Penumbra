using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

public readonly record struct AtrIdentifier(HumanSlot Slot, PrimaryId? Id, ShapeAttributeString Attribute, GenderRace GenderRaceCondition)
    : IComparable<AtrIdentifier>, IMetaIdentifier
{
    public int CompareTo(AtrIdentifier other)
    {
        var slotComparison = Slot.CompareTo(other.Slot);
        if (slotComparison is not 0)
            return slotComparison;

        if (Id.HasValue)
        {
            if (other.Id.HasValue)
            {
                var idComparison = Id.Value.Id.CompareTo(other.Id.Value.Id);
                if (idComparison is not 0)
                    return idComparison;
            }
            else
            {
                return -1;
            }
        }
        else if (other.Id.HasValue)
        {
            return 1;
        }

        var genderRaceComparison = GenderRaceCondition.CompareTo(other.GenderRaceCondition);
        if (genderRaceComparison is not 0)
            return genderRaceComparison;

        return Attribute.CompareTo(other.Attribute);
    }


    public override string ToString()
    {
        var sb = new StringBuilder(64);
        sb.Append("ATR - ")
            .Append(Attribute);
        if (Slot is HumanSlot.Unknown)
        {
            sb.Append(" - All Slots & IDs");
        }
        else
        {
            sb.Append(" - ")
                .Append(Slot.ToName())
                .Append(" - ");
            if (Id.HasValue)
                sb.Append(Id.Value.Id);
            else
                sb.Append("All IDs");
        }

        if (GenderRaceCondition is not GenderRace.Unknown)
            sb.Append(" - ").Append(GenderRaceCondition.ToRaceCode());

        return sb.ToString();
    }

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData> changedItems)
    {
        // Nothing for now since it depends entirely on the shape key.
    }

    public MetaIndex FileIndex()
        => (MetaIndex)(-1);

    public bool Validate()
    {
        if (!Enum.IsDefined(Slot) || Slot is HumanSlot.UnkBonus)
            return false;

        if (!ShapeAttributeHashSet.GenderRaceIndices.ContainsKey(GenderRaceCondition))
            return false;

        if (Slot is HumanSlot.Unknown && Id is not null)
            return false;

        if (Slot.ToSpecificEnum() is BodySlot && Id is { Id: > byte.MaxValue })
            return false;

        if (Id is { Id: > ExpandedEqpGmpBase.Count - 1 })
            return false;

        return Attribute.ValidateCustomAttributeString();
    }

    public JObject AddToJson(JObject jObj)
    {
        if (Slot is not HumanSlot.Unknown)
            jObj["Slot"] = Slot.ToString();
        if (Id.HasValue)
            jObj["Id"] = Id.Value.Id.ToString();
        jObj["Attribute"] = Attribute.ToString();
        if (GenderRaceCondition is not GenderRace.Unknown)
            jObj["GenderRaceCondition"] = (uint)GenderRaceCondition;
        return jObj;
    }

    public static AtrIdentifier? FromJson(JObject jObj)
    {
        var attribute = jObj["Attribute"]?.ToObject<string>();
        if (attribute is null || !ShapeAttributeString.TryRead(attribute, out var attributeString))
            return null;

        var slot                = jObj["Slot"]?.ToObject<HumanSlot>() ?? HumanSlot.Unknown;
        var id                  = jObj["Id"]?.ToObject<ushort>();
        var genderRaceCondition = jObj["GenderRaceCondition"]?.ToObject<GenderRace>() ?? 0;
        var identifier          = new AtrIdentifier(slot, id, attributeString, genderRaceCondition);
        return identifier.Validate() ? identifier : null;
    }

    public MetaManipulationType Type
        => MetaManipulationType.Atr;
}

[JsonConverter(typeof(Converter))]
public readonly record struct AtrEntry(bool Value)
{
    public static readonly AtrEntry True  = new(true);
    public static readonly AtrEntry False = new(false);

    private class Converter : JsonConverter<AtrEntry>
    {
        public override void WriteJson(JsonWriter writer, AtrEntry value, JsonSerializer serializer)
            => serializer.Serialize(writer, value.Value);

        public override AtrEntry ReadJson(JsonReader reader, Type objectType, AtrEntry existingValue, bool hasExistingValue,
            JsonSerializer serializer)
            => new(serializer.Deserialize<bool>(reader));
    }
}
