using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct RspManipulation : IMetaManipulation<RspManipulation>
{
    public const float MinValue = 0.01f;
    public const float MaxValue = 512f;
    public       float Entry { get; private init; }

    [JsonConverter(typeof(StringEnumConverter))]
    public SubRace SubRace { get; private init; }

    [JsonConverter(typeof(StringEnumConverter))]
    public RspAttribute Attribute { get; private init; }

    [JsonConstructor]
    public RspManipulation(SubRace subRace, RspAttribute attribute, float entry)
    {
        Entry     = entry;
        SubRace   = subRace;
        Attribute = attribute;
    }

    public RspManipulation Copy(float entry)
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
        if (Entry is < MinValue or > MaxValue)
            return false;

        return true;
    }
}
