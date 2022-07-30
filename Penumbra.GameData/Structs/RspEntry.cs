using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Penumbra.GameData.Enums;

namespace Penumbra.GameData.Structs;

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public readonly struct RspEntry
{
    public const int ByteSize = ( int )RspAttribute.NumAttributes * 4;

    private readonly float[] Attributes;

    public RspEntry( RspEntry copy )
        => Attributes = ( float[] )copy.Attributes.Clone();

    public RspEntry( byte[] bytes, int offset )
    {
        if( offset < 0 || offset + ByteSize > bytes.Length )
        {
            throw new ArgumentOutOfRangeException();
        }

        Attributes = new float[( int )RspAttribute.NumAttributes];
        using MemoryStream s  = new(bytes) { Position = offset };
        using BinaryReader br = new(s);
        for( var i = 0; i < ( int )RspAttribute.NumAttributes; ++i )
        {
            Attributes[ i ] = br.ReadSingle();
        }
    }

    private static int ToIndex( RspAttribute attribute )
        => attribute < RspAttribute.NumAttributes && attribute >= 0
            ? ( int )attribute
            : throw new InvalidEnumArgumentException();

    public float this[ RspAttribute attribute ]
    {
        get => Attributes[ ToIndex( attribute ) ];
        set => Attributes[ ToIndex( attribute ) ] = value;
    }

    public byte[] ToBytes()
    {
        using var s  = new MemoryStream( ByteSize );
        using var bw = new BinaryWriter( s );
        foreach( var attribute in Attributes )
        {
            bw.Write( attribute );
        }

        return s.ToArray();
    }
}