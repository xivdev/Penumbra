using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Structs;
using ImcEntry = Penumbra.GameData.Structs.ImcEntry;

namespace Penumbra.Meta.Manipulations;

[JsonConverter(typeof(Converter))]
public sealed class MetaDictionary : HashSet<MetaManipulation>
{
    public MetaDictionary()
    { }

    public MetaDictionary(int capacity)
        : base(capacity)
    { }

    private class Converter : JsonConverter<MetaDictionary>
    {
        public override void WriteJson(JsonWriter writer, MetaDictionary? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            foreach (var item in value)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Type");
                writer.WriteValue(item.ManipulationType.ToString());
                writer.WritePropertyName("Manipulation");
                serializer.Serialize(writer, item.Manipulation);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        public override MetaDictionary ReadJson(JsonReader reader, Type objectType, MetaDictionary? existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            var dict = existingValue ?? [];
            dict.Clear();
            var jObj = JArray.Load(reader);
            foreach (var item in jObj)
            {
                var type = item["Type"]?.ToObject<MetaManipulation.Type>() ?? MetaManipulation.Type.Unknown;
                if (type is MetaManipulation.Type.Unknown)
                {
                    Penumbra.Log.Warning($"Invalid Meta Manipulation Type {type} encountered.");
                    continue;
                }

                if (item["Manipulation"] is not JObject manip)
                {
                    Penumbra.Log.Warning($"Manipulation of type {type} does not contain manipulation data.");
                    continue;
                }

                switch (type)
                {
                    case MetaManipulation.Type.Imc:
                    {
                        var identifier = ImcIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<ImcEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.Add(new MetaManipulation(new ImcManipulation(identifier.Value, entry.Value)));
                        else
                            Penumbra.Log.Warning("Invalid IMC Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.Eqdp:
                    {
                        var identifier = EqdpIdentifier.FromJson(manip);
                        var entry      = (EqdpEntry?)manip["Entry"]?.ToObject<ushort>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.Add(new MetaManipulation(new EqdpManipulation(identifier.Value, entry.Value)));
                        else
                            Penumbra.Log.Warning("Invalid EQDP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.Eqp:
                    {
                        var identifier = EqpIdentifier.FromJson(manip);
                        var entry      = (EqpEntry?)manip["Entry"]?.ToObject<ulong>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.Add(new MetaManipulation(new EqpManipulation(identifier.Value, entry.Value)));
                        else
                            Penumbra.Log.Warning("Invalid EQP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.Est:
                    {
                        var identifier = EstIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<EstEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.Add(new MetaManipulation(new EstManipulation(identifier.Value, entry.Value)));
                        else
                            Penumbra.Log.Warning("Invalid EST Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.Gmp:
                    {
                        var identifier = GmpIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<GmpEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.Add(new MetaManipulation(new GmpManipulation(identifier.Value, entry.Value)));
                        else
                            Penumbra.Log.Warning("Invalid GMP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.Rsp:
                    {
                        var identifier = RspIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<RspEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.Add(new MetaManipulation(new RspManipulation(identifier.Value, entry.Value)));
                        else
                            Penumbra.Log.Warning("Invalid RSP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.GlobalEqp:
                    {
                        var identifier = GlobalEqpManipulation.FromJson(manip);
                        if (identifier.HasValue)
                            dict.Add(new MetaManipulation(identifier.Value));
                        else
                            Penumbra.Log.Warning("Invalid Global EQP Manipulation encountered.");
                        break;
                    }
                }
            }

            return dict;
        }
    }
}
