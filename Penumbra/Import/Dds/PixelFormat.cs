using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Penumbra.Import.Dds;

public enum ParseType
{
    Unsupported,
    DXT1,
    DXT3,
    DXT5,
    BC4,
    BC5,

    Greyscale,
    R4G4B4A4,
    B4G4R4A4,
    R5G5B5,
    B5G5R5,
    R5G6B5,
    B5G6R5,
    R5G5B5A1,
    B5G5R5A1,
    R8G8B8,
    B8G8R8,
    R8G8B8A8,
    B8G8R8A8,
}

[StructLayout( LayoutKind.Sequential )]
public struct PixelFormat
{
    public int         Size;
    public FormatFlags Flags;
    public FourCCType  FourCC;
    public int         RgbBitCount;
    public uint        RBitMask;
    public uint        GBitMask;
    public uint        BBitMask;
    public uint        ABitMask;


    [Flags]
    public enum FormatFlags : uint
    {
        AlphaPixels = 0x000001,
        Alpha       = 0x000002,
        FourCC      = 0x000004,
        RGB         = 0x000040,
        YUV         = 0x000200,
        Luminance   = 0x020000,
    }

    public enum FourCCType : uint
    {
        NoCompression = 0,
        DXT1          = 'D' | ( 'X' << 8 ) | ( 'T' << 16 ) | ( '1' << 24 ),
        DXT2          = 'D' | ( 'X' << 8 ) | ( 'T' << 16 ) | ( '2' << 24 ),
        DXT3          = 'D' | ( 'X' << 8 ) | ( 'T' << 16 ) | ( '3' << 24 ),
        DXT4          = 'D' | ( 'X' << 8 ) | ( 'T' << 16 ) | ( '4' << 24 ),
        DXT5          = 'D' | ( 'X' << 8 ) | ( 'T' << 16 ) | ( '5' << 24 ),
        DX10          = 'D' | ( 'X' << 8 ) | ( '1' << 16 ) | ( '0' << 24 ),
        ATI1          = 'A' | ( 'T' << 8 ) | ( 'I' << 16 ) | ( '1' << 24 ),
        BC4U          = 'B' | ( 'C' << 8 ) | ( '4' << 16 ) | ( 'U' << 24 ),
        BC45          = 'B' | ( 'C' << 8 ) | ( '4' << 16 ) | ( '5' << 24 ),
        ATI2          = 'A' | ( 'T' << 8 ) | ( 'I' << 16 ) | ( '2' << 24 ),
        BC5U          = 'B' | ( 'C' << 8 ) | ( '5' << 16 ) | ( 'U' << 24 ),
        BC55          = 'B' | ( 'C' << 8 ) | ( '5' << 16 ) | ( '5' << 24 ),
    }



    public void Write( BinaryWriter bw )
    {
        bw.Write( Size );
        bw.Write( ( uint )Flags );
        bw.Write( ( uint )FourCC );
        bw.Write( RgbBitCount );
        bw.Write( RBitMask );
        bw.Write( GBitMask );
        bw.Write( BBitMask );
        bw.Write( ABitMask );
    }

    public ParseType ToParseType( DXT10Header? dxt10 )
    {
        return FourCC switch
        {
            FourCCType.NoCompression => HandleUncompressed(),
            FourCCType.DXT1          => ParseType.DXT1,
            FourCCType.DXT2          => ParseType.Unsupported,
            FourCCType.DXT3          => ParseType.DXT3,
            FourCCType.DXT4          => ParseType.Unsupported,
            FourCCType.DXT5          => ParseType.DXT5,
            FourCCType.DX10          => dxt10?.ToParseType() ?? ParseType.Unsupported,
            FourCCType.ATI1          => ParseType.BC4,
            FourCCType.BC4U          => ParseType.BC4,
            FourCCType.BC45          => ParseType.BC4,
            FourCCType.ATI2          => ParseType.BC5,
            FourCCType.BC5U          => ParseType.BC5,
            FourCCType.BC55          => ParseType.BC5,
            _                        => ParseType.Unsupported,
        };
    }

    private ParseType HandleUncompressed()
    {
        switch( RgbBitCount )
        {
            case 8: return ParseType.Greyscale;
            case 16:
                if( ABitMask == 0xF000 )
                {
                    return RBitMask > GBitMask ? ParseType.B4G4R4A4 : ParseType.R4G4B4A4;
                }

                if( Flags.HasFlag( FormatFlags.AlphaPixels ) )
                {
                    return RBitMask > GBitMask ? ParseType.B5G5R5A1 : ParseType.R5G5B5A1;
                }

                if( GBitMask == 0x07E0 )
                {
                    return RBitMask > GBitMask ? ParseType.B5G6R5 : ParseType.R5G6B5;
                }

                return RBitMask > GBitMask ? ParseType.B5G5R5 : ParseType.R5G5B5;
            case 24: return RBitMask > GBitMask ? ParseType.B8G8R8 : ParseType.R8G8B8;
            case 32: return RBitMask > GBitMask ? ParseType.B8G8R8A8 : ParseType.R8G8B8A8;
            default: return ParseType.Unsupported;
        }
    }
}