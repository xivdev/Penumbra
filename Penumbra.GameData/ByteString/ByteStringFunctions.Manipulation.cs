using System.Runtime.InteropServices;

namespace Penumbra.GameData.ByteString;

public static unsafe partial class ByteStringFunctions
{
    // Replace all occurrences of from in a byte array of known length with to.
    public static int Replace( byte* ptr, int length, byte from, byte to )
    {
        var end         = ptr + length;
        var numReplaced = 0;
        for( ; ptr < end; ++ptr )
        {
            if( *ptr == from )
            {
                *ptr = to;
                ++numReplaced;
            }
        }

        return numReplaced;
    }

    // Convert a byte array of given length to ASCII-lowercase.
    public static void AsciiToLowerInPlace( byte* path, int length )
    {
        for( var i = 0; i < length; ++i )
        {
            path[ i ] = AsciiLowerCaseBytes[ path[ i ] ];
        }
    }

    // Copy a byte array and convert the copy to ASCII-lowercase.
    public static byte* AsciiToLower( byte* path, int length )
    {
        var ptr = ( byte* )Marshal.AllocHGlobal( length + 1 );
        ptr[ length ] = 0;
        for( var i = 0; i < length; ++i )
        {
            ptr[ i ] = AsciiLowerCaseBytes[ path[ i ] ];
        }

        return ptr;
    }
}