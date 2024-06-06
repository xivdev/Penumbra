using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct RspManipulation : IMetaManipulation<RspManipulation>
{
    public RspIdentifier Identifier { get; private init; }
    public RspEntry      Entry      { get; private init; }

    [JsonConverter(typeof(StringEnumConverter))]
    public SubRace SubRace
        => Identifier.SubRace;

    [JsonConverter(typeof(StringEnumConverter))]
    public RspAttribute Attribute
        => Identifier.Attribute;

    [JsonConstructor]
    public RspManipulation(SubRace subRace, RspAttribute attribute, RspEntry entry)
    {
        Entry      = entry;
        Identifier = new RspIdentifier(subRace, attribute);
    }

    public RspManipulation Copy(RspEntry entry)
        => new(SubRace, Attribute, entry);

    public override string ToString()
        => $"Rsp - {SubRace.ToName()} - {Attribute.ToFullString()}";

    public bool Equals(RspManipulation other)
        => SubRace == other.SubRace
         && Attribute == other.Attribute;

    public override bool Equals(object? obj)
        => obj is RspManipulation other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine((int)SubRace, (int)Attribute);

    public int CompareTo(RspManipulation other)
    {
        var s = SubRace.CompareTo(other.SubRace);
        return s != 0 ? s : Attribute.CompareTo(other.Attribute);
    }

    public MetaIndex FileIndex()
        => MetaIndex.HumanCmp;

    public bool Apply(CmpFile file)
    {
        var value = file[SubRace, Attribute];
        if (value == Entry)
            return false;

        file[SubRace, Attribute] = Entry;
        return true;
    }

    public bool Validate()
    {
        if (SubRace is SubRace.Unknown || !Enum.IsDefined(SubRace))
            return false;
        if (!Enum.IsDefined(Attribute))
            return false;
        if (Entry.Value is < RspEntry.MinValue or > RspEntry.MaxValue)
            return false;

        return true;
    }
}
