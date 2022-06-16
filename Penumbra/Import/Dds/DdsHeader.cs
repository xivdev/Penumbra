using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Penumbra.Import.Dds;

[StructLayout( LayoutKind.Sequential )]
public struct DdsHeader
{
    public const int  Size        = 124;
    public const uint MagicNumber = 'D' | ( 'D' << 8 ) | ( 'S' << 16 ) | ( ' ' << 24 );

    private int         _size;
    public  DdsFlags    Flags;
    public  int         Height;
    public  int         Width;
    public  int         PitchOrLinearSize;
    public  int         Depth;
    public  int         MipMapCount;
    public  int         Reserved1;
    public  int         Reserved2;
    public  int         Reserved3;
    public  int         Reserved4;
    public  int         Reserved5;
    public  int         Reserved6;
    public  int         Reserved7;
    public  int         Reserved8;
    public  int         Reserved9;
    public  int         ReservedA;
    public  int         ReservedB;
    public  PixelFormat PixelFormat;
    public  DdsCaps1    Caps1;
    public  DdsCaps2    Caps2;
    public  uint        Caps3;
    public  uint        Caps4;
    public  int         ReservedC;

    [Flags]
    public enum DdsFlags : uint
    {
        Caps        = 0x00000001,
        Height      = 0x00000002,
        Width       = 0x00000004,
        Pitch       = 0x00000008,
        PixelFormat = 0x00001000,
        MipMapCount = 0x00020000,
        LinearSize  = 0x00080000,
        Depth       = 0x00800000,

        Required = Caps | Height | Width | PixelFormat,
    }

    [Flags]
    public enum DdsCaps1 : uint
    {
        Complex = 0x08,
        MipMap  = 0x400000,
        Texture = 0x1000,
    }

    [Flags]
    public enum DdsCaps2 : uint
    {
        CubeMap           = 0x200,
        CubeMapPositiveEX = 0x400,
        CubeMapNegativeEX = 0x800,
        CubeMapPositiveEY = 0x1000,
        CubeMapNegativeEY = 0x2000,
        CubeMapPositiveEZ = 0x4000,
        CubeMapNegativeEZ = 0x8000,
        Volume            = 0x200000,
    }

    public void Write( BinaryWriter bw )
    {
        bw.Write( MagicNumber );
        bw.Write( Size );
        bw.Write( ( uint )Flags );
        bw.Write( Height );
        bw.Write( Width );
        bw.Write( PitchOrLinearSize );
        bw.Write( Depth );
        bw.Write( MipMapCount );
        bw.Write( Reserved1 );
        bw.Write( Reserved2 );
        bw.Write( Reserved3 );
        bw.Write( Reserved4 );
        bw.Write( Reserved5 );
        bw.Write( Reserved6 );
        bw.Write( Reserved7 );
        bw.Write( Reserved8 );
        bw.Write( Reserved9 );
        bw.Write( ReservedA );
        bw.Write( ReservedB );
        PixelFormat.Write( bw );
        bw.Write( ( uint )Caps1 );
        bw.Write( ( uint )Caps2 );
        bw.Write( Caps3 );
        bw.Write( Caps4 );
        bw.Write( ReservedC );
    }

    public void Write( byte[] bytes, int offset )
    {
        using var m  = new MemoryStream( bytes, offset, bytes.Length - offset );
        using var bw = new BinaryWriter( m );
        Write( bw );
    }
}