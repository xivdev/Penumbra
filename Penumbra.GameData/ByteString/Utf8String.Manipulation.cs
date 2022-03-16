using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Penumbra.GameData.Util;

namespace Penumbra.GameData.ByteString;

public sealed unsafe partial class Utf8String
{
    // Create a C# Unicode string from this string.
    // If the string is known to be pure ASCII, use that encoding, otherwise UTF8.
    public override string ToString()
        => Length == 0
            ? string.Empty
            : ( _length & AsciiFlag ) != 0
                ? Encoding.ASCII.GetString( _path, Length )
                : Encoding.UTF8.GetString( _path, Length );


    // Convert the ascii portion of the string to lowercase.
    // Only creates a new string and copy if the string is not already known to be lowercase.
    public Utf8String AsciiToLower()
        => ( _length & AsciiLowerFlag ) == 0
            ? new Utf8String().Setup( ByteStringFunctions.AsciiToLower( _path, Length ), Length, null, true, true, true, IsAsciiInternal )
            : this;

    // Convert the ascii portion of the string to lowercase.
    // Guaranteed to create an owned copy.
    public Utf8String AsciiToLowerClone()
        => ( _length & AsciiLowerFlag ) == 0
            ? new Utf8String().Setup( ByteStringFunctions.AsciiToLower( _path, Length ), Length, null, true, true, true, IsAsciiInternal )
            : Clone();

    // Create an owned copy of the given string.
    public Utf8String Clone()
    {
        var ret = new Utf8String();
        ret._length = _length | OwnedFlag | NullTerminatedFlag;
        ret._path   = ByteStringFunctions.CopyString(Path, Length);
        ret._crc32  = Crc32;
        return ret;
    }

    // Create a non-owning substring from the given position.
    // If from is negative or too large, the returned string will be the empty string.
    public Utf8String Substring( int from )
        => ( uint )from < Length
            ? FromByteStringUnsafe( _path + from, Length - from, IsNullTerminated, IsAsciiLowerInternal, IsAsciiInternal )
            : Empty;

    // Create a non-owning substring from the given position of the given length.
    // If from is negative or too large, the returned string will be the empty string.
    // If from + length is too large, it will be the same as if length was not specified.
    public Utf8String Substring( int from, int length )
    {
        var maxLength = Length - ( uint )from;
        if( maxLength <= 0 )
        {
            return Empty;
        }

        return length < maxLength
            ? FromByteStringUnsafe( _path + from, length, false, IsAsciiLowerInternal, IsAsciiInternal )
            : Substring( from );
    }

    // Create a owned copy of the string and replace all occurences of from with to in it.
    public Utf8String Replace( byte from, byte to )
    {
        var length      = Length;
        var newPtr      = ByteStringFunctions.CopyString( _path, length );
        var numReplaced = ByteStringFunctions.Replace( newPtr, length, from, to );
        return new Utf8String().Setup( newPtr, length, numReplaced == 0 ? _crc32 : null, true, true, IsAsciiLowerInternal, IsAsciiInternal );
    }

    // Join a number of strings with a given byte between them.
    public static Utf8String Join( byte splitter, params Utf8String[] strings )
    {
        var length = strings.Sum( s => s.Length ) + strings.Length;
        var data   = ( byte* )Marshal.AllocHGlobal( length );

        var   ptr     = data;
        bool? isLower = ByteStringFunctions.AsciiIsLower( splitter );
        bool? isAscii = splitter < 128;
        foreach( var s in strings )
        {
            Functions.MemCpyUnchecked( ptr, s.Path, s.Length );
            ptr     += s.Length;
            *ptr++  =  splitter;
            isLower =  Combine( isLower, s.IsAsciiLowerInternal );
            isAscii &= s.IsAscii;
        }

        --length;
        data[ length ] = 0;
        var ret = FromByteStringUnsafe( data, length, true, isLower, isAscii );
        ret._length |= OwnedFlag;
        return ret;
    }

    // Split a string and return a list of the substrings delimited by b.
    // You can specify the maximum number of splits (if the maximum is reached, the last substring may contain delimiters).
    // You can also specify to ignore empty substrings inside delimiters. Those are also not counted for max splits.
    public List< Utf8String > Split( byte b, int maxSplits = int.MaxValue, bool removeEmpty = true )
    {
        var ret   = new List< Utf8String >();
        var start = 0;
        for( var idx = IndexOf( b, start ); idx >= 0; idx = IndexOf( b, start ) )
        {
            if( start != idx || !removeEmpty )
            {
                ret.Add( Substring( start, idx - start ) );
            }

            start = idx + 1;
            if( ret.Count == maxSplits - 1 )
            {
                break;
            }
        }

        ret.Add( Substring( start ) );
        return ret;
    }

    private static bool? Combine( bool? val1, bool? val2 )
    {
        return ( val1, val2 ) switch
        {
            (null, null)   => null,
            (null, true)   => null,
            (null, false)  => false,
            (true, null)   => null,
            (true, true)   => true,
            (true, false)  => false,
            (false, null)  => false,
            (false, true)  => false,
            (false, false) => false,
        };
    }
}