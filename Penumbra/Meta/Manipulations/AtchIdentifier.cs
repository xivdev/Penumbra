using ImSharp;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files.AtchStructs;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public readonly record struct AtchIdentifier(AtchType Type, GenderRace GenderRace, ushort EntryIndex)
    : IComparable<AtchIdentifier>, IMetaIdentifier
{
    public Gender Gender
        => GenderRace.Split().Item1;

    public ModelRace Race
        => GenderRace.Split().Item2;

    public int CompareTo(AtchIdentifier other)
    {
        var typeComparison = Type.CompareTo(other.Type);
        if (typeComparison is not 0)
            return typeComparison;

        var genderRaceComparison = GenderRace.CompareTo(other.GenderRace);
        if (genderRaceComparison is not 0)
            return genderRaceComparison;

        return EntryIndex.CompareTo(other.EntryIndex);
    }

    public override string ToString()
        => $"ATCH - {Type.ToAbbreviation()} - {GenderRace.ToName()} - {EntryIndex}";

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData> changedItems)
    {
        // Nothing specific
    }

    public MetaIndex FileIndex()
        => (MetaIndex)(-1);

    public bool Validate()
    {
        if (EntryIndex is ushort.MaxValue || GenderRace is GenderRace.Unknown || Type is AtchType.Unknown)
            return false;

        var race      = (int)GenderRace / 100;
        var remainder = (int)GenderRace - 100 * race;
        if (remainder is not 1)
            return false;

        return race is >= 0 and <= 18;
    }

    public JObject AddToJson(JObject jObj)
    {
        var (gender, race) = GenderRace.Split();
        jObj["Gender"]     = gender.String;
        jObj["Race"]       = race.String;
        jObj["Type"]       = Type.ToAbbreviation();
        jObj["Index"]      = EntryIndex;
        return jObj;
    }

    public System.Text.Json.Utf8JsonWriter AddToJson(System.Text.Json.Utf8JsonWriter j)
    {
        var (gender, race) = GenderRace.Split();
        j.WriteString("Gender"u8, gender.StringU8);
        j.WriteString("Race"u8,   race.StringU8);
        j.WriteString("Type"u8, Type.ToAbbreviation());
        j.WriteNumber("Index"u8, EntryIndex);
        return j;
    }

    public static AtchIdentifier? FromJson(JObject jObj)
    {
        var gender     = jObj["Gender"]?.ToObject<Gender>() ?? Gender.Unknown;
        var race       = jObj["Race"]?.ToObject<ModelRace>() ?? ModelRace.Unknown;
        var type       = AtchExtensions.FromString(jObj["Type"]?.ToObject<string>() ?? string.Empty);
        var entryIndex = jObj["Index"]?.ToObject<ushort>() ?? ushort.MaxValue;
        if (entryIndex == ushort.MaxValue || type is AtchType.Unknown)
            return null;

        var ret = new AtchIdentifier(type, Names.CombinedRace(gender, race), entryIndex);
        return ret.Validate() ? ret : null;
    }

    MetaManipulationType IMetaIdentifier.Type
        => MetaManipulationType.Atch;
}
