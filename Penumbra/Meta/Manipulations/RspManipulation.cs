using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct RspManipulation(RspIdentifier identifier, RspEntry entry) : IMetaManipulation<RspManipulation>
{
    [JsonIgnore]
    public RspIdentifier Identifier { get; } = identifier;

    public RspEntry Entry { get; } = entry;

    [JsonConverter(typeof(StringEnumConverter))]
    public SubRace SubRace
        => Identifier.SubRace;

    [JsonConverter(typeof(StringEnumConverter))]
    public RspAttribute Attribute
        => Identifier.Attribute;

    [JsonConstructor]
    public RspManipulation(SubRace subRace, RspAttribute attribute, RspEntry entry)
        : this(new RspIdentifier(subRace, attribute), entry)
    { }

    public RspManipulation Copy(RspEntry entry)
        => new(Identifier, entry);

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
        => Identifier.Validate() && Entry.Validate();
}
