using System;
using System.IO;
using Lumina.Data.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.Import.Textures;

public static class TextureImporter
{
    private static void WriteHeader( byte[] target, int width, int height )
    {
        using var mem = new MemoryStream( target );
        using var bw  = new BinaryWriter( mem );
        bw.Write( ( uint )TexFile.Attribute.TextureType2D );
        bw.Write( ( uint )TexFile.TextureFormat.B8G8R8A8 );
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

    public static bool RgbaBytesToTex( byte[] rgba, int width, int height, out byte[] texData )
    {
        texData = Array.Empty< byte >();
        if( rgba.Length != width * height * 4 )
        {
            return false;
        }

        texData = new byte[80 + width * height * 4];
        WriteHeader( texData, width, height );
        rgba.CopyTo( texData.AsSpan( 80 ) );
        for( var i = 80; i < texData.Length; i += 4 )
            (texData[ i  ], texData[i + 2]) = (texData[ i + 2], texData[i]);
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
}