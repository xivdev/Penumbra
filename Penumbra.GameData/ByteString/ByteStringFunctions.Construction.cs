using System;
using System.Runtime.InteropServices;
using System.Text;
using Penumbra.GameData.Util;

namespace Penumbra.GameData.ByteString;

public static unsafe partial class ByteStringFunctions
{
    // Used for static null-terminators.
    public class NullTerminator
    {
        public readonly byte* NullBytePtr;

        public NullTerminator()
        {
            NullBytePtr  = ( byte* )Marshal.AllocHGlobal( 1 );
            *NullBytePtr = 0;
        }

        ~NullTerminator()
            => Marshal.FreeHGlobal( ( IntPtr )NullBytePtr );
    }

    // Convert a C# unicode-string to an unmanaged UTF8-byte array and return the pointer.
    // If the length would exceed the given maxLength, return a nullpointer instead.
    public static byte* Utf8FromString( string s, out int length, int maxLength = int.MaxValue )
    {
        length = Encoding.UTF8.GetByteCount( s );
        if( length >= maxLength )
        {
            return null;
        }

        var path = ( byte* )Marshal.AllocHGlobal( length + 1 );
        fixed( char* ptr = s )
        {
            Encoding.UTF8.GetBytes( ptr, length, path, length + 1 );
        }

        path[ length ] = 0;
        return path;
    }

    // Create a copy of a given string and return the pointer.
    public static byte* CopyString( byte* path, int length )
    {
        var ret = ( byte* )Marshal.AllocHGlobal( length + 1 );
        Functions.MemCpyUnchecked( ret, path, length );
        ret[ length ] = 0;
        return ret;
    }

    // Check the length of a null-terminated byte array no longer than the given maxLength.
    public static int CheckLength( byte* path, int maxLength = int.MaxValue )
    {
        var end = path + maxLength;
        for( var it = path; it < end; ++it )
        {
            if( *it == 0 )
            {
                return ( int )( it - path );
            }
        }

        throw new ArgumentOutOfRangeException( "Null-terminated path too long" );
    }
}