using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Structs;
using Penumbra.Util;
using ImcEntry = Penumbra.GameData.Structs.ImcEntry;

namespace Penumbra.Meta.Manipulations;

[JsonConverter(typeof(Converter))]
public sealed class MetaDictionary : IEnumerable<MetaManipulation>
{
    private readonly Dictionary<ImcIdentifier, ImcEntry>   _imc       = [];
    private readonly Dictionary<EqpIdentifier, EqpEntry>   _eqp       = [];
    private readonly Dictionary<EqdpIdentifier, EqdpEntry> _eqdp      = [];
    private readonly Dictionary<EstIdentifier, EstEntry>   _est       = [];
    private readonly Dictionary<RspIdentifier, RspEntry>   _rsp       = [];
    private readonly Dictionary<GmpIdentifier, GmpEntry>   _gmp       = [];
    private readonly HashSet<GlobalEqpManipulation>        _globalEqp = [];

    public int Count { get; private set; }

    public void Clear()
    {
        _imc.Clear();
        _eqp.Clear();
        _eqdp.Clear();
        _est.Clear();
        _rsp.Clear();
        _gmp.Clear();
        _globalEqp.Clear();
    }

    public IEnumerator<MetaManipulation> GetEnumerator()
        => _imc.Select(kvp => new MetaManipulation(new ImcManipulation(kvp.Key, kvp.Value)))
            .Concat(_eqp.Select(kvp => new MetaManipulation(new EqpManipulation(kvp.Key, kvp.Value))))
            .Concat(_eqdp.Select(kvp => new MetaManipulation(new EqdpManipulation(kvp.Key, kvp.Value))))
            .Concat(_est.Select(kvp => new MetaManipulation(new EstManipulation(kvp.Key, kvp.Value))))
            .Concat(_rsp.Select(kvp => new MetaManipulation(new RspManipulation(kvp.Key, kvp.Value))))
            .Concat(_gmp.Select(kvp => new MetaManipulation(new GmpManipulation(kvp.Key, kvp.Value))))
            .Concat(_globalEqp.Select(manip => new MetaManipulation(manip))).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public bool Add(MetaManipulation manip)
    {
        var ret = manip.ManipulationType switch
        {
            MetaManipulation.Type.Imc       => _imc.TryAdd(manip.Imc.Identifier, manip.Imc.Entry),
            MetaManipulation.Type.Eqdp      => _eqdp.TryAdd(manip.Eqdp.Identifier, manip.Eqdp.Entry),
            MetaManipulation.Type.Eqp       => _eqp.TryAdd(manip.Eqp.Identifier, manip.Eqp.Entry),
            MetaManipulation.Type.Est       => _est.TryAdd(manip.Est.Identifier, manip.Est.Entry),
            MetaManipulation.Type.Gmp       => _gmp.TryAdd(manip.Gmp.Identifier, manip.Gmp.Entry),
            MetaManipulation.Type.Rsp       => _rsp.TryAdd(manip.Rsp.Identifier, manip.Rsp.Entry),
            MetaManipulation.Type.GlobalEqp => _globalEqp.Add(manip.GlobalEqp),
            _                               => false,
        };

        if (ret)
            ++Count;
        return ret;
    }

    public bool TryAdd(ImcIdentifier identifier, ImcEntry entry)
    {
        if (!_imc.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(EqpIdentifier identifier, EqpEntry entry)
    {
        if (!_eqp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(EqdpIdentifier identifier, EqdpEntry entry)
    {
        if (!_eqdp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(EstIdentifier identifier, EstEntry entry)
    {
        if (!_est.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(GmpIdentifier identifier, GmpEntry entry)
    {
        if (!_gmp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(RspIdentifier identifier, RspEntry entry)
    {
        if (!_rsp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(GlobalEqpManipulation identifier)
    {
        if (!_globalEqp.Add(identifier))
            return false;

        ++Count;
        return true;
    }

    public void UnionWith(MetaDictionary manips)
    {
        foreach (var (identifier, entry) in manips._imc)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._eqp)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._eqdp)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._gmp)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._rsp)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._est)
            TryAdd(identifier, entry);

        foreach (var identifier in manips._globalEqp)
            TryAdd(identifier);
    }

    public bool TryGetValue(MetaManipulation identifier, out MetaManipulation oldValue)
    {
        switch (identifier.ManipulationType)
        {
            case MetaManipulation.Type.Imc:
                if (_imc.TryGetValue(identifier.Imc.Identifier, out var oldImc))
                {
                    oldValue = new MetaManipulation(new ImcManipulation(identifier.Imc.Identifier, oldImc));
                    return true;
                }

                break;
            case MetaManipulation.Type.Eqdp:
                if (_eqp.TryGetValue(identifier.Eqp.Identifier, out var oldEqdp))
                {
                    oldValue = new MetaManipulation(new EqpManipulation(identifier.Eqp.Identifier, oldEqdp));
                    return true;
                }

                break;
            case MetaManipulation.Type.Eqp:
                if (_eqdp.TryGetValue(identifier.Eqdp.Identifier, out var oldEqp))
                {
                    oldValue = new MetaManipulation(new EqdpManipulation(identifier.Eqdp.Identifier, oldEqp));
                    return true;
                }

                break;
            case MetaManipulation.Type.Est:
                if (_est.TryGetValue(identifier.Est.Identifier, out var oldEst))
                {
                    oldValue = new MetaManipulation(new EstManipulation(identifier.Est.Identifier, oldEst));
                    return true;
                }

                break;
            case MetaManipulation.Type.Gmp:
                if (_gmp.TryGetValue(identifier.Gmp.Identifier, out var oldGmp))
                {
                    oldValue = new MetaManipulation(new GmpManipulation(identifier.Gmp.Identifier, oldGmp));
                    return true;
                }

                break;
            case MetaManipulation.Type.Rsp:
                if (_rsp.TryGetValue(identifier.Rsp.Identifier, out var oldRsp))
                {
                    oldValue = new MetaManipulation(new RspManipulation(identifier.Rsp.Identifier, oldRsp));
                    return true;
                }

                break;
            case MetaManipulation.Type.GlobalEqp:
                if (_globalEqp.TryGetValue(identifier.GlobalEqp, out var oldGlobalEqp))
                {
                    oldValue = new MetaManipulation(oldGlobalEqp);
                    return true;
                }

                break;
        }

        oldValue = default;
        return false;
    }

    public void SetTo(MetaDictionary other)
    {
        _imc.SetTo(other._imc);
        _eqp.SetTo(other._eqp);
        _eqdp.SetTo(other._eqdp);
        _est.SetTo(other._est);
        _rsp.SetTo(other._rsp);
        _gmp.SetTo(other._gmp);
        _globalEqp.SetTo(other._globalEqp);
        Count = _imc.Count + _eqp.Count + _eqdp.Count + _est.Count + _rsp.Count + _gmp.Count + _globalEqp.Count;
    }

    public MetaDictionary Clone()
    {
        var ret = new MetaDictionary();
        ret.SetTo(this);
        return ret;
    }

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
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid IMC Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.Eqdp:
                    {
                        var identifier = EqdpIdentifier.FromJson(manip);
                        var entry      = (EqdpEntry?)manip["Entry"]?.ToObject<ushort>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid EQDP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.Eqp:
                    {
                        var identifier = EqpIdentifier.FromJson(manip);
                        var entry      = (EqpEntry?)manip["Entry"]?.ToObject<ulong>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid EQP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.Est:
                    {
                        var identifier = EstIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<EstEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid EST Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.Gmp:
                    {
                        var identifier = GmpIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<GmpEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid GMP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.Rsp:
                    {
                        var identifier = RspIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<RspEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid RSP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulation.Type.GlobalEqp:
                    {
                        var identifier = GlobalEqpManipulation.FromJson(manip);
                        if (identifier.HasValue)
                            dict.TryAdd(identifier.Value);
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
