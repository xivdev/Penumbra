using System;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

public readonly struct RspManipulation : IEquatable< RspManipulation >
{
    public readonly float        Entry;
    public readonly SubRace      SubRace;
    public readonly RspAttribute Attribute;

    public RspManipulation( SubRace subRace, RspAttribute attribute, float entry )
    {
        Entry     = entry;
        SubRace   = subRace;
        Attribute = attribute;
    }

    public override string ToString()
        => $"Rsp - {SubRace.ToName()} - {Attribute.ToFullString()}";

    public bool Equals( RspManipulation other )
        => SubRace    == other.SubRace
         && Attribute == other.Attribute;

    public override bool Equals( object? obj )
        => obj is RspManipulation other && Equals( other );

    public override int GetHashCode()
        => HashCode.Combine( ( int )SubRace, ( int )Attribute );

    public int FileIndex()
        => CharacterUtility.HumanCmpIdx;

    public bool Apply( CmpFile file )
    {
        var value = file[ SubRace, Attribute ];
        if( value == Entry )
        {
            return false;
        }

        file[ SubRace, Attribute ] = Entry;
        return true;
    }
}