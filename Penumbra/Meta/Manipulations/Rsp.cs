using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public readonly record struct RspIdentifier(SubRace SubRace, RspAttribute Attribute) : IMetaIdentifier
{
    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, object?> changedItems)
        => changedItems.TryAdd($"{SubRace.ToName()} {Attribute.ToFullString()}", null);

    public MetaIndex FileIndex()
        => throw new NotImplementedException();

    public bool Validate()
        => throw new NotImplementedException();

    public JObject AddToJson(JObject jObj)
        => throw new NotImplementedException();
}

[JsonConverter(typeof(Converter))]
public readonly record struct RspEntry(float Value) : IComparisonOperators<RspEntry, RspEntry, bool>
{
    public const           float    MinValue = 0.01f;
    public const           float    MaxValue = 512f;
    public static readonly RspEntry One      = new(1f);

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
