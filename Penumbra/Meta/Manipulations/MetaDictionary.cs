using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Structs;
using Penumbra.Util;
using ImcEntry = Penumbra.GameData.Structs.ImcEntry;

namespace Penumbra.Meta.Manipulations;

[JsonConverter(typeof(Converter))]
public class MetaDictionary : IEnumerable<MetaManipulation>
{
    private readonly Dictionary<ImcIdentifier, ImcEntry>           _imc       = [];
    private readonly Dictionary<EqpIdentifier, EqpEntryInternal>   _eqp       = [];
    private readonly Dictionary<EqdpIdentifier, EqdpEntryInternal> _eqdp      = [];
    private readonly Dictionary<EstIdentifier, EstEntry>           _est       = [];
    private readonly Dictionary<RspIdentifier, RspEntry>           _rsp       = [];
    private readonly Dictionary<GmpIdentifier, GmpEntry>           _gmp       = [];
    private readonly HashSet<GlobalEqpManipulation>                _globalEqp = [];

    public IReadOnlyDictionary<ImcIdentifier, ImcEntry> Imc
        => _imc;

    public int Count { get; private set; }

    public int GetCount(MetaManipulation.Type type)
        => type switch
        {
            MetaManipulation.Type.Imc       => _imc.Count,
            MetaManipulation.Type.Eqdp      => _eqdp.Count,
            MetaManipulation.Type.Eqp       => _eqp.Count,
            MetaManipulation.Type.Est       => _est.Count,
            MetaManipulation.Type.Gmp       => _gmp.Count,
            MetaManipulation.Type.Rsp       => _rsp.Count,
            MetaManipulation.Type.GlobalEqp => _globalEqp.Count,
            _                               => 0,
        };

    public bool CanAdd(IMetaIdentifier identifier)
        => identifier switch
        {
            EqdpIdentifier eqdpIdentifier               => !_eqdp.ContainsKey(eqdpIdentifier),
            EqpIdentifier eqpIdentifier                 => !_eqp.ContainsKey(eqpIdentifier),
            EstIdentifier estIdentifier                 => !_est.ContainsKey(estIdentifier),
            GlobalEqpManipulation globalEqpManipulation => !_globalEqp.Contains(globalEqpManipulation),
            GmpIdentifier gmpIdentifier                 => !_gmp.ContainsKey(gmpIdentifier),
            ImcIdentifier imcIdentifier                 => !_imc.ContainsKey(imcIdentifier),
            RspIdentifier rspIdentifier                 => !_rsp.ContainsKey(rspIdentifier),
            _                                           => false,
        };

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

    public bool Equals(MetaDictionary other)
        => Count == other.Count
         && _imc.SetEquals(other._imc)
         && _eqp.SetEquals(other._eqp)
         && _eqdp.SetEquals(other._eqdp)
         && _est.SetEquals(other._est)
         && _rsp.SetEquals(other._rsp)
         && _gmp.SetEquals(other._gmp)
         && _globalEqp.SetEquals(other._globalEqp);

    public IEnumerator<MetaManipulation> GetEnumerator()
        => _imc.Select(kvp => new MetaManipulation(new ImcManipulation(kvp.Key, kvp.Value)))
            .Concat(_eqp.Select(kvp => new MetaManipulation(new EqpManipulation(kvp.Key, kvp.Value.ToEntry(kvp.Key.Slot)))))
            .Concat(_eqdp.Select(kvp => new MetaManipulation(new EqdpManipulation(kvp.Key, kvp.Value.ToEntry(kvp.Key.Slot)))))
            .Concat(_est.Select(kvp => new MetaManipulation(new EstManipulation(kvp.Key, kvp.Value))))
            .Concat(_rsp.Select(kvp => new MetaManipulation(new RspManipulation(kvp.Key, kvp.Value))))
            .Concat(_gmp.Select(kvp => new MetaManipulation(new GmpManipulation(kvp.Key, kvp.Value))))
            .Concat(_globalEqp.Select(manip => new MetaManipulation(manip))).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public bool TryAdd(ImcIdentifier identifier, ImcEntry entry)
    {
        if (!_imc.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }


    public bool TryAdd(EqpIdentifier identifier, EqpEntryInternal entry)
    {
        if (!_eqp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(EqpIdentifier identifier, EqpEntry entry)
        => TryAdd(identifier, new EqpEntryInternal(entry, identifier.Slot));


    public bool TryAdd(EqdpIdentifier identifier, EqdpEntryInternal entry)
    {
        if (!_eqdp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(EqdpIdentifier identifier, EqdpEntry entry)
        => TryAdd(identifier, new EqdpEntryInternal(entry, identifier.Slot));

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

    public bool Update(ImcIdentifier identifier, ImcEntry entry)
    {
        if (!_imc.ContainsKey(identifier))
            return false;

        _imc[identifier] = entry;
        return true;
    }


    public bool Update(EqpIdentifier identifier, EqpEntryInternal entry)
    {
        if (!_eqp.ContainsKey(identifier))
            return false;

        _eqp[identifier] = entry;
        return true;
    }

    public bool Update(EqpIdentifier identifier, EqpEntry entry)
        => Update(identifier, new EqpEntryInternal(entry, identifier.Slot));


    public bool Update(EqdpIdentifier identifier, EqdpEntryInternal entry)
    {
        if (!_eqdp.ContainsKey(identifier))
            return false;

        _eqdp[identifier] = entry;
        return true;
    }

    public bool Update(EqdpIdentifier identifier, EqdpEntry entry)
        => Update(identifier, new EqdpEntryInternal(entry, identifier.Slot));

    public bool Update(EstIdentifier identifier, EstEntry entry)
    {
        if (!_est.ContainsKey(identifier))
            return false;

        _est[identifier] = entry;
        return true;
    }

    public bool Update(GmpIdentifier identifier, GmpEntry entry)
    {
        if (!_gmp.ContainsKey(identifier))
            return false;

        _gmp[identifier] = entry;
        return true;
    }

    public bool Update(RspIdentifier identifier, RspEntry entry)
    {
        if (!_rsp.ContainsKey(identifier))
            return false;

        _rsp[identifier] = entry;
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

    /// <summary> Try to merge all manipulations from manips into this, and return the first failure, if any. </summary>
    public bool MergeForced(MetaDictionary manips, out IMetaIdentifier? failedIdentifier)
    {
        foreach (var (identifier, _) in manips._imc.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._eqp.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._eqdp.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._gmp.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._rsp.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._est.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var identifier in manips._globalEqp.Where(identifier => !TryAdd(identifier)))
        {
            failedIdentifier = identifier;
            return false;
        }

        failedIdentifier = default;
        return false;
    }

    public bool TryGetValue(EstIdentifier identifier, out EstEntry value)
        => _est.TryGetValue(identifier, out value);

    public bool TryGetValue(EqpIdentifier identifier, out EqpEntryInternal value)
        => _eqp.TryGetValue(identifier, out value);

    public bool TryGetValue(EqdpIdentifier identifier, out EqdpEntryInternal value)
        => _eqdp.TryGetValue(identifier, out value);

    public bool TryGetValue(GmpIdentifier identifier, out GmpEntry value)
        => _gmp.TryGetValue(identifier, out value);

    public bool TryGetValue(RspIdentifier identifier, out RspEntry value)
        => _rsp.TryGetValue(identifier, out value);

    public bool TryGetValue(ImcIdentifier identifier, out ImcEntry value)
        => _imc.TryGetValue(identifier, out value);

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

    public void UpdateTo(MetaDictionary other)
    {
        _imc.UpdateTo(other._imc);
        _eqp.UpdateTo(other._eqp);
        _eqdp.UpdateTo(other._eqdp);
        _est.UpdateTo(other._est);
        _rsp.UpdateTo(other._rsp);
        _gmp.UpdateTo(other._gmp);
        _globalEqp.UnionWith(other._globalEqp);
        Count = _imc.Count + _eqp.Count + _eqdp.Count + _est.Count + _rsp.Count + _gmp.Count + _globalEqp.Count;
    }

    public MetaDictionary Clone()
    {
        var ret = new MetaDictionary();
        ret.SetTo(this);
        return ret;
    }

    private static void WriteJson(JsonWriter writer, JsonSerializer serializer, IMetaIdentifier identifier, object entry)
    {
        var type = identifier switch
        {
            ImcIdentifier         => "Imc",
            EqdpIdentifier        => "Eqdp",
            EqpIdentifier         => "Eqp",
            EstIdentifier         => "Est",
            GmpIdentifier         => "Gmp",
            RspIdentifier         => "Rsp",
            GlobalEqpManipulation => "GlobalEqp",
            _                     => string.Empty,
        };

        if (type.Length == 0)
            return;

        writer.WriteStartObject();
        writer.WritePropertyName("Type");
        writer.WriteValue(type);
        writer.WritePropertyName("Manipulation");

        writer.WriteEndObject();
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
