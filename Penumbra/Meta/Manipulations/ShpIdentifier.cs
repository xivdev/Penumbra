using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[JsonConverter(typeof(StringEnumConverter))]
public enum ShapeConnectorCondition : byte
{
    None   = 0,
    Wrists = 1,
    Waist  = 2,
    Ankles = 3,
}

public readonly record struct ShpIdentifier(
    HumanSlot Slot,
    PrimaryId? Id,
    ShapeAttributeString Shape,
    ShapeConnectorCondition ConnectorCondition,
    GenderRace GenderRaceCondition)
    : IComparable<ShpIdentifier>, IMetaIdentifier
{
    public int CompareTo(ShpIdentifier other)
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

        var conditionComparison = ConnectorCondition.CompareTo(other.ConnectorCondition);
        if (conditionComparison is not 0)
            return conditionComparison;

        var genderRaceComparison = GenderRaceCondition.CompareTo(other.GenderRaceCondition);
        if (genderRaceComparison is not 0)
            return genderRaceComparison;

        return Shape.CompareTo(other.Shape);
    }


    public override string ToString()
    {
        var sb = new StringBuilder(64);
        sb.Append("SHP - ")
            .Append(Shape);
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

        switch (ConnectorCondition)
        {
            case ShapeConnectorCondition.Wrists: sb.Append(" - Wrist Connector"); break;
            case ShapeConnectorCondition.Waist:  sb.Append(" - Waist Connector"); break;
            case ShapeConnectorCondition.Ankles: sb.Append(" - Ankle Connector"); break;
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

        if (!Enum.IsDefined(ConnectorCondition))
            return false;

        if (Slot is HumanSlot.Unknown && Id is not null)
            return false;

        if (Slot.ToSpecificEnum() is BodySlot && Id is { Id: > byte.MaxValue })
            return false;

        if (Id is { Id: > ExpandedEqpGmpBase.Count - 1 })
            return false;

        if (!Shape.ValidateCustomShapeString())
            return false;

        return ConnectorCondition switch
        {
            ShapeConnectorCondition.None   => true,
            ShapeConnectorCondition.Wrists => Slot is HumanSlot.Body or HumanSlot.Hands or HumanSlot.Unknown,
            ShapeConnectorCondition.Waist  => Slot is HumanSlot.Body or HumanSlot.Legs or HumanSlot.Unknown,
            ShapeConnectorCondition.Ankles => Slot is HumanSlot.Legs or HumanSlot.Feet or HumanSlot.Unknown,
            _                              => false,
        };
    }

    public JObject AddToJson(JObject jObj)
    {
        if (Slot is not HumanSlot.Unknown)
            jObj["Slot"] = Slot.ToString();
        if (Id.HasValue)
            jObj["Id"] = Id.Value.Id.ToString();
        jObj["Shape"] = Shape.ToString();
        if (ConnectorCondition is not ShapeConnectorCondition.None)
            jObj["ConnectorCondition"] = ConnectorCondition.ToString();
        if (GenderRaceCondition is not GenderRace.Unknown)
            jObj["GenderRaceCondition"] = (uint)GenderRaceCondition;
        return jObj;
    }

    public static ShpIdentifier? FromJson(JObject jObj)
    {
        var shape = jObj["Shape"]?.ToObject<string>();
        if (shape is null || !ShapeAttributeString.TryRead(shape, out var shapeString))
            return null;

        var slot                = jObj["Slot"]?.ToObject<HumanSlot>() ?? HumanSlot.Unknown;
        var id                  = jObj["Id"]?.ToObject<ushort>();
        var connectorCondition  = jObj["ConnectorCondition"]?.ToObject<ShapeConnectorCondition>() ?? ShapeConnectorCondition.None;
        var genderRaceCondition = jObj["GenderRaceCondition"]?.ToObject<GenderRace>() ?? 0;
        var identifier          = new ShpIdentifier(slot, id, shapeString, connectorCondition, genderRaceCondition);
        return identifier.Validate() ? identifier : null;
    }

    public MetaManipulationType Type
        => MetaManipulationType.Shp;
}

[JsonConverter(typeof(Converter))]
public readonly record struct ShpEntry(bool Value)
{
    public static readonly ShpEntry True  = new(true);
    public static readonly ShpEntry False = new(false);

    private class Converter : JsonConverter<ShpEntry>
    {
        public override void WriteJson(JsonWriter writer, ShpEntry value, JsonSerializer serializer)
            => serializer.Serialize(writer, value.Value);

        public override ShpEntry ReadJson(JsonReader reader, Type objectType, ShpEntry existingValue, bool hasExistingValue,
            JsonSerializer serializer)
            => new(serializer.Deserialize<bool>(reader));
    }
}
