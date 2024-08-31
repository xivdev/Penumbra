using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Structs;
using Penumbra.Util;
using ImcEntry = Penumbra.GameData.Structs.ImcEntry;

namespace Penumbra.Meta.Manipulations;

[JsonConverter(typeof(Converter))]
public class MetaDictionary
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

    public IReadOnlyDictionary<EqpIdentifier, EqpEntryInternal> Eqp
        => _eqp;

    public IReadOnlyDictionary<EqdpIdentifier, EqdpEntryInternal> Eqdp
        => _eqdp;

    public IReadOnlyDictionary<EstIdentifier, EstEntry> Est
        => _est;

    public IReadOnlyDictionary<GmpIdentifier, GmpEntry> Gmp
        => _gmp;

    public IReadOnlyDictionary<RspIdentifier, RspEntry> Rsp
        => _rsp;

    public IReadOnlySet<GlobalEqpManipulation> GlobalEqp
        => _globalEqp;

    public int Count { get; private set; }

    public int GetCount(MetaManipulationType type)
        => type switch
        {
            MetaManipulationType.Imc       => _imc.Count,
            MetaManipulationType.Eqdp      => _eqdp.Count,
            MetaManipulationType.Eqp       => _eqp.Count,
            MetaManipulationType.Est       => _est.Count,
            MetaManipulationType.Gmp       => _gmp.Count,
            MetaManipulationType.Rsp       => _rsp.Count,
            MetaManipulationType.GlobalEqp => _globalEqp.Count,
            _                              => 0,
        };

    public bool Contains(IMetaIdentifier identifier)
        => identifier switch
        {
            EqdpIdentifier i        => _eqdp.ContainsKey(i),
            EqpIdentifier i         => _eqp.ContainsKey(i),
            EstIdentifier i         => _est.ContainsKey(i),
            GlobalEqpManipulation i => _globalEqp.Contains(i),
            GmpIdentifier i         => _gmp.ContainsKey(i),
            ImcIdentifier i         => _imc.ContainsKey(i),
            RspIdentifier i         => _rsp.ContainsKey(i),
            _                       => false,
        };

    public void Clear()
    {
        Count = 0;
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

    public IEnumerable<IMetaIdentifier> Identifiers
        => _imc.Keys.Cast<IMetaIdentifier>()
            .Concat(_eqdp.Keys.Cast<IMetaIdentifier>())
            .Concat(_eqp.Keys.Cast<IMetaIdentifier>())
            .Concat(_est.Keys.Cast<IMetaIdentifier>())
            .Concat(_gmp.Keys.Cast<IMetaIdentifier>())
            .Concat(_rsp.Keys.Cast<IMetaIdentifier>())
            .Concat(_globalEqp.Cast<IMetaIdentifier>());

    #region TryAdd

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

    #endregion

    #region Update

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

    #endregion

    #region TryGetValue

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

    #endregion

    public bool Remove(IMetaIdentifier identifier)
    {
        var ret = identifier switch
        {
            EqdpIdentifier i        => _eqdp.Remove(i),
            EqpIdentifier i         => _eqp.Remove(i),
            EstIdentifier i         => _est.Remove(i),
            GlobalEqpManipulation i => _globalEqp.Remove(i),
            GmpIdentifier i         => _gmp.Remove(i),
            ImcIdentifier i         => _imc.Remove(i),
            RspIdentifier i         => _rsp.Remove(i),
            _                       => false,
        };
        if (ret)
            --Count;
        return ret;
    }

    #region Merging

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
        return true;
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

    #endregion

    public MetaDictionary Clone()
    {
        var ret = new MetaDictionary();
        ret.SetTo(this);
        return ret;
    }

    public static JObject Serialize(EqpIdentifier identifier, EqpEntryInternal entry)
        => Serialize(identifier, entry.ToEntry(identifier.Slot));

    public static JObject Serialize(EqpIdentifier identifier, EqpEntry entry)
        => new()
        {
            ["Type"] = MetaManipulationType.Eqp.ToString(),
            ["Manipulation"] = identifier.AddToJson(new JObject
            {
                ["Entry"] = (ulong)entry,
            }),
        };

    public static JObject Serialize(EqdpIdentifier identifier, EqdpEntryInternal entry)
        => Serialize(identifier, entry.ToEntry(identifier.Slot));

    public static JObject Serialize(EqdpIdentifier identifier, EqdpEntry entry)
        => new()
        {
            ["Type"] = MetaManipulationType.Eqdp.ToString(),
            ["Manipulation"] = identifier.AddToJson(new JObject
            {
                ["Entry"] = (ushort)entry,
            }),
        };

    public static JObject Serialize(EstIdentifier identifier, EstEntry entry)
        => new()
        {
            ["Type"] = MetaManipulationType.Est.ToString(),
            ["Manipulation"] = identifier.AddToJson(new JObject
            {
                ["Entry"] = entry.Value,
            }),
        };

    public static JObject Serialize(GmpIdentifier identifier, GmpEntry entry)
        => new()
        {
            ["Type"] = MetaManipulationType.Gmp.ToString(),
            ["Manipulation"] = identifier.AddToJson(new JObject
            {
                ["Entry"] = JObject.FromObject(entry),
            }),
        };

    public static JObject Serialize(ImcIdentifier identifier, ImcEntry entry)
        => new()
        {
            ["Type"] = MetaManipulationType.Imc.ToString(),
            ["Manipulation"] = identifier.AddToJson(new JObject
            {
                ["Entry"] = JObject.FromObject(entry),
            }),
        };

    public static JObject Serialize(RspIdentifier identifier, RspEntry entry)
        => new()
        {
            ["Type"] = MetaManipulationType.Rsp.ToString(),
            ["Manipulation"] = identifier.AddToJson(new JObject
            {
                ["Entry"] = entry.Value,
            }),
        };

    public static JObject Serialize(GlobalEqpManipulation identifier)
        => new()
        {
            ["Type"]         = MetaManipulationType.GlobalEqp.ToString(),
            ["Manipulation"] = identifier.AddToJson(new JObject()),
        };

    public static JObject? Serialize<TIdentifier, TEntry>(TIdentifier identifier, TEntry entry)
        where TIdentifier : unmanaged, IMetaIdentifier
        where TEntry : unmanaged
    {
        if (typeof(TIdentifier) == typeof(EqpIdentifier) && typeof(TEntry) == typeof(EqpEntryInternal))
            return Serialize(Unsafe.As<TIdentifier, EqpIdentifier>(ref identifier), Unsafe.As<TEntry, EqpEntryInternal>(ref entry));
        if (typeof(TIdentifier) == typeof(EqpIdentifier) && typeof(TEntry) == typeof(EqpEntry))
            return Serialize(Unsafe.As<TIdentifier, EqpIdentifier>(ref identifier), Unsafe.As<TEntry, EqpEntry>(ref entry));
        if (typeof(TIdentifier) == typeof(EqdpIdentifier) && typeof(TEntry) == typeof(EqdpEntryInternal))
            return Serialize(Unsafe.As<TIdentifier, EqdpIdentifier>(ref identifier), Unsafe.As<TEntry, EqdpEntryInternal>(ref entry));
        if (typeof(TIdentifier) == typeof(EqdpIdentifier) && typeof(TEntry) == typeof(EqdpEntry))
            return Serialize(Unsafe.As<TIdentifier, EqdpIdentifier>(ref identifier), Unsafe.As<TEntry, EqdpEntry>(ref entry));
        if (typeof(TIdentifier) == typeof(EstIdentifier) && typeof(TEntry) == typeof(EstEntry))
            return Serialize(Unsafe.As<TIdentifier, EstIdentifier>(ref identifier), Unsafe.As<TEntry, EstEntry>(ref entry));
        if (typeof(TIdentifier) == typeof(GmpIdentifier) && typeof(TEntry) == typeof(GmpEntry))
            return Serialize(Unsafe.As<TIdentifier, GmpIdentifier>(ref identifier), Unsafe.As<TEntry, GmpEntry>(ref entry));
        if (typeof(TIdentifier) == typeof(RspIdentifier) && typeof(TEntry) == typeof(RspEntry))
            return Serialize(Unsafe.As<TIdentifier, RspIdentifier>(ref identifier), Unsafe.As<TEntry, RspEntry>(ref entry));
        if (typeof(TIdentifier) == typeof(ImcIdentifier) && typeof(TEntry) == typeof(ImcEntry))
            return Serialize(Unsafe.As<TIdentifier, ImcIdentifier>(ref identifier), Unsafe.As<TEntry, ImcEntry>(ref entry));
        if (typeof(TIdentifier) == typeof(GlobalEqpManipulation))
            return Serialize(Unsafe.As<TIdentifier, GlobalEqpManipulation>(ref identifier));

        return null;
    }

    public static JArray SerializeTo<TIdentifier, TEntry>(JArray array, IEnumerable<KeyValuePair<TIdentifier, TEntry>> manipulations)
        where TIdentifier : unmanaged, IMetaIdentifier
        where TEntry : unmanaged
    {
        foreach (var (identifier, entry) in manipulations)
        {
            if (Serialize(identifier, entry) is { } jObj)
                array.Add(jObj);
        }

        return array;
    }

    public static JArray SerializeTo(JArray array, IEnumerable<GlobalEqpManipulation> manipulations)
    {
        foreach (var manip in manipulations)
            array.Add(Serialize(manip));

        return array;
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

            var array = new JArray();
            SerializeTo(array, value._imc);
            SerializeTo(array, value._eqp);
            SerializeTo(array, value._eqdp);
            SerializeTo(array, value._est);
            SerializeTo(array, value._rsp);
            SerializeTo(array, value._gmp);
            SerializeTo(array, value._globalEqp);
            array.WriteTo(writer);
        }

        public override MetaDictionary ReadJson(JsonReader reader, Type objectType, MetaDictionary? existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            var dict = existingValue ?? new MetaDictionary();
            dict.Clear();
            var jObj = JArray.Load(reader);
            foreach (var item in jObj)
            {
                var type = item["Type"]?.ToObject<MetaManipulationType>() ?? MetaManipulationType.Unknown;
                if (type is MetaManipulationType.Unknown)
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
                    case MetaManipulationType.Imc:
                    {
                        var identifier = ImcIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<ImcEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid IMC Manipulation encountered.");
                        break;
                    }
                    case MetaManipulationType.Eqdp:
                    {
                        var identifier = EqdpIdentifier.FromJson(manip);
                        var entry      = (EqdpEntry?)manip["Entry"]?.ToObject<ushort>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid EQDP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulationType.Eqp:
                    {
                        var identifier = EqpIdentifier.FromJson(manip);
                        var entry      = (EqpEntry?)manip["Entry"]?.ToObject<ulong>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid EQP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulationType.Est:
                    {
                        var identifier = EstIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<EstEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid EST Manipulation encountered.");
                        break;
                    }
                    case MetaManipulationType.Gmp:
                    {
                        var identifier = GmpIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<GmpEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid GMP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulationType.Rsp:
                    {
                        var identifier = RspIdentifier.FromJson(manip);
                        var entry      = manip["Entry"]?.ToObject<RspEntry>();
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid RSP Manipulation encountered.");
                        break;
                    }
                    case MetaManipulationType.GlobalEqp:
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

    public MetaDictionary()
    { }

    public MetaDictionary(MetaCache? cache)
    {
        if (cache == null)
            return;

        _imc       = cache.Imc.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);
        _eqp       = cache.Eqp.ToDictionary(kvp => kvp.Key, kvp => new EqpEntryInternal(kvp.Value.Entry, kvp.Key.Slot));
        _eqdp      = cache.Eqdp.ToDictionary(kvp => kvp.Key, kvp => new EqdpEntryInternal(kvp.Value.Entry, kvp.Key.Slot));
        _est       = cache.Est.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);
        _gmp       = cache.Gmp.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);
        _rsp       = cache.Rsp.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);
        _globalEqp = cache.GlobalEqp.Select(kvp => kvp.Key).ToHashSet();
        Count      = cache.Count;
    }
}
