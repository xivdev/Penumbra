using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public readonly record struct RspIdentifier(SubRace SubRace, RspAttribute Attribute) : IMetaIdentifier
{
    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData?> changedItems)
        => changedItems.TryAdd($"{SubRace.ToName()} {Attribute.ToFullString()}", null);

    public MetaIndex FileIndex()
        => MetaIndex.HumanCmp;

    public bool Validate()
        => SubRace is not SubRace.Unknown
         && Enum.IsDefined(SubRace)
         && Attribute is not RspAttribute.NumAttributes
         && Enum.IsDefined(Attribute);

    public JObject AddToJson(JObject jObj)
    {
        jObj["SubRace"]   = SubRace.ToString();
        jObj["Attribute"] = Attribute.ToString();
        return jObj;
    }

    public static RspIdentifier? FromJson(JObject? jObj)
    {
        if (jObj == null)
            return null;

        var subRace   = jObj["SubRace"]?.ToObject<SubRace>() ?? SubRace.Unknown;
        var attribute = jObj["Attribute"]?.ToObject<RspAttribute>() ?? RspAttribute.NumAttributes;
        var ret       = new RspIdentifier(subRace, attribute);
        return ret.Validate() ? ret : null;
    }

    public MetaManipulationType Type
        => MetaManipulationType.Rsp;
}

[JsonConverter(typeof(Converter))]
public readonly record struct RspEntry(float Value) : IComparisonOperators<RspEntry, RspEntry, bool>
{
    public const           float    MinValue = 0.01f;
    public const           float    MaxValue = 512f;
    public static readonly RspEntry One      = new(1f);

    public bool Validate()
        => Value is >= MinValue and <= MaxValue;

    private class Converter : JsonConverter<RspEntry>
    {
        public override void WriteJson(JsonWriter writer, RspEntry value, JsonSerializer serializer)
            => serializer.Serialize(writer, value.Value);

        public override RspEntry ReadJson(JsonReader reader, Type objectType, RspEntry existingValue, bool hasExistingValue,
            JsonSerializer serializer)
            => new(serializer.Deserialize<float>(reader));
    }

    public static bool operator >(RspEntry left, RspEntry right)
        => left.Value > right.Value;

    public static bool operator >=(RspEntry left, RspEntry right)
        => left.Value >= right.Value;

    public static bool operator <(RspEntry left, RspEntry right)
        => left.Value < right.Value;

    public static bool operator <=(RspEntry left, RspEntry right)
        => left.Value <= right.Value;
}
