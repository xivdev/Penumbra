using System.Buffers;
using System.Text.Json;
using Luna;
using Penumbra.GameData.Files.AtchStructs;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Files;

public static class MetaSerialization
{
    /// <summary> Write all meta entries in a meta dictionary as a new JSON array. </summary>
    public static Utf8JsonWriter WriteMetaDictionary(Utf8JsonWriter j, MetaDictionary meta)
    {
        j.WriteStartArray();
        if (meta.Count > 0)
        {
            SerializeTo(j, meta.Imc);
            SerializeTo(j, meta.Eqp);
            SerializeTo(j, meta.Eqdp);
            SerializeTo(j, meta.Est);
            SerializeTo(j, meta.Rsp);
            SerializeTo(j, meta.Gmp);
            SerializeTo(j, meta.Atch);
            SerializeTo(j, meta.Shp);
            SerializeTo(j, meta.Atr);
            SerializeTo(j, meta.GlobalEqp);
        }

        j.WriteEndArray();
        return j;
    }

    public static ReadOnlyMemory<byte> Serialize<TIdentifier, TEntry>(IEnumerable<KeyValuePair<TIdentifier, TEntry>> manipulations)
        where TIdentifier : unmanaged, IMetaIdentifier
        where TEntry : unmanaged
    {
        var       data   = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(data, JsonFunctions.WriterOptions);
        writer.WriteStartArray();
        SerializeTo(writer, manipulations);
        writer.WriteEndArray();
        return data.WrittenMemory;
    }

    public static ReadOnlyMemory<byte> Serialize(IEnumerable<GlobalEqpManipulation> manipulations)
    {
        var       data   = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(data, JsonFunctions.WriterOptions);
        writer.WriteStartArray();
        SerializeTo(writer, manipulations);
        writer.WriteEndArray();
        return data.WrittenMemory;
    }

    /// <summary> Add all given entries to a pre-existing JSON array. </summary>
    public static Utf8JsonWriter SerializeTo<TIdentifier, TEntry>(Utf8JsonWriter j,
        IEnumerable<KeyValuePair<TIdentifier, TEntry>> manipulations)
        where TIdentifier : unmanaged, IMetaIdentifier
        where TEntry : unmanaged
    {
        foreach (var (identifier, entry) in manipulations)
            Serialize(j, identifier, entry);
        return j;
    }

    /// <summary> Add all given entries to a pre-existing JSON array. </summary>
    public static Utf8JsonWriter SerializeTo(Utf8JsonWriter j, IEnumerable<GlobalEqpManipulation> manipulations)
    {
        foreach (var manip in manipulations)
            Serialize(j, manip);
        return j;
    }

    /// <summary> Serialize a single identifier and entry pair. </summary>
    public static Utf8JsonWriter Serialize<TIdentifier, TEntry>(Utf8JsonWriter j, TIdentifier identifier, TEntry entry)
        where TIdentifier : unmanaged, IMetaIdentifier
        where TEntry : unmanaged
    {
        if (typeof(TIdentifier) == typeof(EqpIdentifier) && typeof(TEntry) == typeof(EqpEntryInternal))
            Serialize(j, Unsafe.As<TIdentifier, EqpIdentifier>(ref identifier), Unsafe.As<TEntry, EqpEntryInternal>(ref entry));
        else if (typeof(TIdentifier) == typeof(EqpIdentifier) && typeof(TEntry) == typeof(EqpEntry))
            Serialize(j, Unsafe.As<TIdentifier, EqpIdentifier>(ref identifier), Unsafe.As<TEntry, EqpEntry>(ref entry));
        else if (typeof(TIdentifier) == typeof(EqdpIdentifier) && typeof(TEntry) == typeof(EqdpEntryInternal))
            Serialize(j, Unsafe.As<TIdentifier, EqdpIdentifier>(ref identifier), Unsafe.As<TEntry, EqdpEntryInternal>(ref entry));
        else if (typeof(TIdentifier) == typeof(EqdpIdentifier) && typeof(TEntry) == typeof(EqdpEntry))
            Serialize(j, Unsafe.As<TIdentifier, EqdpIdentifier>(ref identifier), Unsafe.As<TEntry, EqdpEntry>(ref entry));
        else if (typeof(TIdentifier) == typeof(EstIdentifier) && typeof(TEntry) == typeof(EstEntry))
            Serialize(j, Unsafe.As<TIdentifier, EstIdentifier>(ref identifier), Unsafe.As<TEntry, EstEntry>(ref entry));
        else if (typeof(TIdentifier) == typeof(GmpIdentifier) && typeof(TEntry) == typeof(GmpEntry))
            Serialize(j, Unsafe.As<TIdentifier, GmpIdentifier>(ref identifier), Unsafe.As<TEntry, GmpEntry>(ref entry));
        else if (typeof(TIdentifier) == typeof(RspIdentifier) && typeof(TEntry) == typeof(RspEntry))
            Serialize(j, Unsafe.As<TIdentifier, RspIdentifier>(ref identifier), Unsafe.As<TEntry, RspEntry>(ref entry));
        else if (typeof(TIdentifier) == typeof(ImcIdentifier) && typeof(TEntry) == typeof(ImcEntry))
            Serialize(j, Unsafe.As<TIdentifier, ImcIdentifier>(ref identifier), Unsafe.As<TEntry, ImcEntry>(ref entry));
        else if (typeof(TIdentifier) == typeof(AtchIdentifier) && typeof(TEntry) == typeof(AtchEntry))
            Serialize(j, Unsafe.As<TIdentifier, AtchIdentifier>(ref identifier), Unsafe.As<TEntry, AtchEntry>(ref entry));
        else if (typeof(TIdentifier) == typeof(ShpIdentifier) && typeof(TEntry) == typeof(ShpEntry))
            Serialize(j, Unsafe.As<TIdentifier, ShpIdentifier>(ref identifier), Unsafe.As<TEntry, ShpEntry>(ref entry));
        else if (typeof(TIdentifier) == typeof(AtrIdentifier) && typeof(TEntry) == typeof(AtrEntry))
            Serialize(j, Unsafe.As<TIdentifier, AtrIdentifier>(ref identifier), Unsafe.As<TEntry, AtrEntry>(ref entry));
        else if (typeof(TIdentifier) == typeof(GlobalEqpManipulation))
            Serialize(j, Unsafe.As<TIdentifier, GlobalEqpManipulation>(ref identifier));
        return j;
    }

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, EqpIdentifier identifier, EqpEntryInternal entry)
        => Serialize(j, identifier, entry.ToEntry(identifier.Slot));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, EqpIdentifier identifier, EqpEntry entry)
        => Serialize(j, "Eqp"u8, identifier, entry, static (w, e) => w.WriteNumberValue((ulong)e));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, EqdpIdentifier identifier, EqdpEntryInternal entry)
        => Serialize(j, identifier, entry.ToEntry(identifier.Slot));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, EqdpIdentifier identifier, EqdpEntry entry)
        => Serialize(j, "Eqdp"u8, identifier, entry, static (w, e) => w.WriteNumberValue((ushort)e));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, EstIdentifier identifier, EstEntry entry)
        => Serialize(j, "Est"u8, identifier, entry, static (w, e) => w.WriteNumberValue(e.Value));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, GmpIdentifier identifier, GmpEntry entry)
        => Serialize(j, "Gmp"u8, identifier, entry, static (w, e) => e.WriteJson(w));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, ImcIdentifier identifier, ImcEntry entry)
        => Serialize(j, "Imc"u8, identifier, entry, static (w, e) => e.WriteJson(w));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, RspIdentifier identifier, RspEntry entry)
        => Serialize(j, "Rsp"u8, identifier, entry, static (w, e) => w.WriteNumberValue(e.Value));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, AtchIdentifier identifier, AtchEntry entry)
        => Serialize(j, "Atch"u8, identifier, entry, static (w, e) => e.WriteJson(w));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, ShpIdentifier identifier, ShpEntry entry)
        => Serialize(j, "Shp"u8, identifier, entry, static (w, e) => w.WriteBooleanValue(e.Value));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, AtrIdentifier identifier, AtrEntry entry)
        => Serialize(j, "Atr"u8, identifier, entry, static (w, e) => w.WriteBooleanValue(e.Value));

    public static Utf8JsonWriter Serialize(Utf8JsonWriter j, GlobalEqpManipulation identifier)
    {
        j.WriteStartObject();
        j.WriteString("Type"u8, "GlobalEqp"u8);
        j.WriteStartObject("Manipulation"u8);
        identifier.AddToJson(j);
        j.WriteEndObject();
        j.WriteEndObject();
        return j;
    }

    private static Utf8JsonWriter Serialize<TIdentifier, TEntry>(Utf8JsonWriter j, ReadOnlySpan<byte> type, TIdentifier identifier,
        TEntry entry,
        Action<Utf8JsonWriter, TEntry> writeEntry)
        where TIdentifier : IMetaIdentifier
    {
        j.WriteStartObject();
        j.WriteString("Type"u8, type);
        j.WriteStartObject("Manipulation"u8);
        identifier.AddToJson(j);
        j.WritePropertyName("Entry"u8);
        writeEntry(j, entry);
        j.WriteEndObject();
        j.WriteEndObject();
        return j;
    }
}
