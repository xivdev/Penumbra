using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.String.Classes;

namespace Penumbra.Meta.Manipulations;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct ImcManipulation : IMetaManipulation<ImcManipulation>
{
    [JsonIgnore]
    public ImcIdentifier Identifier { get; private init; }

    public ImcEntry Entry { get; private init; }


    public PrimaryId PrimaryId
        => Identifier.PrimaryId;

    public SecondaryId SecondaryId
        => Identifier.SecondaryId;

    public Variant Variant
        => Identifier.Variant;

    [JsonConverter(typeof(StringEnumConverter))]
    public ObjectType ObjectType
        => Identifier.ObjectType;

    [JsonConverter(typeof(StringEnumConverter))]
    public EquipSlot EquipSlot
        => Identifier.EquipSlot;

    [JsonConverter(typeof(StringEnumConverter))]
    public BodySlot BodySlot
        => Identifier.BodySlot;

    public ImcManipulation(EquipSlot equipSlot, ushort variant, PrimaryId primaryId, ImcEntry entry)
        : this(new ImcIdentifier(equipSlot, primaryId, variant), entry)
    { }

    public ImcManipulation(ImcIdentifier identifier, ImcEntry entry)
    {
        Identifier = identifier;
        Entry      = entry;
    }


    // Variants were initially ushorts but got shortened to bytes.
    // There are still some manipulations around that have values > 255 for variant,
    // so we change the unused value to something nonsensical in that case, just so they do not compare equal,
    // and clamp the variant to 255.
    [JsonConstructor]
    internal ImcManipulation(ObjectType objectType, BodySlot bodySlot, PrimaryId primaryId, SecondaryId secondaryId, ushort variant,
        EquipSlot equipSlot, ImcEntry entry)
    {
        Entry = entry;
        var v = (Variant)Math.Clamp(variant, (ushort)0, byte.MaxValue);
        Identifier = objectType switch
        {
            ObjectType.Accessory or ObjectType.Equipment => new ImcIdentifier(primaryId, v, objectType, 0, equipSlot,
                variant > byte.MaxValue ? BodySlot.Body : BodySlot.Unknown),
            ObjectType.DemiHuman => new ImcIdentifier(primaryId, v, objectType, secondaryId, equipSlot, variant > byte.MaxValue ? BodySlot.Body : BodySlot.Unknown),
            _ => new ImcIdentifier(primaryId, v, objectType, secondaryId, equipSlot, bodySlot == BodySlot.Unknown ? BodySlot.Body : BodySlot.Unknown),
        };
    }

    public ImcManipulation Copy(ImcEntry entry)
        => new(Identifier, entry);

    public override string ToString()
        => Identifier.ToString();

    public bool Equals(ImcManipulation other)
        => Identifier == other.Identifier;

    public override bool Equals(object? obj)
        => obj is ImcManipulation other && Equals(other);

    public override int GetHashCode()
        => Identifier.GetHashCode();

    public int CompareTo(ImcManipulation other)
        => Identifier.CompareTo(other.Identifier);

    public MetaIndex FileIndex()
        => Identifier.FileIndex();

    public Utf8GamePath GamePath()
        => Identifier.GamePath();

    public bool Apply(ImcFile file)
        => file.SetEntry(ImcFile.PartIndex(EquipSlot), Variant.Id, Entry);

    public bool Validate(bool withMaterial)
    {
        if (!Identifier.Validate())
            return false;

        if (withMaterial && Entry.MaterialId == 0)
            return false;

        return true;
    }
}
