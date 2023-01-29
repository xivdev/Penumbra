using System;

namespace Penumbra.GameData.Structs;

public readonly struct SetId : IComparable< SetId >, IEquatable<SetId>, IEquatable<ushort>
{
    public readonly ushort Value;

    public SetId( ushort value )
        => Value = value;

    public static implicit operator SetId( ushort id )
        => new(id);

    public static explicit operator ushort( SetId id )
        => id.Value;

    public bool Equals(SetId other)
        => Value == other.Value;

    public bool Equals(ushort other)
        => Value == other;

    public override string ToString()
        => Value.ToString();

    public int CompareTo( SetId other )
        => Value.CompareTo( other.Value );
}