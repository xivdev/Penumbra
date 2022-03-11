using System;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

public readonly struct GmpManipulation : IEquatable< GmpManipulation >
{
    public readonly GmpEntry Entry;
    public readonly ushort   SetId;

    public GmpManipulation( GmpEntry entry, ushort setId )
    {
        Entry = entry;
        SetId = setId;
    }

    public override string ToString()
        => $"Gmp - {SetId}";

    public bool Equals( GmpManipulation other )
        => SetId == other.SetId;

    public override bool Equals( object? obj )
        => obj is GmpManipulation other && Equals( other );

    public override int GetHashCode()
        => SetId.GetHashCode();

    public int FileIndex()
        => CharacterUtility.GmpIdx;

    public bool Apply( ExpandedGmpFile file )
    {
        var entry = file[ SetId ];
        if( entry == Entry )
        {
            return false;
        }

        file[ SetId ] = Entry;
        return true;
    }
}