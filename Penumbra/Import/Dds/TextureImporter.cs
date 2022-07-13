using System;
using System.IO;
using Lumina.Data.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.Import.Dds;

public class TextureImporter
{
    private static void WriteHeader( byte[] target, int width, int height )
    {
        using var mem = new MemoryStream( target );
        using var bw  = new BinaryWriter( mem );
        bw.Write( ( uint )TexFile.Attribute.TextureType2D );
        bw.Write( ( uint )TexFile.TextureFormat.B8G8R8X8 );
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
        ddsData = new byte[4 + DdsHeader.Size + rgba.Length];
        header.Write( ddsData, 0 );
        rgba.CopyTo( ddsData, DdsHeader.Size + 4 );
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