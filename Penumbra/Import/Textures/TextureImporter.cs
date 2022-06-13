using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using Dalamud.Logging;
using Lumina.Data.Files;
using Lumina.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.Import.Textures;

[StructLayout( LayoutKind.Sequential )]
public struct PixelFormat
{
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
        DXT1 = 'D' | ( 'X' << 8 ) | ( 'T' << 16 ) | ( '1' << 24 ),
        DXT3 = 'D' | ( 'X' << 8 ) | ( 'T' << 16 ) | ( '3' << 24 ),
        DXT5 = 'D' | ( 'X' << 8 ) | ( 'T' << 16 ) | ( '5' << 24 ),
        DX10 = 'D' | ( 'X' << 8 ) | ( '1' << 16 ) | ( '0' << 24 ),
    }

    public int         Size;
    public FormatFlags Flags;
    public FourCCType  FourCC;
    public int         RgbBitCount;
    public uint        RBitMask;
    public uint        GBitMask;
    public uint        BBitMask;
    public uint        ABitMask;

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
}

[StructLayout( LayoutKind.Sequential )]
public struct DdsHeader
{
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

    public const int         Size = 124;
    private      int         _size;
    public       DdsFlags    Flags;
    public       int         Height;
    public       int         Width;
    public       int         PitchOrLinearSize;
    public       int         Depth;
    public       int         MipMapCount;
    public       int         Reserved1;
    public       int         Reserved2;
    public       int         Reserved3;
    public       int         Reserved4;
    public       int         Reserved5;
    public       int         Reserved6;
    public       int         Reserved7;
    public       int         Reserved8;
    public       int         Reserved9;
    public       int         ReservedA;
    public       int         ReservedB;
    public       PixelFormat PixelFormat;
    public       DdsCaps1    Caps1;
    public       DdsCaps2    Caps2;
    public       uint        Caps3;
    public       uint        Caps4;
    public       int         ReservedC;

    public void Write( BinaryWriter bw )
    {
        bw.Write( ( byte )'D' );
        bw.Write( ( byte )'D' );
        bw.Write( ( byte )'S' );
        bw.Write( ( byte )' ' );
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

[StructLayout( LayoutKind.Sequential )]
public struct DXT10Header
{
    public enum DXGIFormat : uint
    {
        Unknown                = 0,
        R32G32B32A32Typeless   = 1,
        R32G32B32A32Float      = 2,
        R32G32B32A32UInt       = 3,
        R32G32B32A32SInt       = 4,
        R32G32B32Typeless      = 5,
        R32G32B32Float         = 6,
        R32G32B32UInt          = 7,
        R32G32B32SInt          = 8,
        R16G16B16A16Typeless   = 9,
        R16G16B16A16Float      = 10,
        R16G16B16A16UNorm      = 11,
        R16G16B16A16UInt       = 12,
        R16G16B16A16SNorm      = 13,
        R16G16B16A16SInt       = 14,
        R32G32Typeless         = 15,
        R32G32Float            = 16,
        R32G32UInt             = 17,
        R32G32SInt             = 18,
        R32G8X24Typeless       = 19,
        D32FloatS8X24UInt      = 20,
        R32FloatX8X24Typeless  = 21,
        X32TypelessG8X24UInt   = 22,
        R10G10B10A2Typeless    = 23,
        R10G10B10A2UNorm       = 24,
        R10G10B10A2UInt        = 25,
        R11G11B10Float         = 26,
        R8G8B8A8Typeless       = 27,
        R8G8B8A8UNorm          = 28,
        R8G8B8A8UNormSRGB      = 29,
        R8G8B8A8UInt           = 30,
        R8G8B8A8SNorm          = 31,
        R8G8B8A8SInt           = 32,
        R16G16Typeless         = 33,
        R16G16Float            = 34,
        R16G16UNorm            = 35,
        R16G16UInt             = 36,
        R16G16SNorm            = 37,
        R16G16SInt             = 38,
        R32Typeless            = 39,
        D32Float               = 40,
        R32Float               = 41,
        R32UInt                = 42,
        R32SInt                = 43,
        R24G8Typeless          = 44,
        D24UNormS8UInt         = 45,
        R24UNormX8Typeless     = 46,
        X24TypelessG8UInt      = 47,
        R8G8Typeless           = 48,
        R8G8UNorm              = 49,
        R8G8UInt               = 50,
        R8G8SNorm              = 51,
        R8G8SInt               = 52,
        R16Typeless            = 53,
        R16Float               = 54,
        D16UNorm               = 55,
        R16UNorm               = 56,
        R16UInt                = 57,
        R16SNorm               = 58,
        R16SInt                = 59,
        R8Typeless             = 60,
        R8UNorm                = 61,
        R8UInt                 = 62,
        R8SNorm                = 63,
        R8SInt                 = 64,
        A8UNorm                = 65,
        R1UNorm                = 66,
        R9G9B9E5SharedEXP      = 67,
        R8G8B8G8UNorm          = 68,
        G8R8G8B8UNorm          = 69,
        BC1Typeless            = 70,
        BC1UNorm               = 71,
        BC1UNormSRGB           = 72,
        BC2Typeless            = 73,
        BC2UNorm               = 74,
        BC2UNormSRGB           = 75,
        BC3Typeless            = 76,
        BC3UNorm               = 77,
        BC3UNormSRGB           = 78,
        BC4Typeless            = 79,
        BC4UNorm               = 80,
        BC4SNorm               = 81,
        BC5Typeless            = 82,
        BC5UNorm               = 83,
        BC5SNorm               = 84,
        B5G6R5UNorm            = 85,
        B5G5R5A1UNorm          = 86,
        B8G8R8A8UNorm          = 87,
        B8G8R8X8UNorm          = 88,
        R10G10B10XRBiasA2UNorm = 89,
        B8G8R8A8Typeless       = 90,
        B8G8R8A8UNormSRGB      = 91,
        B8G8R8X8Typeless       = 92,
        B8G8R8X8UNormSRGB      = 93,
        BC6HTypeless           = 94,
        BC6HUF16               = 95,
        BC6HSF16               = 96,
        BC7Typeless            = 97,
        BC7UNorm               = 98,
        BC7UNormSRGB           = 99,
        AYUV                   = 100,
        Y410                   = 101,
        Y416                   = 102,
        NV12                   = 103,
        P010                   = 104,
        P016                   = 105,
        F420Opaque             = 106,
        YUY2                   = 107,
        Y210                   = 108,
        Y216                   = 109,
        NV11                   = 110,
        AI44                   = 111,
        IA44                   = 112,
        P8                     = 113,
        A8P8                   = 114,
        B4G4R4A4UNorm          = 115,
        P208                   = 130,
        V208                   = 131,
        V408                   = 132,
        SamplerFeedbackMinMipOpaque,
        SamplerFeedbackMipRegionUsedOpaque,
        ForceUInt = 0xffffffff,
    }

    public enum D3DResourceDimension : int
    {
        Unknown   = 0,
        Buffer    = 1,
        Texture1D = 2,
        Texture2D = 3,
        Texture3D = 4,
    }

    [Flags]
    public enum D3DResourceMiscFlags : uint
    {
        GenerateMips                 = 0x000001,
        Shared                       = 0x000002,
        TextureCube                  = 0x000004,
        DrawIndirectArgs             = 0x000010,
        BufferAllowRawViews          = 0x000020,
        BufferStructured             = 0x000040,
        ResourceClamp                = 0x000080,
        SharedKeyedMutex             = 0x000100,
        GDICompatible                = 0x000200,
        SharedNTHandle               = 0x000800,
        RestrictedContent            = 0x001000,
        RestrictSharedResource       = 0x002000,
        RestrictSharedResourceDriver = 0x004000,
        Guarded                      = 0x008000,
        TilePool                     = 0x020000,
        Tiled                        = 0x040000,
        HWProtected                  = 0x080000,
        SharedDisplayable,
        SharedExclusiveWriter,
    };

    public enum D3DAlphaMode : int
    {
        Unknown       = 0,
        Straight      = 1,
        Premultiplied = 2,
        Opaque        = 3,
        Custom        = 4,
    };

    public DXGIFormat           Format;
    public D3DResourceDimension ResourceDimension;
    public D3DResourceMiscFlags MiscFlags;
    public uint                 ArraySize;
    public D3DAlphaMode         AlphaMode;
}

public class DdsFile
{
    public const int DdsIdentifier = 0x20534444;

    public DdsHeader    Header;
    public DXT10Header? DXT10Header;
    public byte[]       MainSurfaceData;
    public byte[]       RemainingSurfaceData;

    private DdsFile( DdsHeader header, byte[] mainSurfaceData, byte[] remainingSurfaceData, DXT10Header? dXT10Header = null )
    {
        Header               = header;
        DXT10Header          = dXT10Header;
        MainSurfaceData      = mainSurfaceData;
        RemainingSurfaceData = remainingSurfaceData;
    }

    public static bool Load( Stream data, [NotNullWhen( true )] out DdsFile? file )
    {
        file = null;
        try
        {
            using var br = new BinaryReader( data );
            if( br.ReadUInt32() != DdsIdentifier )
            {
                return false;
            }

            var header = br.ReadStructure< DdsHeader >();
            var dxt10  = header.PixelFormat.FourCC == PixelFormat.FourCCType.DX10 ? ( DXT10Header? )br.ReadStructure< DXT10Header >() : null;

            file = new DdsFile( header, br.ReadBytes( ( int )( br.BaseStream.Length - br.BaseStream.Position ) ), Array.Empty< byte >(),
                dxt10 );
            return true;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not load DDS file:\n{e}" );
            return false;
        }
    }

    public bool ConvertToTex( out byte[] texBytes )
    {
        using var mem = new MemoryStream( MainSurfaceData.Length * 2 );
        using( var bw = new BinaryWriter( mem ) )
        {
            var format = WriteTexHeader( bw );
            bw.Write( ConvertBytes( MainSurfaceData, format ) );
        }

        texBytes = mem.ToArray();
        return true;
    }

    private TexFile.TextureFormat WriteTexHeader( BinaryWriter bw )
    {
        var (format, mipLength) = ConvertFormat( Header.PixelFormat, Header.Height, Header.Width, DXT10Header );

        bw.Write( ( uint )TexFile.Attribute.TextureType2D );
        bw.Write( ( uint )format );
        bw.Write( ( ushort )Header.Width );
        bw.Write( ( ushort )Header.Height );
        bw.Write( ( ushort )Header.Depth );
        bw.Write( ( ushort )Header.MipMapCount );
        bw.Write( 0 );
        bw.Write( 1 );
        bw.Write( 2 );

        var offset = 80;
        for( var i = 0; i < Header.MipMapCount; ++i )
        {
            bw.Write( offset );
            offset    += mipLength;
            mipLength =  Math.Max( 16, mipLength >> 2 );
        }

        for( var i = Header.MipMapCount; i < 13; ++i )
        {
            bw.Write( 0 );
        }

        return format;
    }

    private static byte[] ConvertBytes( byte[] ddsData, TexFile.TextureFormat format )
    {
        return format switch
        {
            _ => ddsData,
        };
    }

    private static (TexFile.TextureFormat, int) ConvertFormat( PixelFormat format, int height, int width, DXT10Header? dxt10 )
        => format.FourCC switch
        {
            PixelFormat.FourCCType.DXT1 => ( TexFile.TextureFormat.DXT1, height * width / 2 ),
            PixelFormat.FourCCType.DXT3 => ( TexFile.TextureFormat.DXT3, height         * width * 4 ),
            PixelFormat.FourCCType.DXT5 => ( TexFile.TextureFormat.DXT5, height         * width ),
            PixelFormat.FourCCType.DX10 => dxt10!.Value.Format switch
            {
                Textures.DXT10Header.DXGIFormat.A8UNorm           => ( TexFile.TextureFormat.A8, height            * width ),
                Textures.DXT10Header.DXGIFormat.R8G8B8A8UInt      => ( TexFile.TextureFormat.A8R8G8B8, height      * width * 4 ),
                Textures.DXT10Header.DXGIFormat.R8G8UNorm         => ( TexFile.TextureFormat.L8, height            * width ),
                Textures.DXT10Header.DXGIFormat.B8G8R8X8UNorm     => ( TexFile.TextureFormat.R8G8B8X8, height      * width * 4 ),
                Textures.DXT10Header.DXGIFormat.B4G4R4A4UNorm     => ( TexFile.TextureFormat.R4G4B4A4, height      * width * 2 ),
                Textures.DXT10Header.DXGIFormat.B5G5R5A1UNorm     => ( TexFile.TextureFormat.R5G5B5A1, height      * width * 2 ),
                Textures.DXT10Header.DXGIFormat.R32Float          => ( TexFile.TextureFormat.R32F, height          * width * 4 ),
                Textures.DXT10Header.DXGIFormat.R32G32B32A32Float => ( TexFile.TextureFormat.R32G32B32A32F, height * width * 16 ),
                Textures.DXT10Header.DXGIFormat.R16G16Float       => ( TexFile.TextureFormat.R16G16F, height       * width * 4 ),
                Textures.DXT10Header.DXGIFormat.R16G16B16A16Float => ( TexFile.TextureFormat.R16G16B16A16F, height * width * 8 ),
                Textures.DXT10Header.DXGIFormat.D16UNorm          => ( TexFile.TextureFormat.D16, height           * width * 2 ),
                Textures.DXT10Header.DXGIFormat.D24UNormS8UInt    => ( TexFile.TextureFormat.D24S8, height         * width * 4 ),
                _                                                 => ( TexFile.TextureFormat.A8R8G8B8, height      * width * 4 ),
            },
            _ => ( TexFile.TextureFormat.A8R8G8B8, height * width * 4 ),
        };
}

public class TextureImporter
{
    private static void WriteHeader( byte[] target, int width, int height )
    {
        using var mem = new MemoryStream( target );
        using var bw  = new BinaryWriter( mem );
        bw.Write( ( uint )TexFile.Attribute.TextureType2D );
        bw.Write( ( uint )TexFile.TextureFormat.A8R8G8B8 );
        bw.Write( ( ushort )width );
        bw.Write( ( ushort )height );
        bw.Write( ( ushort )1 );
        bw.Write( ( ushort )1 );
        bw.Write( 0 );
        bw.Write( 1 );
        bw.Write( 2 );
        bw.Write( 80 );
        for( var i = 1; i < 13; ++i )
        {
            bw.Write( 0 );
        }
    }

    public static unsafe bool RgbaBytesToDds( byte[] rgba, int width, int height, out byte[] ddsData )
    {
        var header = new DdsHeader()
        {
            Caps1  = DdsHeader.DdsCaps1.Complex | DdsHeader.DdsCaps1.Texture | DdsHeader.DdsCaps1.MipMap,
            Depth  = 1,
            Flags  = DdsHeader.DdsFlags.Required | DdsHeader.DdsFlags.Pitch | DdsHeader.DdsFlags.MipMapCount,
            Height = height,
            Width  = width,
            PixelFormat = new PixelFormat()
            {
                Flags       = PixelFormat.FormatFlags.AlphaPixels | PixelFormat.FormatFlags.RGB,
                FourCC      = 0,
                BBitMask    = 0x000000FF,
                GBitMask    = 0x0000FF00,
                RBitMask    = 0x00FF0000,
                ABitMask    = 0xFF000000,
                Size        = 32,
                RgbBitCount = 32,
            },
        };
        ddsData = new byte[DdsHeader.Size + rgba.Length];
        header.Write( ddsData, 0 );
        rgba.CopyTo( ddsData, DdsHeader.Size );
        for( var i = 0; i < rgba.Length; i += 4 )
        {
            ( ddsData[ DdsHeader.Size       + i ], ddsData[ DdsHeader.Size + i                            + 2 ] )
                = ( ddsData[ DdsHeader.Size + i                            + 2 ], ddsData[ DdsHeader.Size + i ] );
        }

        return true;
    }

    public static bool RgbaBytesToTex( byte[] rgba, int width, int height, out byte[] texData )
    {
        texData = Array.Empty< byte >();
        if( rgba.Length != width * height * 4 )
        {
            return false;
        }

        texData = new byte[80 + width * height * 4];
        WriteHeader( texData, width, height );
        // RGBA to BGRA.
        for( var i = 0; i < rgba.Length; i += 4 )
        {
            texData[ 80 + i + 0 ] = rgba[ i + 2 ];
            texData[ 80 + i + 1 ] = rgba[ i + 1 ];
            texData[ 80 + i + 2 ] = rgba[ i + 0 ];
            texData[ 80 + i + 3 ] = rgba[ i + 3 ];
        }

        return true;
    }

    public static bool PngToTex( string inputFile, out byte[] texData )
    {
        using var file  = File.OpenRead( inputFile );
        var       image = Image.Load< Bgra32 >( file );

        var buffer = new byte[80 + image.Height * image.Width * 4];
        WriteHeader( buffer, image.Width, image.Height );

        var span = new Span< byte >( buffer, 80, buffer.Length - 80 );
        image.CopyPixelDataTo( span );

        texData = buffer;
        return true;
    }

    public void Import( string inputFile )
    { }
}