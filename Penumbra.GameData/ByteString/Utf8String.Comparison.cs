using System;
using System.Linq;

namespace Penumbra.GameData.ByteString;

public sealed unsafe partial class Utf8String : IEquatable< Utf8String >, IComparable< Utf8String >
{
    public bool Equals( Utf8String? other )
    {
        if( ReferenceEquals( null, other ) )
        {
            return false;
        }

        if( ReferenceEquals( this, other ) )
        {
            return true;
        }

        return _crc32 == other._crc32 && ByteStringFunctions.Equals( _path, Length, other._path, other.Length );
    }

    public bool EqualsCi( Utf8String? other )
    {
        if( ReferenceEquals( null, other ) )
        {
            return false;
        }

        if( ReferenceEquals( this, other ) )
        {
            return true;
        }

        if( ( IsAsciiLowerInternal ?? false ) && ( other.IsAsciiLowerInternal ?? false ) )
        {
            return _crc32 == other._crc32 && ByteStringFunctions.Equals( _path, Length, other._path, other.Length );
        }

        return ByteStringFunctions.AsciiCaselessEquals( _path, Length, other._path, other.Length );
    }

    public int CompareTo( Utf8String? other )
    {
        if( ReferenceEquals( this, other ) )
        {
            return 0;
        }

        if( ReferenceEquals( null, other ) )
        {
            return 1;
        }

        return ByteStringFunctions.Compare( _path, Length, other._path, other.Length );
    }

    public int CompareToCi( Utf8String? other )
    {
        if( ReferenceEquals( null, other ) )
        {
            return 0;
        }

        if( ReferenceEquals( this, other ) )
        {
            return 1;
        }

        if( ( IsAsciiLowerInternal ?? false ) && ( other.IsAsciiLowerInternal ?? false ) )
        {
            return ByteStringFunctions.Compare( _path, Length, other._path, other.Length );
        }

        return ByteStringFunctions.AsciiCaselessCompare( _path, Length, other._path, other.Length );
    }

    public bool StartsWith( Utf8String other )
    {
        var otherLength = other.Length;
        return otherLength <= Length && ByteStringFunctions.Equals( other.Path, otherLength, Path, otherLength );
    }

    public bool EndsWith( Utf8String other )
    {
        var otherLength = other.Length;
        var offset      = Length - otherLength;
        return offset >= 0 && ByteStringFunctions.Equals( other.Path, otherLength, Path + offset, otherLength );
    }

    public bool StartsWith( params char[] chars )
    {
        if( chars.Length > Length )
        {
            return false;
        }

        var ptr = _path;
        return chars.All( t => *ptr++ == ( byte )t );
    }

    public bool EndsWith( params char[] chars )
    {
        if( chars.Length > Length )
        {
            return false;
        }

        var ptr = _path + Length - chars.Length;
        return chars.All( c => *ptr++ == ( byte )c );
    }

    public int IndexOf( byte b, int from = 0 )
    {
        var end = _path + Length;
        for( var tmp = _path + from; tmp < end; ++tmp )
        {
            if( *tmp == b )
            {
                return ( int )( tmp - _path );
            }
        }

        return -1;
    }

    public int LastIndexOf( byte b, int to = 0 )
    {
        var end = _path + to;
        for( var tmp = _path + Length - 1; tmp >= end; --tmp )
        {
            if( *tmp == b )
            {
                return ( int )( tmp - _path );
            }
        }

        return -1;
    }
}