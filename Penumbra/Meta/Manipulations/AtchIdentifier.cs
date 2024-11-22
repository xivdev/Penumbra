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
        if (typeComparison != 0)
            return typeComparison;

        var genderRaceComparison = GenderRace.CompareTo(other.GenderRace);
        if (genderRaceComparison != 0)
            return genderRaceComparison;

        return EntryIndex.CompareTo(other.EntryIndex);
    }

    public override string ToString()
        => $"Atch - {Type.ToAbbreviation()} - {GenderRace.ToName()} - {EntryIndex}";

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData?> changedItems)
    {
        // Nothing specific
    }

    public MetaIndex FileIndex()
        => (MetaIndex)(-1);

    public bool Validate()
    {
        var race      = (int)GenderRace / 100;
        var remainder = (int)GenderRace - 100 * race;
        if (remainder != 1)
            return false;

        return race is >= 0 and <= 18;
    }

    public JObject AddToJson(JObject jObj)
    {
        var (gender, race) = GenderRace.Split();
        jObj["Gender"]     = gender.ToString();
        jObj["Race"]       = race.ToString();
        jObj["Type"]       = Type.ToAbbreviation();
        jObj["Index"]      = EntryIndex;
        return jObj;
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
