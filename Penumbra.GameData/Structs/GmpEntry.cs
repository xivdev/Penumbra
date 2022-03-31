using System;
using System.IO;

namespace Penumbra.GameData.Structs;

public struct GmpEntry : IEquatable< GmpEntry >
{
    public static readonly GmpEntry Default = new();

    public bool Enabled
    {
        get => ( Value & 1 ) == 1;
        set
        {
            if( value )
            {
                Value |= 1ul;
            }
            else
            {
                Value &= ~1ul;
            }
        }
    }

    public bool Animated
    {
        get => ( Value & 2 ) == 2;
        set
        {
            if( value )
            {
                Value |= 2ul;
            }
            else
            {
                Value &= ~2ul;
            }
        }
    }

    public ushort RotationA
    {
        get => ( ushort )( ( Value >> 2 ) & 0x3FF );
        set => Value = ( Value & ~0xFFCul ) | ( ( value & 0x3FFul ) << 2 );
    }

    public ushort RotationB
    {
        get => ( ushort )( ( Value >> 12 ) & 0x3FF );
        set => Value = ( Value & ~0x3FF000ul ) | ( ( value & 0x3FFul ) << 12 );
    }

    public ushort RotationC
    {
        get => ( ushort )( ( Value >> 22 ) & 0x3FF );
        set => Value = ( Value & ~0xFFC00000ul ) | ( ( value & 0x3FFul ) << 22 );
    }

    public byte UnknownA
    {
        get => ( byte )( ( Value >> 32 ) & 0x0F );
        set => Value = ( Value & ~0x0F00000000ul ) | ( ( value & 0x0Ful ) << 32 );
    }

    public byte UnknownB
    {
        get => ( byte )( ( Value >> 36 ) & 0x0F );
        set => Value = ( Value & ~0xF000000000ul ) | ( ( value & 0x0Ful ) << 36 );
    }

    public byte UnknownTotal
    {
        get => ( byte )( ( Value >> 32 ) & 0xFF );
        set => Value = ( Value & ~0xFF00000000ul ) | ( ( value & 0xFFul ) << 32 );
    }

    public ulong Value { get; set; }

    public static GmpEntry FromTexToolsMeta( byte[] data )
    {
        GmpEntry  ret    = new();
        using var reader = new BinaryReader( new MemoryStream( data ) );
        ret.Value        = reader.ReadUInt32();
        ret.UnknownTotal = data[ 4 ];
        return ret;
    }

    public static implicit operator ulong( GmpEntry entry )
        => entry.Value;

    public static explicit operator GmpEntry( ulong entry )
        => new() { Value = entry };

    public bool Equals( GmpEntry other )
        => Value == other.Value;

    public override bool Equals( object? obj )
        => obj is GmpEntry other && Equals( other );

    public override int GetHashCode()
        => Value.GetHashCode();
}