using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public readonly record struct ShpIdentifier(HumanSlot Slot, PrimaryId? Id, ShapeString Shape)
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


        return Shape.CompareTo(other.Shape);
    }

    public override string ToString()
        => $"Shp - {Shape}{(Slot is HumanSlot.Unknown ? " - All Slots & IDs" : $" - {Slot.ToName()}{(Id.HasValue ? $" - {Id.Value.Id}" : " - All IDs")}")}";

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

        if (Slot is HumanSlot.Unknown && Id is not null)
            return false;

        return ValidateCustomShapeString(Shape);
    }

    public static unsafe bool ValidateCustomShapeString(byte* shape)
    {
        // "shpx_*"
        if (shape is null)
            return false;

        if (*shape++ is not (byte)'s'
         || *shape++ is not (byte)'h'
         || *shape++ is not (byte)'p'
         || *shape++ is not (byte)'x'
         || *shape++ is not (byte)'_'
         || *shape is 0)
            return false;

        return true;
    }

    public static bool ValidateCustomShapeString(in ShapeString shape)
    {
        // "shpx_*"
        if (shape.Length < 6)
            return false;

        var span = shape.AsSpan;
        if (span[0] is not (byte)'s'
         || span[1] is not (byte)'h'
         || span[2] is not (byte)'p'
         || span[3] is not (byte)'x'
         || span[4] is not (byte)'_')
            return false;

        return true;
    }

    public JObject AddToJson(JObject jObj)
    {
        if (Slot is not HumanSlot.Unknown)
            jObj["Slot"] = Slot.ToString();
        if (Id.HasValue)
            jObj["Id"] = Id.Value.Id.ToString();
        jObj["Shape"] = Shape.ToString();
        return jObj;
    }

    public static ShpIdentifier? FromJson(JObject jObj)
    {
        var slot  = jObj["Slot"]?.ToObject<HumanSlot>() ?? HumanSlot.Unknown;
        var id    = jObj["Id"]?.ToObject<ushort>();
        var shape = jObj["Shape"]?.ToObject<string>();
        if (shape is null || !ShapeString.TryRead(shape, out var shapeString))
            return null;

        var identifier = new ShpIdentifier(slot, id, shapeString);
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
