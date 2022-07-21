using System;
using System.Runtime.InteropServices;

namespace Penumbra.GameData.Structs;

[StructLayout( LayoutKind.Explicit, Pack = 1 )]
public readonly struct CharacterArmor : IEquatable< CharacterArmor >
{
    [FieldOffset( 0 )]
    public readonly uint Value;

    [FieldOffset( 0 )]
    public readonly SetId Set;

    [FieldOffset( 2 )]
    public readonly byte Variant;

    [FieldOffset( 3 )]
    public readonly StainId Stain;

    public CharacterArmor( SetId set, byte variant, StainId stain )
    {
        Value   = 0;
        Set     = set;
        Variant = variant;
        Stain   = stain;
    }

    public override string ToString()
        => $"{Set},{Variant},{Stain}";

    public static readonly CharacterArmor Empty;

    public bool Equals( CharacterArmor other )
        => Value == other.Value;

    public override bool Equals( object? obj )
        => obj is CharacterArmor other && Equals( other );

    public override int GetHashCode()
        => ( int )Value;

    public static bool operator ==( CharacterArmor left, CharacterArmor right )
        => left.Value == right.Value;

    public static bool operator !=( CharacterArmor left, CharacterArmor right )
        => left.Value != right.Value;
}