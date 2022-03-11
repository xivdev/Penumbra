using System;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

public readonly struct EqdpManipulation : IEquatable< EqdpManipulation >
{
    public readonly EqdpEntry Entry;
    public readonly Gender    Gender;
    public readonly ModelRace Race;
    public readonly ushort    SetId;
    public readonly EquipSlot Slot;

    public EqdpManipulation( EqdpEntry entry, EquipSlot slot, Gender gender, ModelRace race, ushort setId )
    {
        Entry  = Eqdp.Mask( slot ) & entry;
        Gender = gender;
        Race   = race;
        SetId  = setId;
        Slot   = slot;
    }

    public override string ToString()
        => $"Eqdp - {SetId} - {Slot} - {Race.ToName()} - {Gender.ToName()}";

    public bool Equals( EqdpManipulation other )
        => Gender == other.Gender
         && Race  == other.Race
         && SetId == other.SetId
         && Slot  == other.Slot;

    public override bool Equals( object? obj )
        => obj is EqdpManipulation other && Equals( other );

    public override int GetHashCode()
        => HashCode.Combine( ( int )Gender, ( int )Race, SetId, ( int )Slot );

    public int FileIndex()
        => CharacterUtility.EqdpIdx( Names.CombinedRace( Gender, Race ), Slot.IsAccessory() );

    public bool Apply( ExpandedEqdpFile file )
    {
        var entry = file[ SetId ];
        var mask  = Eqdp.Mask( Slot );
        if( ( entry & mask ) == Entry )
        {
            return false;
        }

        file[ SetId ] = ( entry & ~mask ) | Entry;
        return true;
    }
}