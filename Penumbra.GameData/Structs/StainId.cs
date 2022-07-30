using System;

namespace Penumbra.GameData.Structs;

public readonly struct StainId : IEquatable< StainId >
{
    public readonly byte Value;

    public StainId( byte value )
        => Value = value;

    public static implicit operator StainId( byte id )
        => new(id);

    public static explicit operator byte( StainId id )
        => id.Value;

    public override string ToString()
        => Value.ToString();

    public bool Equals( StainId other )
        => Value == other.Value;

    public override bool Equals( object? obj )
        => obj is StainId other && Equals( other );

    public override int GetHashCode()
        => Value.GetHashCode();
}