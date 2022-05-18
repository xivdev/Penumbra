using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public readonly struct EqdpManipulation : IMetaManipulation< EqdpManipulation >
{
    public EqdpEntry Entry { get; init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public Gender Gender { get; init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public ModelRace Race { get; init; }

    public ushort SetId { get; init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public EquipSlot Slot { get; init; }

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

    public int CompareTo( EqdpManipulation other )
    {
        var r = Race.CompareTo( other.Race );
        if( r != 0 )
        {
            return r;
        }

        var g = Gender.CompareTo( other.Gender );
        if( g != 0 )
        {
            return g;
        }

        var set = SetId.CompareTo( other.SetId );
        return set != 0 ? set : Slot.CompareTo( other.Slot );
    }

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