using System;
using System.Runtime.InteropServices;

namespace Penumbra.GameData.Structs;

[StructLayout(LayoutKind.Explicit, Pack = 1)]
public struct CharacterArmor : IEquatable<CharacterArmor>
{
    public const int Size = 4;

    [FieldOffset(0)]
    public readonly uint Value;

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

    public readonly CharacterArmor With(StainId stain)
        => new(Set, Variant, stain);

    public readonly CharacterWeapon ToWeapon(WeaponType type)
        => new(Set, type, Variant, Stain);

    public readonly CharacterWeapon ToWeapon(WeaponType type, StainId stain)
        => new(Set, type, Variant, stain);

    public override readonly string ToString()
        => $"{Set},{Variant},{Stain}";

    public static readonly CharacterArmor Empty;

    public readonly bool Equals(CharacterArmor other)
        => Value == other.Value;

    public override readonly bool Equals(object? obj)
        => obj is CharacterArmor other && Equals(other);

    public override readonly int GetHashCode()
        => (int)Value;

    public static bool operator ==(CharacterArmor left, CharacterArmor right)
        => left.Value == right.Value;

    public static bool operator !=(CharacterArmor left, CharacterArmor right)
        => left.Value != right.Value;
}
