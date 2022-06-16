using System;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.Import.Dds;

public static partial class ImageParsing
{
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static Rgba32 Get565Color( ushort c )
    {
        var ret = new Rgba32
        {
            R = ( byte )( c          & 0x1F ),
            G = ( byte )( ( c >> 5 ) & 0x3F ),
            B = ( byte )( c >> 11 ),
            A = 0xFF,
        };

        ret.R = ( byte )( ( ret.R << 3 ) | ( ret.R >> 2 ) );
        ret.G = ( byte )( ( ret.G << 2 ) | ( ret.G >> 3 ) );
        ret.B = ( byte )( ( ret.B << 3 ) | ( ret.B >> 2 ) );

        return ret;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static (Rgba32, Rgba32) GetDxt1CombinedColors( bool c1Larger, Rgba32 c1, Rgba32 c2 )
    {
        if( c1Larger )
        {
            static byte C( byte a1, byte a2 )
                => ( byte )( ( 2 * a1 + a2 ) / 3 );

            return ( new Rgba32( C( c1.R, c2.R ), C( c1.G, c2.G ), C( c1.B, c2.B ) ),
                new Rgba32( C( c2.R, c1.R ), C( c2.G, c1.G ), C( c2.B, c1.B ) ) );
        }
        else
        {
            static byte C( byte a1, byte a2 )
                => ( byte )( ( a1 + a2 ) / 2 );

            return ( new Rgba32( C( c1.R, c2.R ), C( c1.G, c2.G ), C( c1.B, c2.B ) ),
                new Rgba32( 0, 0, 0, 0 ) );
        }
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static unsafe byte* CopyBytes( byte* ptr, Rgba32 color, byte alpha )
    {
        *ptr++ = color.R;
        *ptr++ = color.G;
        *ptr++ = color.B;
        *ptr++ = alpha;
        return ptr;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static unsafe byte* CopyBytes( byte* ptr, Rgba32 color )
        => CopyBytes( ptr, color, color.A );


    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static unsafe byte* CopyBytesAlphaDown( byte* ptr, Rgba32 color, byte alpha )
        => CopyBytes( ptr, color, ( byte )( ( alpha & 0x0F ) | ( alpha << 4 ) ) );

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static unsafe byte* CopyBytesAlphaUp( byte* ptr, Rgba32 color, byte alpha )
        => CopyBytes( ptr, color, ( byte )( ( alpha & 0xF0 ) | ( alpha >> 4 ) ) );

    private static void Verify( ReadOnlySpan< byte > data, int height, int width, int blockSize, int bytes )
    {
        if( data.Length % bytes != 0 )
        {
            throw new ArgumentException( $"Length {data.Length} not a multiple of {bytes} bytes.", nameof( data ) );
        }

        if( height * width > data.Length * blockSize * blockSize / bytes )
        {
            throw new ArgumentException( $"Not enough data encoded in {data.Length} to fill image of dimensions {height} * {width}.",
                nameof( data ) );
        }

        if( height % blockSize != 0 )
        {
            throw new ArgumentException( $"Height must be a multiple of {blockSize}.", nameof( height ) );
        }

        if( width % blockSize != 0 )
        {
            throw new ArgumentException( $"Height must be a multiple of {blockSize}.", nameof( height ) );
        }
    }

    private static unsafe byte* GetDxt1Colors( byte* ptr, Span< Rgba32 > colors )
    {
        var c1 = ( ushort )( *ptr     | ( ptr[ 1 ] << 8 ) );
        var c2 = ( ushort )( ptr[ 2 ] | ( ptr[ 3 ] << 8 ) );
        colors[ 0 ]                  = Get565Color( c1 );
        colors[ 1 ]                  = Get565Color( c2 );
        ( colors[ 2 ], colors[ 3 ] ) = GetDxt1CombinedColors( c1 > c2, colors[ 0 ], colors[ 1 ] );
        return ptr + 4;
    }

    public static unsafe byte[] DecodeDxt1( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 4, 8 );

        var            ret    = new byte[data.Length * 8];
        Span< Rgba32 > colors = stackalloc Rgba32[4];

        fixed( byte* r = ret, d = data )
        {
            var inputPtr = d;
            for( var y = 0; y < height; y += 4 )
            {
                var outputPtr = r + y * width * 4;
                for( var x = 0; x < width; x += 4 )
                {
                    inputPtr = GetDxt1Colors( inputPtr, colors );
                    for( var j = 0; j < 4; ++j )
                    {
                        var outputPtr2 = outputPtr + 4 * ( x + j * width );
                        var colorMask  = *inputPtr++;
                        outputPtr2 = CopyBytes( outputPtr2, colors[ colorMask          & 0b11 ] );
                        outputPtr2 = CopyBytes( outputPtr2, colors[ ( colorMask >> 2 ) & 0b11 ] );
                        outputPtr2 = CopyBytes( outputPtr2, colors[ ( colorMask >> 4 ) & 0b11 ] );
                        CopyBytes( outputPtr2, colors[ ( colorMask >> 6 ) & 0b11 ] );
                    }
                }
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeDxt3( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 4, 16 );
        var            ret    = new byte[data.Length * 4];
        Span< Rgba32 > colors = stackalloc Rgba32[4];

        fixed( byte* r = ret, d = data )
        {
            var inputPtr = d;
            for( var y = 0; y < height; y += 4 )
            {
                var outputPtr = r + y * width * 4;
                for( var x = 0; x < width; x += 4 )
                {
                    var alphaPtr = inputPtr;
                    inputPtr = GetDxt1Colors( inputPtr + 8, colors );
                    for( var j = 0; j < 4; ++j )
                    {
                        var outputPtr2 = outputPtr + 4 * ( x + j * width );
                        var colorMask  = *inputPtr++;
                        outputPtr2 = CopyBytesAlphaDown( outputPtr2, colors[ colorMask          & 0b11 ], *alphaPtr );
                        outputPtr2 = CopyBytesAlphaUp( outputPtr2, colors[ ( colorMask   >> 2 ) & 0b11 ], *alphaPtr++ );
                        outputPtr2 = CopyBytesAlphaDown( outputPtr2, colors[ ( colorMask >> 4 ) & 0b11 ], *alphaPtr );
                        CopyBytesAlphaUp( outputPtr2, colors[ ( colorMask >> 6 ) & 0b11 ], *alphaPtr++ );
                    }
                }
            }
        }

        return ret;
    }

    private static unsafe byte* Dxt5AlphaTable( byte* ptr, Span< byte > alphaValues )
    {
        var alphaLookup = stackalloc byte[8];
        alphaLookup[ 0 ] = *ptr++;
        alphaLookup[ 1 ] = *ptr++;
        if( alphaLookup[ 0 ] > alphaLookup[ 1 ] )
        {
            alphaLookup[ 2 ] = ( byte )( ( 6 * alphaLookup[ 0 ] + alphaLookup[ 1 ] )     / 7 );
            alphaLookup[ 3 ] = ( byte )( ( 5 * alphaLookup[ 0 ] + 2 * alphaLookup[ 1 ] ) / 7 );
            alphaLookup[ 4 ] = ( byte )( ( 4 * alphaLookup[ 0 ] + 3 * alphaLookup[ 1 ] ) / 7 );
            alphaLookup[ 5 ] = ( byte )( ( 3 * alphaLookup[ 0 ] + 4 * alphaLookup[ 1 ] ) / 7 );
            alphaLookup[ 6 ] = ( byte )( ( 2 * alphaLookup[ 0 ] + 5 * alphaLookup[ 1 ] ) / 7 );
            alphaLookup[ 7 ] = ( byte )( ( alphaLookup[ 0 ]     + 6 * alphaLookup[ 1 ] ) / 7 );
        }
        else
        {
            alphaLookup[ 2 ] = ( byte )( ( 4 * alphaLookup[ 0 ] + alphaLookup[ 1 ] )     / 5 );
            alphaLookup[ 3 ] = ( byte )( ( 3 * alphaLookup[ 0 ] + 3 * alphaLookup[ 1 ] ) / 5 );
            alphaLookup[ 4 ] = ( byte )( ( 2 * alphaLookup[ 0 ] + 2 * alphaLookup[ 1 ] ) / 5 );
            alphaLookup[ 5 ] = ( byte )( ( alphaLookup[ 0 ]     + alphaLookup[ 1 ] )     / 5 );
            alphaLookup[ 6 ] = byte.MinValue;
            alphaLookup[ 7 ] = byte.MaxValue;
        }

        var alphaLong = ( ulong )*ptr++;
        alphaLong |= ( ulong )*ptr++ << 8;
        alphaLong |= ( ulong )*ptr++ << 16;
        alphaLong |= ( ulong )*ptr++ << 24;
        alphaLong |= ( ulong )*ptr++ << 32;
        alphaLong |= ( ulong )*ptr++ << 40;

        for( var i = 0; i < 16; ++i )
        {
            alphaValues[ i ] = alphaLookup[ ( alphaLong >> ( i * 3 ) ) & 0x07 ];
        }

        return ptr;
    }

    public static unsafe byte[] DecodeDxt5( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 4, 16 );
        var            ret         = new byte[data.Length * 4];
        Span< Rgba32 > colors      = stackalloc Rgba32[4];
        Span< byte >   alphaValues = stackalloc byte[16];

        fixed( byte* r = ret, d = data, a = alphaValues )
        {
            var inputPtr = d;
            for( var y = 0; y < height; y += 4 )
            {
                var outputPtr = r + y * width * 4;
                for( var x = 0; x < width; x += 4 )
                {
                    inputPtr = Dxt5AlphaTable( inputPtr, alphaValues );
                    inputPtr = GetDxt1Colors( inputPtr, colors );
                    var alphaPtr = a;
                    for( var j = 0; j < 4; ++j )
                    {
                        var outputPtr2 = outputPtr + 4 * ( x + j * width );
                        var colorMask  = *inputPtr++;
                        outputPtr2 = CopyBytesAlphaDown( outputPtr2, colors[ colorMask          & 0b11 ], *alphaPtr++ );
                        outputPtr2 = CopyBytesAlphaUp( outputPtr2, colors[ ( colorMask   >> 2 ) & 0b11 ], *alphaPtr++ );
                        outputPtr2 = CopyBytesAlphaDown( outputPtr2, colors[ ( colorMask >> 4 ) & 0b11 ], *alphaPtr++ );
                        CopyBytesAlphaUp( outputPtr2, colors[ ( colorMask >> 6 ) & 0b11 ], *alphaPtr++ );
                    }
                }
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeBc4( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 4, 8 );
        var          ret           = new byte[data.Length * 8];
        Span< byte > channelValues = stackalloc byte[16];

        fixed( byte* r = ret, d = data, a = channelValues )
        {
            var inputPtr = d;
            for( var y = 0; y < height; y += 4 )
            {
                var outputPtr = r + y * width * 4;
                for( var x = 0; x < width; x += 4 )
                {
                    inputPtr = Dxt5AlphaTable( inputPtr, channelValues );
                    var channelPtr = a;
                    for( var j = 0; j < 4; ++j )
                    {
                        var outputPtr2 = outputPtr + 4 * ( x + j * width );
                        outputPtr2 = CopyBytes( outputPtr2, new Rgba32( *channelPtr, *channelPtr, *channelPtr++, 0xFF ) );
                        outputPtr2 = CopyBytes( outputPtr2, new Rgba32( *channelPtr, *channelPtr, *channelPtr++, 0xFF ) );
                        outputPtr2 = CopyBytes( outputPtr2, new Rgba32( *channelPtr, *channelPtr, *channelPtr++, 0xFF ) );
                        CopyBytes( outputPtr2, new Rgba32( *channelPtr, *channelPtr, *channelPtr++, 0xFF ) );
                    }
                }
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeBc5( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 4, 16 );
        var          ret      = new byte[data.Length * 4];
        Span< byte > channel1 = stackalloc byte[16];
        Span< byte > channel2 = stackalloc byte[16];

        fixed( byte* r = ret, d = data, a = channel1, b = channel2 )
        {
            var inputPtr = d;
            for( var y = 0; y < height; y += 4 )
            {
                var outputPtr = r + y * width * 4;
                for( var x = 0; x < width; x += 4 )
                {
                    inputPtr = Dxt5AlphaTable( inputPtr, channel1 );
                    inputPtr = Dxt5AlphaTable( inputPtr, channel2 );
                    var channel1Ptr = a;
                    var channel2Ptr = b;
                    for( var j = 0; j < 4; ++j )
                    {
                        var outputPtr2 = outputPtr + 4 * ( x + j * width );
                        outputPtr2 = CopyBytes( outputPtr2, new Rgba32( *channel1Ptr++, *channel2Ptr++, 0, 0xFF ) );
                        outputPtr2 = CopyBytes( outputPtr2, new Rgba32( *channel1Ptr++, *channel2Ptr++, 0, 0xFF ) );
                        outputPtr2 = CopyBytes( outputPtr2, new Rgba32( *channel1Ptr++, *channel2Ptr++, 0, 0xFF ) );
                        CopyBytes( outputPtr2, new Rgba32( *channel1Ptr++, *channel2Ptr++, 0, 0xFF ) );
                    }
                }
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedGreyscale( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 1 );
        var ret = new byte[data.Length * 4];

        fixed( byte* r = ret, d = data )
        {
            var ptr   = r;
            var end   = d + data.Length;
            var input = d;
            while( input != end )
            {
                *ptr++ = *input;
                *ptr++ = *input;
                *ptr++ = *input++;
                *ptr++ = 0xFF;
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedR4G4B4A4( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 2 );
        var ret = new byte[data.Length * 2];

        fixed( byte* r = ret, d = data )
        {
            var ptr   = r;
            var input = ( ushort* )d;
            foreach( var b in data )
            {
                *ptr++ = ( byte )( ( b << 4 ) | ( b & 0x0F ) );
                *ptr++ = ( byte )( ( b >> 4 ) | ( b & 0xF0 ) );
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedB4G4R4A4( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 2 );
        var ret = new byte[data.Length * 2];

        fixed( byte* r = ret, d = data )
        {
            var ptr = r;
            foreach( var b in new Span< ushort >( d, data.Length / 2 ) )
            {
                *ptr++ = ( byte )( ( ( b >> 8 ) & 0x0F ) | ( ( b >> 4 ) & 0xF0 ) );
                *ptr++ = ( byte )( ( b          & 0xF0 ) | ( ( b >> 4 ) & 0x0F ) );

                *ptr++ = ( byte )( ( b          & 0x0F ) | ( b << 4 ) );
                *ptr++ = ( byte )( ( ( b >> 8 ) & 0xF0 ) | ( b >> 12 ) );
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedR5G5B5( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 2 );
        var ret = new byte[data.Length * 2];

        fixed( byte* r = ret, d = data )
        {
            var ptr = r;
            foreach( var b in new Span< ushort >( d, data.Length / 2 ) )
            {
                *ptr++ = ( byte )( ( b & 0x1F ) | ( b << 3 ) );
                var tmp = b & 0x03E0;
                *ptr++ = ( byte )( ( tmp >> 2 ) | ( tmp >> 7 ) );
                tmp    = b & 0x7C00;
                *ptr++ = ( byte )( ( tmp >> 12 ) | ( tmp >> 7 ) );
                *ptr++ = 0xFF;
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedB5G5R5( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 2 );
        var ret = new byte[data.Length * 2];

        fixed( byte* r = ret, d = data )
        {
            var ptr = r;
            foreach( var b in new Span< ushort >( d, data.Length / 2 ) )
            {
                var tmp = b & 0x7C00;
                *ptr++ = ( byte )( ( tmp >> 12 ) | ( tmp >> 7 ) );
                tmp    = b & 0x03E0;
                *ptr++ = ( byte )( ( tmp >> 2 ) | ( tmp >> 7 ) );
                *ptr++ = ( byte )( ( b & 0x1F ) | ( b   << 3 ) );
                *ptr++ = 0xFF;
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedR5G6B5( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 2 );
        var ret = new byte[data.Length * 2];

        fixed( byte* r = ret, d = data )
        {
            var ptr = r;
            foreach( var b in new Span< ushort >( d, data.Length / 2 ) )
            {
                *ptr++ = ( byte )( ( b & 0x1F ) | ( b << 3 ) );
                var tmp = b & 0x07E0;
                *ptr++ = ( byte )( ( tmp >> 3 ) | ( tmp >> 9 ) );
                tmp    = b & 0xF800;
                *ptr++ = ( byte )( ( tmp >> 14 ) | ( tmp >> 9 ) );
                *ptr++ = 0xFF;
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedB5G6R5( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 2 );
        var ret = new byte[data.Length * 2];

        fixed( byte* r = ret, d = data )
        {
            var ptr = r;
            foreach( var b in new Span< ushort >( d, data.Length / 2 ) )
            {
                var tmp = b & 0xF800;
                *ptr++ = ( byte )( ( tmp >> 14 ) | ( tmp >> 9 ) );
                tmp    = b & 0x07E0;
                *ptr++ = ( byte )( ( tmp >> 3 ) | ( tmp >> 9 ) );
                *ptr++ = ( byte )( ( b & 0x1F ) | ( b   << 3 ) );
                *ptr++ = 0xFF;
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedR5G5B5A1( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 2 );
        var ret = new byte[data.Length * 2];

        fixed( byte* r = ret, d = data )
        {
            var ptr = r;
            foreach( var b in new Span< ushort >( d, data.Length / 2 ) )
            {
                *ptr++ = ( byte )( ( b & 0x1F ) | ( b << 3 ) );
                var tmp = b & 0x03E0;
                *ptr++ = ( byte )( ( tmp >> 2 ) | ( tmp >> 7 ) );
                tmp    = b & 0x7C00;
                *ptr++ = ( byte )( ( tmp >> 12 ) | ( tmp >> 7 ) );
                *ptr++ = 0xFF;
                *ptr++ = ( byte )( b > 0x7FFF ? 0xFF : 0x00 );
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedB5G5R5A1( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 2 );
        var ret = new byte[data.Length * 2];

        fixed( byte* r = ret, d = data )
        {
            var ptr = r;
            foreach( var b in new Span< ushort >( d, data.Length / 2 ) )
            {
                var tmp = b & 0x7C00;
                *ptr++ = ( byte )( ( tmp >> 12 ) | ( tmp >> 7 ) );
                tmp    = b & 0x03E0;
                *ptr++ = ( byte )( ( tmp >> 2 ) | ( tmp >> 7 ) );
                *ptr++ = ( byte )( ( b & 0x1F ) | ( b   << 3 ) );
                *ptr++ = ( byte )( b > 0x7FFF ? 0xFF : 0x00 );
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedR8G8B8( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 3 );
        var ret = new byte[data.Length * 4 / 3];

        fixed( byte* r = ret, d = data )
        {
            var ptr   = r;
            var end   = d + data.Length;
            var input = d;
            while( input != end )
            {
                *ptr++ = *input++;
                *ptr++ = *input++;
                *ptr++ = *input++;
                *ptr++ = 0xFF;
            }
        }

        return ret;
    }

    public static unsafe byte[] DecodeUncompressedB8G8R8( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 3 );
        var ret = new byte[data.Length * 4 / 3];

        fixed( byte* r = ret, d = data )
        {
            var ptr   = r;
            var end   = d + data.Length;
            var input = d;
            while( input != end )
            {
                var b = *input++;
                var g = *input++;
                *ptr++ = *input++;
                *ptr++ = g;
                *ptr++ = b;
                *ptr++ = 0xFF;
            }
        }

        return ret;
    }

    public static byte[] DecodeUncompressedR8G8B8A8( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 4 );
        var ret = new byte[data.Length];
        data.CopyTo( ret );
        return ret;
    }

    public static unsafe byte[] DecodeUncompressedB8G8R8A8( ReadOnlySpan< byte > data, int height, int width )
    {
        Verify( data, height, width, 1, 4 );
        var ret = new byte[data.Length];

        fixed( byte* r = ret, d = data )
        {
            var ptr   = r;
            var end   = d + data.Length;
            var input = d;
            while( input != end )
            {
                var b = *input++;
                var g = *input++;
                *ptr++ = *input++;
                *ptr++ = g;
                *ptr++ = b;
                *ptr++ = *input++;
            }
        }

        return ret;
    }
}