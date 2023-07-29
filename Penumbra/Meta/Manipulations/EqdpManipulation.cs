using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct EqdpManipulation : IMetaManipulation<EqdpManipulation>
{
    public EqdpEntry Entry { get; private init; }

    [JsonConverter(typeof(StringEnumConverter))]
    public Gender Gender { get; private init; }

    [JsonConverter(typeof(StringEnumConverter))]
    public ModelRace Race { get; private init; }

    public SetId SetId { get; private init; }

    [JsonConverter(typeof(StringEnumConverter))]
    public EquipSlot Slot { get; private init; }

    [JsonConstructor]
    public EqdpManipulation(EqdpEntry entry, EquipSlot slot, Gender gender, ModelRace race, SetId setId)
    {
        Gender = gender;
        Race   = race;
        SetId  = setId;
        Slot   = slot;
        Entry  = Eqdp.Mask(Slot) & entry;
    }

    public EqdpManipulation Copy(EqdpManipulation entry)
    {
        if (entry.Slot != Slot)
        {
            var (bit1, bit2) = entry.Entry.ToBits(entry.Slot);
            return new EqdpManipulation(Eqdp.FromSlotAndBits(Slot, bit1, bit2), Slot, Gender, Race, SetId);
        }

        return new EqdpManipulation(entry.Entry, Slot, Gender, Race, SetId);
    }

    public EqdpManipulation Copy(EqdpEntry entry)
        => new(entry, Slot, Gender, Race, SetId);

    public override string ToString()
        => $"Eqdp - {SetId} - {Slot} - {Race.ToName()} - {Gender.ToName()}";

    public bool Equals(EqdpManipulation other)
        => Gender == other.Gender
         && Race == other.Race
         && SetId == other.SetId
         && Slot == other.Slot;

    public override bool Equals(object? obj)
        => obj is EqdpManipulation other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine((int)Gender, (int)Race, SetId, (int)Slot);

    public int CompareTo(EqdpManipulation other)
    {
        var r = Race.CompareTo(other.Race);
        if (r != 0)
            return r;

        var g = Gender.CompareTo(other.Gender);
        if (g != 0)
            return g;

        var set = SetId.Id.CompareTo(other.SetId.Id);
        return set != 0 ? set : Slot.CompareTo(other.Slot);
    }

    public MetaIndex FileIndex()
        => CharacterUtilityData.EqdpIdx(Names.CombinedRace(Gender, Race), Slot.IsAccessory());

    public bool Apply(ExpandedEqdpFile file)
    {
        var entry = file[SetId];
        var mask  = Eqdp.Mask(Slot);
        if ((entry & mask) == Entry)
            return false;

        file[SetId] = (entry & ~mask) | Entry;
        return true;
    }

    public bool Validate()
    {
        var mask = Eqdp.Mask(Slot);
        if (mask == 0)
            return false;

        if ((mask & Entry) != Entry)
            return false;

        if (FileIndex() == (MetaIndex)(-1))
            return false;

        // No check for set id.
        return true;
    }
}
