using Newtonsoft.Json;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct GmpManipulation(GmpIdentifier identifier, GmpEntry entry) : IMetaManipulation<GmpManipulation>
{
    [JsonIgnore]
    public GmpIdentifier Identifier { get; } = identifier;

    public GmpEntry Entry { get; } = entry;

    public PrimaryId SetId
        => Identifier.SetId;

    [JsonConstructor]
    public GmpManipulation(GmpEntry entry, PrimaryId setId)
        : this(new GmpIdentifier(setId), entry)
    { }

    public GmpManipulation Copy(GmpEntry entry)
        => new(Identifier, entry);

    public override string ToString()
        => $"Gmp - {SetId}";

    public bool Equals(GmpManipulation other)
        => SetId == other.SetId;

    public override bool Equals(object? obj)
        => obj is GmpManipulation other && Equals(other);

    public override int GetHashCode()
        => SetId.GetHashCode();

    public int CompareTo(GmpManipulation other)
        => SetId.Id.CompareTo(other.SetId.Id);

    public MetaIndex FileIndex()
        => MetaIndex.Gmp;

    public bool Apply(ExpandedGmpFile file)
    {
        var entry = file[SetId];
        if (entry == Entry)
            return false;

        file[SetId] = Entry;
        return true;
    }

    public bool Validate()
        // No known conditions.
        => true;
}
