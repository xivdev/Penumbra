using System;
using System.Runtime.InteropServices;

namespace Penumbra.GameData.Structs;

[StructLayout(LayoutKind.Explicit, Pack = 1)]
public struct CharacterArmor : IEquatable<CharacterArmor>
{
    public const int Size = 4;

    [FieldOffset(0)]
    public uint Value;

    [FieldOffset(0)]
    public SetId Set;

    [FieldOffset(2)]
    public byte Variant;

    [FieldOffset(3)]
    public StainId Stain;

    public CharacterArmor(SetId set, byte variant, StainId stain)
    {
        Value   = 0;
        Set     = set;
        Variant = variant;
        Stain   = stain;
    }

    public CharacterArmor With(StainId stain)
        => new(Set, Variant, stain);

    public CharacterWeapon ToWeapon()
        => new(Set, 0, Variant, Stain);

    public CharacterWeapon ToWeapon(StainId stain)
        => new(Set, 0, Variant, stain);

    public override string ToString()
        => $"{Set},{Variant},{Stain}";

    public static readonly CharacterArmor Empty;

    public bool Equals(CharacterArmor other)
        => Value == other.Value;

    public override bool Equals(object? obj)
        => obj is CharacterArmor other && Equals(other);

    public override int GetHashCode()
        => (int)Value;

    public static bool operator ==(CharacterArmor left, CharacterArmor right)
        => left.Value == right.Value;

    public static bool operator !=(CharacterArmor left, CharacterArmor right)
        => left.Value != right.Value;
}
