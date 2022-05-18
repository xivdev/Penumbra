using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Util;

namespace Penumbra.Meta.Manipulations;

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public readonly struct EqpManipulation : IMetaManipulation< EqpManipulation >
{
    [JsonConverter( typeof( ForceNumericFlagEnumConverter ) )]
    public EqpEntry Entry { get; init; }

    public ushort SetId { get; init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public EquipSlot Slot { get; init; }

    public EqpManipulation( EqpEntry entry, EquipSlot slot, ushort setId )
    {
        Slot  = slot;
        SetId = setId;
        Entry = Eqp.Mask( slot ) & entry;
    }

    public override string ToString()
        => $"Eqp - {SetId} - {Slot}";

    public bool Equals( EqpManipulation other )
        => Slot   == other.Slot
         && SetId == other.SetId;

    public override bool Equals( object? obj )
        => obj is EqpManipulation other && Equals( other );

    public override int GetHashCode()
        => HashCode.Combine( ( int )Slot, SetId );

    public int CompareTo( EqpManipulation other )
    {
        var set = SetId.CompareTo( other.SetId );
        return set != 0 ? set : Slot.CompareTo( other.Slot );
    }

    public int FileIndex()
        => CharacterUtility.EqpIdx;

    public bool Apply( ExpandedEqpFile file )
    {
        var entry = file[ SetId ];
        var mask  = Eqp.Mask( Slot );
        if( ( entry & mask ) == Entry )
        {
            return false;
        }

        file[ SetId ] = ( entry & ~mask ) | Entry;
        return true;
    }
}