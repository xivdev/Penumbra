using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Penumbra.GameData.Util;

namespace Penumbra.GameData.ByteString;

public sealed unsafe partial class Utf8String : IDisposable
{
    // statically allocated null-terminator for empty strings to point to.
    private static readonly ByteStringFunctions.NullTerminator Null = new();

    public static readonly Utf8String Empty = new();

    // actual data members.
    private byte* _path;
    private uint  _length;
    private int   _crc32;

    // Create an empty string.
    public Utf8String()
    {
        _path   =  Null.NullBytePtr;
        _length |= AsciiCheckedFlag | AsciiFlag | AsciiLowerCheckedFlag | AsciiLowerFlag | NullTerminatedFlag | AsciiFlag;
        _crc32  =  0;
    }

    // Create a temporary Utf8String from a byte pointer.
    // This computes CRC, checks for ASCII and AsciiLower and assumes Null-Termination.
    public Utf8String( byte* path )
    {
        var length = Functions.ComputeCrc32AsciiLowerAndSize( path, out var crc32, out var lower, out var ascii );
        Setup( path, length, crc32, true, false, lower, ascii );
    }

    // Construct a temporary Utf8String from a given byte string of known size. 
    // Other known attributes can also be provided and are not computed.
    // Can throw ArgumentOutOfRange if length is higher than max length.
    // The Crc32 will be computed.
    public static Utf8String FromByteStringUnsafe( byte* path, int length, bool isNullTerminated, bool? isLower = null, bool? isAscii = false )
        => new Utf8String().Setup( path, length, null, isNullTerminated, false, isLower, isAscii );

    // Same as above, just with a span.
    public static Utf8String FromSpanUnsafe( ReadOnlySpan< byte > path, bool isNullTerminated, bool? isLower = null, bool? isAscii = false )
    {
        fixed( byte* ptr = path )
        {
            return FromByteStringUnsafe( ptr, path.Length, isNullTerminated, isLower, isAscii );
        }
    }

    // Construct a Utf8String from a given unicode string, possibly converted to ascii lowercase.
    // Only returns false if the length exceeds the max length.
    public static bool FromString( string? path, out Utf8String ret, bool toAsciiLower = false )
    {
        if( string.IsNullOrEmpty( path ) )
        {
            ret = Empty;
            return true;
        }

        var p = ByteStringFunctions.Utf8FromString( path, out var l, ( int )FlagMask );
        if( p == null )
        {
            ret = Empty;
            return false;
        }

        if( toAsciiLower )
        {
            ByteStringFunctions.AsciiToLowerInPlace( p, l );
        }

        ret = new Utf8String().Setup( p, l, null, true, true, toAsciiLower ? true : null, l == path.Length );
        return true;
    }

    // Does not check for length and just assumes the isLower state from the second argument.
    public static Utf8String FromStringUnsafe( string? path, bool? isLower )
    {
        if( string.IsNullOrEmpty( path ) )
        {
            return Empty;
        }

        var p   = ByteStringFunctions.Utf8FromString( path, out var l );
        var ret = new Utf8String().Setup( p, l, null, true, true, isLower, l == path.Length );
        return ret;
    }

    // Free memory if the string is owned.
    private void ReleaseUnmanagedResources()
    {
        if( !IsOwned )
        {
            return;
        }

        Marshal.FreeHGlobal( ( IntPtr )_path );
        GC.RemoveMemoryPressure( Length + 1 );
        _length = AsciiCheckedFlag | AsciiFlag | AsciiLowerCheckedFlag | AsciiLowerFlag | NullTerminatedFlag;
        _path   = Null.NullBytePtr;
        _crc32  = 0;
    }

    // Manually free memory. Sets the string to an empty string.
    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize( this );
    }

    ~Utf8String()
    {
        ReleaseUnmanagedResources();
    }

    // Setup from all given values.
    // Only called from constructors or factory functions in this library.
    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    internal Utf8String Setup( byte* path, int length, int? crc32, bool isNullTerminated, bool isOwned,
        bool? isLower = null, bool? isAscii = null )
    {
        if( length > FlagMask )
        {
            throw new ArgumentOutOfRangeException( nameof( length ) );
        }

        _path   = path;
        _length = ( uint )length;
        _crc32  = crc32 ?? ( int )~Lumina.Misc.Crc32.Get( new ReadOnlySpan< byte >( path, length ) );
        if( isNullTerminated )
        {
            _length |= NullTerminatedFlag;
        }

        if( isOwned )
        {
            GC.AddMemoryPressure( length + 1 );
            _length |= OwnedFlag;
        }

        if( isLower != null )
        {
            _length |= AsciiLowerCheckedFlag;
            if( isLower.Value )
            {
                _length |= AsciiLowerFlag;
            }
        }

        if( isAscii != null )
        {
            _length |= AsciiCheckedFlag;
            if( isAscii.Value )
            {
                _length |= AsciiFlag;
            }
        }

        return this;
    }

    private bool CheckAscii()
    {
        switch( _length & ( AsciiCheckedFlag | AsciiFlag ) )
        {
            case AsciiCheckedFlag:             return false;
            case AsciiCheckedFlag | AsciiFlag: return true;
            default:
                _length |= AsciiCheckedFlag;
                var isAscii = ByteStringFunctions.IsAscii( _path, Length );
                if( isAscii )
                {
                    _length |= AsciiFlag;
                }

                return isAscii;
        }
    }

    private bool CheckAsciiLower()
    {
        switch( _length & ( AsciiLowerCheckedFlag | AsciiLowerFlag ) )
        {
            case AsciiLowerCheckedFlag:                  return false;
            case AsciiLowerCheckedFlag | AsciiLowerFlag: return true;
            default:
                _length |= AsciiLowerCheckedFlag;
                var isAsciiLower = ByteStringFunctions.IsAsciiLowerCase( _path, Length );
                if( isAsciiLower )
                {
                    _length |= AsciiLowerFlag;
                }

                return isAsciiLower;
        }
    }

    private bool? IsAsciiInternal
        => ( _length & ( AsciiCheckedFlag | AsciiFlag ) ) switch
        {
            AsciiCheckedFlag => false,
            AsciiFlag        => true,
            _                => null,
        };

    private bool? IsAsciiLowerInternal
        => ( _length & ( AsciiLowerCheckedFlag | AsciiLowerFlag ) ) switch
        {
            AsciiLowerCheckedFlag => false,
            AsciiLowerFlag        => true,
            _                     => null,
        };
}