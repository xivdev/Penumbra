using System;
using System.Runtime.InteropServices;

namespace Penumbra.GameData.Structs;

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 7)]
public struct CharacterWeapon : IEquatable<CharacterWeapon>
{
    [FieldOffset(0)]
    public SetId Set;

    [FieldOffset(2)]
    public WeaponType Type;

    [FieldOffset(4)]
    public ushort Variant;

    [FieldOffset(6)]
    public StainId Stain;

    public readonly ulong Value
        => (ulong)Set | ((ulong)Type << 16) | ((ulong)Variant << 32) | ((ulong)Stain << 48);

    public override readonly string ToString()
        => $"{Set},{Type},{Variant},{Stain}";

    public CharacterWeapon(SetId set, WeaponType type, ushort variant, StainId stain)
    {
        Set     = set;
        Type    = type;
        Variant = variant;
        Stain   = stain;
    }

    public CharacterWeapon(ulong value)
    {
        Set     = (SetId)value;
        Type    = (WeaponType)(value >> 16);
        Variant = (ushort)(value >> 32);
        Stain   = (StainId)(value >> 48);
    }

    public readonly CharacterWeapon With(StainId stain)
        => new(Set, Type, Variant, stain);

    public readonly CharacterArmor ToArmor()
        => new(Set, (byte)Variant, Stain);

    public readonly CharacterArmor ToArmor(StainId stain)
        => new(Set, (byte)Variant, stain);

    public static readonly CharacterWeapon Empty = new(0, 0, 0, 0);

    public readonly bool Equals(CharacterWeapon other)
        => Value == other.Value;

    public override readonly bool Equals(object? obj)
        => obj is CharacterWeapon other && Equals(other);

    public override readonly int GetHashCode()
        => Value.GetHashCode();

    public static bool operator ==(CharacterWeapon left, CharacterWeapon right)
        => left.Value == right.Value;

    public static bool operator !=(CharacterWeapon left, CharacterWeapon right)
        => left.Value != right.Value;
}
