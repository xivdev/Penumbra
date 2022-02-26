using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Penumbra.GameData.Util;

public static unsafe class ByteStringFunctions
{
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

    private static readonly byte[] LowerCaseBytes = Enumerable.Range( 0, 256 )
       .Select( i => ( byte )char.ToLowerInvariant( ( char )i ) )
       .ToArray();

    public static byte* FromString( string s, out int length )
    {
        length = Encoding.UTF8.GetByteCount( s );
        var path = ( byte* )Marshal.AllocHGlobal( length + 1 );
        fixed( char* ptr = s )
        {
            Encoding.UTF8.GetBytes( ptr, length, path, length + 1 );
        }

        path[ length ] = 0;
        return path;
    }

    public static byte* CopyPath( byte* path, int length )
    {
        var ret = ( byte* )Marshal.AllocHGlobal( length + 1 );
        Functions.MemCpyUnchecked( ret, path, length );
        ret[ length ] = 0;
        return ret;
    }

    public static int CheckLength( byte* path )
    {
        var end = path + int.MaxValue;
        for( var it = path; it < end; ++it )
        {
            if( *it == 0 )
            {
                return ( int )( it - path );
            }
        }

        throw new ArgumentOutOfRangeException( "Null-terminated path too long" );
    }

    public static int Compare( byte* lhs, int lhsLength, byte* rhs, int rhsLength )
    {
        if( lhsLength == rhsLength )
        {
            return lhs == rhs ? 0 : Functions.MemCmpUnchecked( lhs, rhs, rhsLength );
        }

        if( lhsLength < rhsLength )
        {
            var cmp = Functions.MemCmpUnchecked( lhs, rhs, lhsLength );
            return cmp != 0 ? cmp : -1;
        }

        var cmp2 = Functions.MemCmpUnchecked( lhs, rhs, rhsLength );
        return cmp2 != 0 ? cmp2 : 1;
    }

    public static int Compare( byte* lhs, int lhsLength, byte* rhs )
    {
        var end = lhs + lhsLength;
        for( var tmp = lhs; tmp < end; ++tmp, ++rhs )
        {
            if( *rhs == 0 )
            {
                return 1;
            }

            var diff = *tmp - *rhs;
            if( diff != 0 )
            {
                return diff;
            }
        }

        return 0;
    }

    public static int Compare( byte* lhs, byte* rhs, int maxLength = int.MaxValue )
    {
        var end = lhs + maxLength;
        for( var tmp = lhs; tmp < end; ++tmp, ++rhs )
        {
            if( *lhs == 0 )
            {
                return *rhs == 0 ? 0 : -1;
            }

            if( *rhs == 0 )
            {
                return 1;
            }

            var diff = *tmp - *rhs;
            if( diff != 0 )
            {
                return diff;
            }
        }

        return 0;
    }


    public static bool Equals( byte* lhs, int lhsLength, byte* rhs, int rhsLength )
    {
        if( lhsLength != rhsLength )
        {
            return false;
        }

        if( lhs == rhs || lhsLength == 0 )
        {
            return true;
        }

        return Functions.MemCmpUnchecked( lhs, rhs, lhsLength ) == 0;
    }

    private static bool Equal( byte* lhs, int lhsLength, byte* rhs )
        => Compare( lhs, lhsLength, rhs ) == 0;

    private static bool Equal( byte* lhs, byte* rhs, int maxLength = int.MaxValue )
        => Compare( lhs, rhs, maxLength ) == 0;


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

    public static void AsciiToLowerInPlace( byte* path, int length )
    {
        for( var i = 0; i < length; ++i )
        {
            path[ i ] = LowerCaseBytes[ path[ i ] ];
        }
    }

    public static byte* AsciiToLower( byte* path, int length )
    {
        var ptr = ( byte* )Marshal.AllocHGlobal( length + 1 );
        ptr[ length ] = 0;
        for( var i = 0; i < length; ++i )
        {
            ptr[ i ] = LowerCaseBytes[ path[ i ] ];
        }

        return ptr;
    }

    public static bool IsLowerCase( byte* path, int length )
    {
        for( var i = 0; i < length; ++i )
        {
            if( path[ i ] != LowerCaseBytes[ path[ i ] ] )
            {
                return false;
            }
        }

        return true;
    }
}

public unsafe class AsciiString : IEnumerable< byte >, IEquatable< AsciiString >, IComparable< AsciiString >
{
    private static readonly ByteStringFunctions.NullTerminator Null = new();

    [Flags]
    private enum Flags : byte
    {
        IsOwned          = 0x01,
        IsNullTerminated = 0x02,
        LowerCaseChecked = 0x04,
        IsLowerCase      = 0x08,
    }

    public readonly IntPtr Path;
    public readonly ulong  Crc64;
    public readonly int    Length;
    private         Flags  _flags;

    public bool IsNullTerminated
    {
        get => _flags.HasFlag( Flags.IsNullTerminated );
        init => _flags = value ? _flags | Flags.IsNullTerminated : _flags & ~ Flags.IsNullTerminated;
    }

    public bool IsOwned
    {
        get => _flags.HasFlag( Flags.IsOwned );
        init => _flags = value ? _flags | Flags.IsOwned : _flags & ~Flags.IsOwned;
    }

    public bool IsLowerCase
    {
        get
        {
            if( _flags.HasFlag( Flags.LowerCaseChecked ) )
            {
                return _flags.HasFlag( Flags.IsLowerCase );
            }

            _flags |= Flags.LowerCaseChecked;
            var ret = ByteStringFunctions.IsLowerCase( Ptr, Length );
            if( ret )
            {
                _flags |= Flags.IsLowerCase;
            }

            return ret;
        }
    }

    public bool IsEmpty
        => Length == 0;

    public AsciiString()
    {
        Path             =  ( IntPtr )Null.NullBytePtr;
        Length           =  0;
        IsNullTerminated =  true;
        IsOwned          =  false;
        _flags           |= Flags.LowerCaseChecked | Flags.IsLowerCase;
        Crc64            =  0;
    }

    public static bool FromString( string? path, out AsciiString ret, bool toLower = false )
    {
        if( string.IsNullOrEmpty( path ) )
        {
            ret = Empty;
            return true;
        }

        var p = ByteStringFunctions.FromString( path, out var l );
        if( l != path.Length )
        {
            ret = Empty;
            return false;
        }

        if( toLower )
        {
            ByteStringFunctions.AsciiToLowerInPlace( p, l );
        }

        ret = new AsciiString( p, l, true, true, toLower ? true : null );
        return true;
    }

    public static AsciiString FromStringUnchecked( string? path, bool? isLower )
    {
        if( string.IsNullOrEmpty( path ) )
        {
            return Empty;
        }

        var p = ByteStringFunctions.FromString( path, out var l );
        return new AsciiString( p, l, true, true, isLower );
    }

    public AsciiString( byte* path )
        : this( path, ByteStringFunctions.CheckLength( path ), true, false )
    { }

    protected AsciiString( byte* path, int length, bool isNullTerminated, bool isOwned, bool? isLower = null )
    {
        Length           = length;
        Path             = ( IntPtr )path;
        IsNullTerminated = isNullTerminated;
        IsOwned          = isOwned;
        Crc64            = Functions.ComputeCrc64( Span );
        if( isLower != null )
        {
            _flags |= Flags.LowerCaseChecked;
            if( isLower.Value )
            {
                _flags |= Flags.IsLowerCase;
            }
        }
    }

    public ReadOnlySpan< byte > Span
        => new(( void* )Path, Length);

    private byte* Ptr
        => ( byte* )Path;

    public override string ToString()
        => Encoding.ASCII.GetString( Ptr, Length );

    public IEnumerator< byte > GetEnumerator()
    {
        for( var i = 0; i < Length; ++i )
        {
            yield return Span[ i ];
        }
    }

    ~AsciiString()
    {
        if( IsOwned )
        {
            Marshal.FreeHGlobal( Path );
        }
    }

    public bool Equals( AsciiString? other )
    {
        if( ReferenceEquals( null, other ) )
        {
            return false;
        }

        if( ReferenceEquals( this, other ) )
        {
            return true;
        }

        return Crc64 == other.Crc64 && ByteStringFunctions.Equals( Ptr, Length, other.Ptr, other.Length );
    }

    public override int GetHashCode()
        => Crc64.GetHashCode();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int CompareTo( AsciiString? other )
    {
        if( ReferenceEquals( this, other ) )
        {
            return 0;
        }

        if( ReferenceEquals( null, other ) )
        {
            return 1;
        }

        return ByteStringFunctions.Compare( Ptr, Length, other.Ptr, other.Length );
    }

    private bool? IsLowerInternal
        => _flags.HasFlag( Flags.LowerCaseChecked ) ? _flags.HasFlag( Flags.IsLowerCase ) : null;

    public AsciiString Clone()
        => new(ByteStringFunctions.CopyPath( Ptr, Length ), Length, true, true, IsLowerInternal);

    public AsciiString Substring( int from )
        => from < Length
            ? new AsciiString( Ptr + from, Length - from, IsNullTerminated, false, IsLowerInternal )
            : Empty;

    public AsciiString Substring( int from, int length )
    {
        Debug.Assert( from >= 0 );
        if( from >= Length )
        {
            return Empty;
        }

        var maxLength = Length - from;
        return length < maxLength
            ? new AsciiString( Ptr + from, length, false, false, IsLowerInternal )
            : new AsciiString( Ptr + from, maxLength, true, false, IsLowerInternal );
    }

    public int IndexOf( byte b, int from = 0 )
    {
        var end = Ptr + Length;
        for( var tmp = Ptr + from; tmp < end; ++tmp )
        {
            if( *tmp == b )
            {
                return ( int )( tmp - Ptr );
            }
        }

        return -1;
    }

    public int LastIndexOf( byte b, int to = 0 )
    {
        var end = Ptr + to;
        for( var tmp = Ptr + Length - 1; tmp >= end; --tmp )
        {
            if( *tmp == b )
            {
                return ( int )( tmp - Ptr );
            }
        }

        return -1;
    }


    public static readonly AsciiString Empty = new();
}

public readonly struct NewGamePath
{
    public const int MaxGamePathLength = 256;

    private readonly AsciiString _string;

    private NewGamePath( AsciiString s )
        => _string = s;


    public static readonly NewGamePath Empty = new(AsciiString.Empty);

    public static NewGamePath FromStringUnchecked( string? s, bool? isLower )
        => new(AsciiString.FromStringUnchecked( s, isLower ));

    public static bool FromString( string? s, out NewGamePath path, bool toLower = false )
    {
        path = Empty;
        if( s.IsNullOrEmpty() )
        {
            return true;
        }

        var substring = s.Replace( '\\', '/' );
        substring.TrimStart( '/' );
        if( substring.Length > MaxGamePathLength )
        {
            return false;
        }

        if( substring.Length == 0 )
        {
            return true;
        }

        if( !AsciiString.FromString( substring, out var ascii, toLower ) )
        {
            return false;
        }

        path = new NewGamePath( ascii );
        return true;
    }

    public static bool FromFile( FileInfo file, DirectoryInfo baseDir, out NewGamePath path, bool toLower = false )
    {
        path = Empty;
        if( !file.FullName.StartsWith( baseDir.FullName ) )
        {
            return false;
        }

        var substring = file.FullName[ baseDir.FullName.Length.. ];
        return FromString( substring, out path, toLower );
    }

    public AsciiString Filename()
    {
        var idx = _string.LastIndexOf( ( byte )'/' );
        return idx == -1 ? _string : _string.Substring( idx + 1 );
    }

    public override string ToString()
        => _string.ToString();
}

public readonly struct GamePath : IComparable
{
    public const int MaxGamePathLength = 256;

    private readonly string _path;

    private GamePath( string path, bool _ )
        => _path = path;

    public GamePath( string? path )
    {
        if( path != null && path.Length < MaxGamePathLength )
        {
            _path = Lower( Trim( ReplaceSlash( path ) ) );
        }
        else
        {
            _path = string.Empty;
        }
    }

    public GamePath( FileInfo file, DirectoryInfo baseDir )
        => _path = CheckPre( file, baseDir ) ? Lower( Trim( ReplaceSlash( Substring( file, baseDir ) ) ) ) : "";

    private static bool CheckPre( FileInfo file, DirectoryInfo baseDir )
        => file.FullName.StartsWith( baseDir.FullName ) && file.FullName.Length < MaxGamePathLength;

    private static string Substring( FileInfo file, DirectoryInfo baseDir )
        => file.FullName.Substring( baseDir.FullName.Length );

    private static string ReplaceSlash( string path )
        => path.Replace( '\\', '/' );

    private static string Trim( string path )
        => path.TrimStart( '/' );

    private static string Lower( string path )
        => path.ToLowerInvariant();

    public static GamePath GenerateUnchecked( string path )
        => new(path, true);

    public static GamePath GenerateUncheckedLower( string path )
        => new(Lower( path ), true);

    public static implicit operator string( GamePath gamePath )
        => gamePath._path;

    public static explicit operator GamePath( string gamePath )
        => new(gamePath);

    public bool Empty
        => _path.Length == 0;

    public string Filename()
    {
        var idx = _path.LastIndexOf( "/", StringComparison.Ordinal );
        return idx == -1 ? _path : idx == _path.Length - 1 ? "" : _path[ ( idx + 1 ).. ];
    }

    public int CompareTo( object? rhs )
    {
        return rhs switch
        {
            string path   => string.Compare( _path, path, StringComparison.InvariantCulture ),
            GamePath path => string.Compare( _path, path._path, StringComparison.InvariantCulture ),
            _             => -1,
        };
    }

    public override string ToString()
        => _path;
}

public class GamePathConverter : JsonConverter
{
    public override bool CanConvert( Type objectType )
        => objectType == typeof( GamePath );

    public override object ReadJson( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer )
    {
        var token = JToken.Load( reader );
        return token.ToObject< GamePath >();
    }

    public override bool CanWrite
        => true;

    public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
    {
        if( value != null )
        {
            var v = ( GamePath )value;
            serializer.Serialize( writer, v.ToString() );
        }
    }
}