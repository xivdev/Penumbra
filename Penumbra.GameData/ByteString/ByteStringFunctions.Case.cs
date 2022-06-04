using System.Linq;
using System.Runtime.InteropServices;
using Penumbra.GameData.Util;

namespace Penumbra.GameData.ByteString;

public static unsafe partial class ByteStringFunctions
{
    private static readonly byte[] AsciiLowerCaseBytes = Enumerable.Range( 0, 256 )
       .Select( i => ( byte )char.ToLowerInvariant( ( char )i ) )
       .ToArray();

    // Convert a byte to its ASCII-lowercase version.
    public static byte AsciiToLower( byte b )
        => AsciiLowerCaseBytes[ b ];

    // Check if a byte is ASCII-lowercase.
    public static bool AsciiIsLower( byte b )
        => AsciiToLower( b ) == b;

    // Check if a byte array of given length is ASCII-lowercase.
    public static bool IsAsciiLowerCase( byte* path, int length )
    {
        var end = path + length;
        for( ; path < end; ++path )
        {
            if( *path != AsciiLowerCaseBytes[*path] )
            {
                return false;
            }
        }

        return true;
    }

    // Compare two byte arrays of given lengths ASCII-case-insensitive.
    public static int AsciiCaselessCompare( byte* lhs, int lhsLength, byte* rhs, int rhsLength )
    {
        if( lhsLength == rhsLength )
        {
            return lhs == rhs ? 0 : Functions.MemCmpCaseInsensitiveUnchecked( lhs, rhs, rhsLength );
        }

        if( lhsLength < rhsLength )
        {
            var cmp = Functions.MemCmpCaseInsensitiveUnchecked( lhs, rhs, lhsLength );
            return cmp != 0 ? cmp : -1;
        }

        var cmp2 = Functions.MemCmpCaseInsensitiveUnchecked( lhs, rhs, rhsLength );
        return cmp2 != 0 ? cmp2 : 1;
    }

    // Check two byte arrays of given lengths for ASCII-case-insensitive equality.
    public static bool AsciiCaselessEquals( byte* lhs, int lhsLength, byte* rhs, int rhsLength )
    {
        if( lhsLength != rhsLength )
        {
            return false;
        }

        if( lhs == rhs || lhsLength == 0 )
        {
            return true;
        }

        return Functions.MemCmpCaseInsensitiveUnchecked( lhs, rhs, lhsLength ) == 0;
    }

    // Check if a byte array of given length consists purely of ASCII characters.
    public static bool IsAscii( byte* path, int length )
    {
        var length8 = length / 8;
        var end8    = ( ulong* )path + length8;
        for( var ptr8 = ( ulong* )path; ptr8 < end8; ++ptr8 )
        {
            if( ( *ptr8 & 0x8080808080808080ul ) != 0 )
            {
                return false;
            }
        }

        var end = path + length;
        for( path += length8 * 8; path < end; ++path )
        {
            if( *path > 127 )
            {
                return false;
            }
        }

        return true;
    }
}