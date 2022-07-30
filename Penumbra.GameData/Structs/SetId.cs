using System;

namespace Penumbra.GameData.Structs;

public readonly struct SetId : IComparable< SetId >
{
    public readonly ushort Value;

    public SetId( ushort value )
        => Value = value;

    public static implicit operator SetId( ushort id )
        => new(id);

    public static explicit operator ushort( SetId id )
        => id.Value;

    public override string ToString()
        => Value.ToString();

    public int CompareTo( SetId other )
        => Value.CompareTo( other.Value );
}