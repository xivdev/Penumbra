using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Files.AtchStructs;
using Penumbra.GameData.Structs;
using Penumbra.Util;
using ImcEntry = Penumbra.GameData.Structs.ImcEntry;

namespace Penumbra.Meta.Manipulations;

[JsonConverter(typeof(Converter))]
public class MetaDictionary
{
    private class Wrapper : HashSet<GlobalEqpManipulation>
    {
        public readonly Dictionary<ImcIdentifier, ImcEntry>           Imc  = [];
        public readonly Dictionary<EqpIdentifier, EqpEntryInternal>   Eqp  = [];
        public readonly Dictionary<EqdpIdentifier, EqdpEntryInternal> Eqdp = [];
        public readonly Dictionary<EstIdentifier, EstEntry>           Est  = [];
        public readonly Dictionary<RspIdentifier, RspEntry>           Rsp  = [];
        public readonly Dictionary<GmpIdentifier, GmpEntry>           Gmp  = [];
        public readonly Dictionary<AtchIdentifier, AtchEntry>         Atch = [];
        public readonly Dictionary<ShpIdentifier, ShpEntry>           Shp  = [];

        public Wrapper()
        { }

        public Wrapper(MetaCache cache)
        {
            Imc  = cache.Imc.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);
            Eqp  = cache.Eqp.ToDictionary(kvp => kvp.Key, kvp => new EqpEntryInternal(kvp.Value.Entry, kvp.Key.Slot));
            Eqdp = cache.Eqdp.ToDictionary(kvp => kvp.Key, kvp => new EqdpEntryInternal(kvp.Value.Entry, kvp.Key.Slot));
            Est  = cache.Est.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);
            Gmp  = cache.Gmp.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);
            Rsp  = cache.Rsp.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);
            Atch = cache.Atch.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);
            Shp  = cache.Shp.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Entry);
            foreach (var geqp in cache.GlobalEqp.Keys)
                Add(geqp);
        }
    }

    private Wrapper? _data;

    public IReadOnlyDictionary<ImcIdentifier, ImcEntry> Imc
        => _data?.Imc ?? [];

    public IReadOnlyDictionary<EqpIdentifier, EqpEntryInternal> Eqp
        => _data?.Eqp ?? [];

    public IReadOnlyDictionary<EqdpIdentifier, EqdpEntryInternal> Eqdp
        => _data?.Eqdp ?? [];

    public IReadOnlyDictionary<EstIdentifier, EstEntry> Est
        => _data?.Est ?? [];

    public IReadOnlyDictionary<GmpIdentifier, GmpEntry> Gmp
        => _data?.Gmp ?? [];

    public IReadOnlyDictionary<RspIdentifier, RspEntry> Rsp
        => _data?.Rsp ?? [];

    public IReadOnlyDictionary<AtchIdentifier, AtchEntry> Atch
        => _data?.Atch ?? [];

    public IReadOnlyDictionary<ShpIdentifier, ShpEntry> Shp
        => _data?.Shp ?? [];

    public IReadOnlySet<GlobalEqpManipulation> GlobalEqp
        => _data ?? [];

    public int Count { get; private set; }

    public int GetCount(MetaManipulationType type)
        => _data is null
            ? 0
            : type switch
            {
                MetaManipulationType.Imc       => _data.Imc.Count,
                MetaManipulationType.Eqdp      => _data.Eqdp.Count,
                MetaManipulationType.Eqp       => _data.Eqp.Count,
                MetaManipulationType.Est       => _data.Est.Count,
                MetaManipulationType.Gmp       => _data.Gmp.Count,
                MetaManipulationType.Rsp       => _data.Rsp.Count,
                MetaManipulationType.Atch      => _data.Atch.Count,
                MetaManipulationType.Shp       => _data.Shp.Count,
                MetaManipulationType.GlobalEqp => _data.Count,
                _                              => 0,
            };

    public bool Contains(IMetaIdentifier identifier)
        => _data is not null
         && identifier switch
            {
                EqdpIdentifier i        => _data.Eqdp.ContainsKey(i),
                EqpIdentifier i         => _data.Eqp.ContainsKey(i),
                EstIdentifier i         => _data.Est.ContainsKey(i),
                GlobalEqpManipulation i => _data.Contains(i),
                GmpIdentifier i         => _data.Gmp.ContainsKey(i),
                ImcIdentifier i         => _data.Imc.ContainsKey(i),
                AtchIdentifier i        => _data.Atch.ContainsKey(i),
                ShpIdentifier i         => _data.Shp.ContainsKey(i),
                RspIdentifier i         => _data.Rsp.ContainsKey(i),
                _                       => false,
            };

    public void Clear()
    {
        _data = null;
        Count = 0;
    }

    public void ClearForDefault()
    {
        if (_data is null)
            return;

        if (_data.Count is 0 && Shp.Count is 0)
        {
            _data = null;
            Count = 0;
        }

        Count = GlobalEqp.Count + Shp.Count;
        _data!.Imc.Clear();
        _data!.Eqp.Clear();
        _data!.Eqdp.Clear();
        _data!.Est.Clear();
        _data!.Rsp.Clear();
        _data!.Gmp.Clear();
        _data!.Atch.Clear();
    }

    public bool Equals(MetaDictionary other)
    {
        if (Count != other.Count)
            return false;

        if (_data is null)
            return true;

        return _data.Imc.SetEquals(other._data!.Imc)
         && _data.Eqp.SetEquals(other._data!.Eqp)
         && _data.Eqdp.SetEquals(other._data!.Eqdp)
         && _data.Est.SetEquals(other._data!.Est)
         && _data.Rsp.SetEquals(other._data!.Rsp)
         && _data.Gmp.SetEquals(other._data!.Gmp)
         && _data.Atch.SetEquals(other._data!.Atch)
         && _data.Shp.SetEquals(other._data!.Shp)
         && _data.SetEquals(other._data!);
    }

    public IEnumerable<IMetaIdentifier> Identifiers
        => _data is null
            ? []
            : _data.Imc.Keys.Cast<IMetaIdentifier>()
                .Concat(_data!.Eqdp.Keys.Cast<IMetaIdentifier>())
                .Concat(_data!.Eqp.Keys.Cast<IMetaIdentifier>())
                .Concat(_data!.Est.Keys.Cast<IMetaIdentifier>())
                .Concat(_data!.Gmp.Keys.Cast<IMetaIdentifier>())
                .Concat(_data!.Rsp.Keys.Cast<IMetaIdentifier>())
                .Concat(_data!.Atch.Keys.Cast<IMetaIdentifier>())
                .Concat(_data!.Shp.Keys.Cast<IMetaIdentifier>())
                .Concat(_data!.Cast<IMetaIdentifier>());

    #region TryAdd

    public bool TryAdd(ImcIdentifier identifier, ImcEntry entry)
    {
        _data ??= [];
        if (!_data!.Imc.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(EqpIdentifier identifier, EqpEntryInternal entry)
    {
        _data ??= [];
        if (!_data!.Eqp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(EqpIdentifier identifier, EqpEntry entry)
        => TryAdd(identifier, new EqpEntryInternal(entry, identifier.Slot));

    public bool TryAdd(EqdpIdentifier identifier, EqdpEntryInternal entry)
    {
        _data ??= [];
        if (!_data!.Eqdp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(EqdpIdentifier identifier, EqdpEntry entry)
        => TryAdd(identifier, new EqdpEntryInternal(entry, identifier.Slot));

    public bool TryAdd(EstIdentifier identifier, EstEntry entry)
    {
        _data ??= [];
        if (!_data!.Est.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(GmpIdentifier identifier, GmpEntry entry)
    {
        _data ??= [];
        if (!_data!.Gmp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(RspIdentifier identifier, RspEntry entry)
    {
        _data ??= [];
        if (!_data!.Rsp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(AtchIdentifier identifier, in AtchEntry entry)
    {
        _data ??= [];
        if (!_data!.Atch.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(ShpIdentifier identifier, in ShpEntry entry)
    {
        _data ??= [];
        if (!_data!.Shp.TryAdd(identifier, entry))
            return false;

        ++Count;
        return true;
    }

    public bool TryAdd(GlobalEqpManipulation identifier)
    {
        _data ??= [];
        if (!_data.Add(identifier))
            return false;

        ++Count;
        return true;
    }

    #endregion

    #region Update

    public bool Update(ImcIdentifier identifier, ImcEntry entry)
    {
        if (_data is null || !_data.Imc.ContainsKey(identifier))
            return false;

        _data.Imc[identifier] = entry;
        return true;
    }

    public bool Update(EqpIdentifier identifier, EqpEntryInternal entry)
    {
        if (_data is null || !_data.Eqp.ContainsKey(identifier))
            return false;

        _data.Eqp[identifier] = entry;
        return true;
    }

    public bool Update(EqpIdentifier identifier, EqpEntry entry)
        => Update(identifier, new EqpEntryInternal(entry, identifier.Slot));

    public bool Update(EqdpIdentifier identifier, EqdpEntryInternal entry)
    {
        if (_data is null || !_data.Eqdp.ContainsKey(identifier))
            return false;

        _data.Eqdp[identifier] = entry;
        return true;
    }

    public bool Update(EqdpIdentifier identifier, EqdpEntry entry)
        => Update(identifier, new EqdpEntryInternal(entry, identifier.Slot));

    public bool Update(EstIdentifier identifier, EstEntry entry)
    {
        if (_data is null || !_data.Est.ContainsKey(identifier))
            return false;

        _data.Est[identifier] = entry;
        return true;
    }

    public bool Update(GmpIdentifier identifier, GmpEntry entry)
    {
        if (_data is null || !_data.Gmp.ContainsKey(identifier))
            return false;

        _data.Gmp[identifier] = entry;
        return true;
    }

    public bool Update(RspIdentifier identifier, RspEntry entry)
    {
        if (_data is null || !_data.Rsp.ContainsKey(identifier))
            return false;

        _data.Rsp[identifier] = entry;
        return true;
    }

    public bool Update(AtchIdentifier identifier, in AtchEntry entry)
    {
        if (_data is null || !_data.Atch.ContainsKey(identifier))
            return false;

        _data.Atch[identifier] = entry;
        return true;
    }

    public bool Update(ShpIdentifier identifier, in ShpEntry entry)
    {
        if (_data is null || !_data.Shp.ContainsKey(identifier))
            return false;

        _data.Shp[identifier] = entry;
        return true;
    }

    #endregion

    #region TryGetValue

    public bool TryGetValue(EstIdentifier identifier, out EstEntry value)
        => _data?.Est.TryGetValue(identifier, out value) ?? SetDefault(out value);

    public bool TryGetValue(EqpIdentifier identifier, out EqpEntryInternal value)
        => _data?.Eqp.TryGetValue(identifier, out value) ?? SetDefault(out value);

    public bool TryGetValue(EqdpIdentifier identifier, out EqdpEntryInternal value)
        => _data?.Eqdp.TryGetValue(identifier, out value) ?? SetDefault(out value);

    public bool TryGetValue(GmpIdentifier identifier, out GmpEntry value)
        => _data?.Gmp.TryGetValue(identifier, out value) ?? SetDefault(out value);

    public bool TryGetValue(RspIdentifier identifier, out RspEntry value)
        => _data?.Rsp.TryGetValue(identifier, out value) ?? SetDefault(out value);

    public bool TryGetValue(ImcIdentifier identifier, out ImcEntry value)
        => _data?.Imc.TryGetValue(identifier, out value) ?? SetDefault(out value);

    public bool TryGetValue(AtchIdentifier identifier, out AtchEntry value)
        => _data?.Atch.TryGetValue(identifier, out value) ?? SetDefault(out value);

    public bool TryGetValue(ShpIdentifier identifier, out ShpEntry value)
        => _data?.Shp.TryGetValue(identifier, out value) ?? SetDefault(out value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool SetDefault<T>(out T? value)
    {
        value = default;
        return false;
    }

    #endregion

    public bool Remove(IMetaIdentifier identifier)
    {
        if (_data is null)
            return false;

        var ret = identifier switch
        {
            EqdpIdentifier i        => _data.Eqdp.Remove(i),
            EqpIdentifier i         => _data.Eqp.Remove(i),
            EstIdentifier i         => _data.Est.Remove(i),
            GlobalEqpManipulation i => _data.Remove(i),
            GmpIdentifier i         => _data.Gmp.Remove(i),
            ImcIdentifier i         => _data.Imc.Remove(i),
            RspIdentifier i         => _data.Rsp.Remove(i),
            AtchIdentifier i        => _data.Atch.Remove(i),
            ShpIdentifier i         => _data.Shp.Remove(i),
            _                       => false,
        };
        if (ret && --Count is 0)
            _data = null;

        return ret;
    }

    #region Merging

    public void UnionWith(MetaDictionary manips)
    {
        if (manips.Count is 0)
            return;

        _data ??= [];
        foreach (var (identifier, entry) in manips._data!.Imc)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._data!.Eqp)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._data!.Eqdp)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._data!.Gmp)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._data!.Rsp)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._data!.Est)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._data!.Atch)
            TryAdd(identifier, entry);

        foreach (var (identifier, entry) in manips._data!.Shp)
            TryAdd(identifier, entry);

        foreach (var identifier in manips._data!)
            TryAdd(identifier);
    }

    /// <summary> Try to merge all manipulations from manips into this, and return the first failure, if any. </summary>
    public bool MergeForced(MetaDictionary manips, out IMetaIdentifier? failedIdentifier)
    {
        if (manips.Count is 0)
        {
            failedIdentifier = null;
            return true;
        }

        _data ??= [];
        foreach (var (identifier, _) in manips._data!.Imc.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._data!.Eqp.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._data!.Eqdp.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._data!.Gmp.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._data!.Rsp.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._data!.Est.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._data!.Atch.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var (identifier, _) in manips._data!.Shp.Where(kvp => !TryAdd(kvp.Key, kvp.Value)))
        {
            failedIdentifier = identifier;
            return false;
        }

        foreach (var identifier in manips._data!.Where(identifier => !TryAdd(identifier)))
        {
            failedIdentifier = identifier;
            return false;
        }

        failedIdentifier = null;
        return true;
    }

    public void SetTo(MetaDictionary other)
    {
        if (other.Count is 0)
        {
            _data = null;
            Count = 0;
            return;
        }

        _data ??= [];
        _data!.Imc.SetTo(other._data!.Imc);
        _data!.Eqp.SetTo(other._data!.Eqp);
        _data!.Eqdp.SetTo(other._data!.Eqdp);
        _data!.Est.SetTo(other._data!.Est);
        _data!.Rsp.SetTo(other._data!.Rsp);
        _data!.Gmp.SetTo(other._data!.Gmp);
        _data!.Atch.SetTo(other._data!.Atch);
        _data!.Shp.SetTo(other._data!.Shp);
        _data!.SetTo(other._data!);
        Count = other.Count;
    }

    public void UpdateTo(MetaDictionary other)
    {
        if (other.Count is 0)
            return;

        _data ??= [];
        _data!.Imc.UpdateTo(other._data!.Imc);
        _data!.Eqp.UpdateTo(other._data!.Eqp);
        _data!.Eqdp.UpdateTo(other._data!.Eqdp);
        _data!.Est.UpdateTo(other._data!.Est);
        _data!.Rsp.UpdateTo(other._data!.Rsp);
        _data!.Gmp.UpdateTo(other._data!.Gmp);
        _data!.Atch.UpdateTo(other._data!.Atch);
        _data!.Shp.UpdateTo(other._data!.Shp);
        _data!.UnionWith(other._data!);
        Count = _data!.Imc.Count
          + _data!.Eqp.Count
          + _data!.Eqdp.Count
          + _data!.Est.Count
          + _data!.Rsp.Count
          + _data!.Gmp.Count
          + _data!.Atch.Count
          + _data!.Shp.Count
          + _data!.Count;
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

    public static JObject Serialize(AtchIdentifier identifier, AtchEntry entry)
        => new()
        {
            ["Type"] = MetaManipulationType.Atch.ToString(),
            ["Manipulation"] = identifier.AddToJson(new JObject
            {
                ["Entry"] = entry.ToJson(),
            }),
        };

    public static JObject Serialize(ShpIdentifier identifier, ShpEntry entry)
        => new()
        {
            ["Type"] = MetaManipulationType.Shp.ToString(),
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
        if (typeof(TIdentifier) == typeof(AtchIdentifier) && typeof(TEntry) == typeof(AtchEntry))
            return Serialize(Unsafe.As<TIdentifier, AtchIdentifier>(ref identifier), Unsafe.As<TEntry, AtchEntry>(ref entry));
        if (typeof(TIdentifier) == typeof(ShpIdentifier) && typeof(TEntry) == typeof(ShpEntry))
            return Serialize(Unsafe.As<TIdentifier, ShpIdentifier>(ref identifier), Unsafe.As<TEntry, ShpEntry>(ref entry));
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
            if (value._data is not null)
            {
                SerializeTo(array, value._data!.Imc);
                SerializeTo(array, value._data!.Eqp);
                SerializeTo(array, value._data!.Eqdp);
                SerializeTo(array, value._data!.Est);
                SerializeTo(array, value._data!.Rsp);
                SerializeTo(array, value._data!.Gmp);
                SerializeTo(array, value._data!.Atch);
                SerializeTo(array, value._data!.Shp);
                SerializeTo(array, value._data!);
            }

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
                    case MetaManipulationType.Atch:
                    {
                        var identifier = AtchIdentifier.FromJson(manip);
                        var entry      = AtchEntry.FromJson(manip["Entry"] as JObject);
                        if (identifier.HasValue && entry.HasValue)
                            dict.TryAdd(identifier.Value, entry.Value);
                        else
                            Penumbra.Log.Warning("Invalid ATCH Manipulation encountered.");
                        break;
                    }
                    case MetaManipulationType.Shp:
                    {
                        var identifier = ShpIdentifier.FromJson(manip);
                        var entry      = new ShpEntry(manip["Entry"]?.Value<bool>() ?? true);
                        if (identifier.HasValue)
                            dict.TryAdd(identifier.Value, entry);
                        else
                            Penumbra.Log.Warning("Invalid SHP Manipulation encountered.");
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
        if (cache is null)
            return;

        _data = new Wrapper(cache);
        Count = cache.Count;
    }
}
