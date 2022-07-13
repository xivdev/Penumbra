using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Dalamud.Logging;
using Lumina.Data.Files;
using Lumina.Extensions;

namespace Penumbra.Import.Dds;

public class DdsFile
{
    public const int DdsIdentifier = 0x20534444;

    public readonly  DdsHeader    Header;
    public readonly  DXT10Header? DXT10Header;
    private readonly byte[]       _data;

    public ReadOnlySpan< byte > Data
        => _data;

    public ReadOnlySpan< byte > MipMap( int level )
    {
        var mipSize = ParseType switch
        {
            ParseType.Unsupported => 0,
            ParseType.DXT1        => Header.Height * Header.Width / 2,
            ParseType.BC4         => Header.Height * Header.Width / 2,

            ParseType.DXT3      => Header.Height * Header.Width,
            ParseType.DXT5      => Header.Height * Header.Width,
            ParseType.BC5       => Header.Height * Header.Width,
            ParseType.Greyscale => Header.Height * Header.Width,

            ParseType.R4G4B4A4 => Header.Height * Header.Width * 2,
            ParseType.B4G4R4A4 => Header.Height * Header.Width * 2,
            ParseType.R5G5B5   => Header.Height * Header.Width * 2,
            ParseType.B5G5R5   => Header.Height * Header.Width * 2,
            ParseType.R5G6B5   => Header.Height * Header.Width * 2,
            ParseType.B5G6R5   => Header.Height * Header.Width * 2,
            ParseType.R5G5B5A1 => Header.Height * Header.Width * 2,
            ParseType.B5G5R5A1 => Header.Height * Header.Width * 2,

            ParseType.R8G8B8 => Header.Height * Header.Width * 3,
            ParseType.B8G8R8 => Header.Height * Header.Width * 3,

            ParseType.R8G8B8A8 => Header.Height * Header.Width * 4,
            ParseType.B8G8R8A8 => Header.Height * Header.Width * 4,
            _                  => throw new ArgumentOutOfRangeException( nameof( ParseType ), ParseType, null ),
        };

        if( Header.MipMapCount < level )
        {
            throw new ArgumentOutOfRangeException( nameof( level ) );
        }

        var sum = 0;
        for( var i = 0; i < level; ++i )
        {
            sum     += mipSize;
            mipSize =  Math.Max( 16, mipSize >> 2 );
        }


        if( _data.Length < sum + mipSize )
        {
            throw new Exception( "Not enough data to encode image." );
        }

        return _data.AsSpan( sum, mipSize );
    }

    private         byte[]?   _rgbaData;
    public readonly ParseType ParseType;

    public ReadOnlySpan< byte > RgbaData
        => _rgbaData ??= ParseToRgba();

    private DdsFile( ParseType type, DdsHeader header, byte[] data, DXT10Header? dxt10Header = null )
    {
        ParseType   = type;
        Header      = header;
        DXT10Header = dxt10Header;
        _data       = data;
    }

    private byte[] ParseToRgba()
    {
        return ParseType switch
        {
            ParseType.Unsupported => Array.Empty< byte >(),
            ParseType.DXT1        => ImageParsing.DecodeDxt1( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.DXT3        => ImageParsing.DecodeDxt3( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.DXT5        => ImageParsing.DecodeDxt5( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.BC4         => ImageParsing.DecodeBc4( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.BC5         => ImageParsing.DecodeBc5( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.Greyscale   => ImageParsing.DecodeUncompressedGreyscale( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.R4G4B4A4    => ImageParsing.DecodeUncompressedR4G4B4A4( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.B4G4R4A4    => ImageParsing.DecodeUncompressedB4G4R4A4( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.R5G5B5      => ImageParsing.DecodeUncompressedR5G5B5( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.B5G5R5      => ImageParsing.DecodeUncompressedB5G5R5( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.R5G6B5      => ImageParsing.DecodeUncompressedR5G6B5( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.B5G6R5      => ImageParsing.DecodeUncompressedB5G6R5( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.R5G5B5A1    => ImageParsing.DecodeUncompressedR5G5B5A1( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.B5G5R5A1    => ImageParsing.DecodeUncompressedB5G5R5A1( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.R8G8B8      => ImageParsing.DecodeUncompressedR8G8B8( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.B8G8R8      => ImageParsing.DecodeUncompressedB8G8R8( MipMap( 0 ), Header.Height, Header.Width ),
            ParseType.R8G8B8A8    => _data.Length == Header.Width * Header.Height * 4 ? _data : _data[ ..( Header.Width * Header.Height * 4 ) ],
            ParseType.B8G8R8A8    => ImageParsing.DecodeUncompressedB8G8R8A8( MipMap( 0 ), Header.Height, Header.Width ),
            _                     => throw new ArgumentOutOfRangeException(),
        };
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
            var type   = header.PixelFormat.ToParseType( dxt10 );

            file = new DdsFile( type, header, br.ReadBytes( ( int )( br.BaseStream.Length - br.BaseStream.Position ) ), dxt10 );
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
        using var mem = new MemoryStream( _data.Length * 2 );
        using( var bw = new BinaryWriter( mem ) )
        {
            var (format, mipLength) = WriteTexHeader( bw );
            var bytes = format == TexFile.TextureFormat.B8G8R8X8 ? RgbaData : _data;

            if( bytes.Length < mipLength )
            {
                throw new Exception( "Broken file. Not enough data." );
            }

            bw.Write( _data.AsSpan( 0, mipLength ) );
        }

        texBytes = mem.ToArray();
        return true;
    }

    private (TexFile.TextureFormat, int) WriteTexHeader( BinaryWriter bw )
    {
        var (format, mipLength) = ConvertFormat( ParseType, Header.Height, Header.Width );
        if( mipLength == 0 )
        {
            throw new Exception( "Invalid format to convert to tex." );
        }

        var mipCount = Header.MipMapCount;
        if( format == TexFile.TextureFormat.B8G8R8X8 && ParseType != ParseType.R8G8B8A8 )
        {
            mipCount = 1;
        }

        bw.Write( ( uint )TexFile.Attribute.TextureType2D );
        bw.Write( ( uint )format );
        bw.Write( ( ushort )Header.Width );
        bw.Write( ( ushort )Header.Height );
        bw.Write( ( ushort )Header.Depth );
        bw.Write( ( ushort )mipCount );
        bw.Write( 0 );
        bw.Write( 1 );
        bw.Write( 2 );

        var offset       = 80;
        var mipLengthSum = 0;
        for( var i = 0; i < mipCount; ++i )
        {
            bw.Write( offset );
            offset       += mipLength;
            mipLengthSum += mipLength;
            mipLength    =  Math.Max( 16, mipLength >> 2 );
        }

        for( var i = mipCount; i < 13; ++i )
        {
            bw.Write( 0 );
        }

        return ( format, mipLengthSum );
    }

    public static (TexFile.TextureFormat, int) ConvertFormat( ParseType type, int height, int width )
    {
        return type switch
        {
            ParseType.Unsupported => ( TexFile.TextureFormat.Unknown, 0 ),
            ParseType.DXT1        => ( TexFile.TextureFormat.DXT1, height * width / 2 ),
            ParseType.DXT3        => ( TexFile.TextureFormat.DXT3, height         * width * 2 ),
            ParseType.DXT5        => ( TexFile.TextureFormat.DXT5, height         * width * 2 ),
            ParseType.BC4         => ( TexFile.TextureFormat.B8G8R8X8, height     * width * 4 ),
            ParseType.BC5         => ( TexFile.TextureFormat.B8G8R8X8, height     * width * 4 ),
            ParseType.Greyscale   => ( TexFile.TextureFormat.A8, height           * width ),
            ParseType.R4G4B4A4    => ( TexFile.TextureFormat.B4G4R4A4, height     * width * 2 ),
            ParseType.B4G4R4A4    => ( TexFile.TextureFormat.B4G4R4A4, height     * width * 2 ),
            ParseType.R5G5B5      => ( TexFile.TextureFormat.B8G8R8X8, height     * width * 4 ),
            ParseType.B5G5R5      => ( TexFile.TextureFormat.B8G8R8X8, height     * width * 4 ),
            ParseType.R5G6B5      => ( TexFile.TextureFormat.B8G8R8X8, height     * width * 4 ),
            ParseType.B5G6R5      => ( TexFile.TextureFormat.B8G8R8X8, height     * width * 4 ),
            ParseType.R5G5B5A1    => ( TexFile.TextureFormat.B5G5R5A1, height     * width * 2 ),
            ParseType.B5G5R5A1    => ( TexFile.TextureFormat.B5G5R5A1, height     * width * 2 ),
            ParseType.R8G8B8      => ( TexFile.TextureFormat.B8G8R8X8, height     * width * 4 ),
            ParseType.B8G8R8      => ( TexFile.TextureFormat.B8G8R8X8, height     * width * 4 ),
            ParseType.R8G8B8A8    => ( TexFile.TextureFormat.B8G8R8X8, height     * width * 4 ),
            ParseType.B8G8R8A8    => ( TexFile.TextureFormat.B8G8R8X8, height     * width * 4 ),
            _                     => throw new ArgumentOutOfRangeException( nameof( type ), type, null ),
        };
    }
}

public class TmpTexFile
{
    public TexFile.TexHeader Header;
    public byte[]            RgbaData = Array.Empty< byte >();

    public void Load( BinaryReader br )
    {
        Header = br.ReadStructure< TexFile.TexHeader >();
        var data = br.ReadBytes( ( int )( br.BaseStream.Length - br.BaseStream.Position ) );
        RgbaData = Header.Format switch
        {
            TexFile.TextureFormat.L8       => ImageParsing.DecodeUncompressedGreyscale( data, Header.Height, Header.Width ),
            TexFile.TextureFormat.A8       => ImageParsing.DecodeUncompressedGreyscale( data, Header.Height, Header.Width ),
            TexFile.TextureFormat.DXT1     => ImageParsing.DecodeDxt1( data, Header.Height, Header.Width ),
            TexFile.TextureFormat.DXT3     => ImageParsing.DecodeDxt3( data, Header.Height, Header.Width ),
            TexFile.TextureFormat.DXT5     => ImageParsing.DecodeDxt5( data, Header.Height, Header.Width ),
            TexFile.TextureFormat.B8G8R8A8 => ImageParsing.DecodeUncompressedB8G8R8A8( data, Header.Height, Header.Width ),
            TexFile.TextureFormat.B8G8R8X8 => ImageParsing.DecodeUncompressedR8G8B8A8( data, Header.Height, Header.Width ),
            //TexFile.TextureFormat.A8R8G8B82 => ImageParsing.DecodeUncompressedR8G8B8A8( data, Header.Height, Header.Width ),
            TexFile.TextureFormat.B4G4R4A4 => ImageParsing.DecodeUncompressedR4G4B4A4( data, Header.Height, Header.Width ),
            TexFile.TextureFormat.B5G5R5A1 => ImageParsing.DecodeUncompressedR5G5B5A1( data, Header.Height, Header.Width ),
            _                              => throw new ArgumentOutOfRangeException(),
        };
    }
}