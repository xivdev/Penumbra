using System;
using System.Reflection;
using System.Runtime.InteropServices;
using ByteStringFunctions = Penumbra.GameData.ByteString.ByteStringFunctions;

namespace Penumbra.GameData.Util;

public static class Functions
{
    public static ulong ComputeCrc64( string name )
    {
        if( name.Length == 0 )
        {
            return 0;
        }

        var lastSlash = name.LastIndexOf( '/' );
        if( lastSlash == -1 )
        {
            return Lumina.Misc.Crc32.Get( name );
        }

        var folder = name[ ..lastSlash ];
        var file   = name[ ( lastSlash + 1 ).. ];
        return ( ( ulong )Lumina.Misc.Crc32.Get( folder ) << 32 ) | Lumina.Misc.Crc32.Get( file );
    }

    public static ulong ComputeCrc64( ReadOnlySpan< byte > name )
    {
        if( name.Length == 0 )
        {
            return 0;
        }

        var lastSlash = name.LastIndexOf( ( byte )'/' );
        if( lastSlash == -1 )
        {
            return Lumina.Misc.Crc32.Get( name );
        }

        var folder = name[ ..lastSlash ];
        var file   = name[ ( lastSlash + 1 ).. ];
        return ( ( ulong )Lumina.Misc.Crc32.Get( folder ) << 32 ) | Lumina.Misc.Crc32.Get( file );
    }

    private static readonly uint[] CrcTable =
        typeof( Lumina.Misc.Crc32 ).GetField( "CrcTable", BindingFlags.Static | BindingFlags.NonPublic )?.GetValue( null ) as uint[]
     ?? throw new Exception( "Could not fetch CrcTable from Lumina." );


    public static unsafe int ComputeCrc64LowerAndSize( byte* ptr, out ulong crc64, out int crc32Ret, out bool isLower, out bool isAscii )
    {
        var  tmp       = ptr;
        uint crcFolder = 0;
        uint crcFile   = 0;
        var  crc32     = uint.MaxValue;
        crc64   = 0;
        isLower = true;
        isAscii = true;
        while( true )
        {
            var value = *tmp;
            if( value == 0 )
            {
                break;
            }

            if( ByteStringFunctions.AsciiToLower( *tmp ) != *tmp )
            {
                isLower = false;
            }

            if( value > 0x80 )
            {
                isAscii = false;
            }

            if( value == ( byte )'/' )
            {
                crcFolder = crc32;
                crcFile   = uint.MaxValue;
                crc32     = CrcTable[ ( byte )( crc32 ^ value ) ] ^ ( crc32 >> 8 );
            }
            else
            {
                crcFile = CrcTable[ ( byte )( crcFolder ^ value ) ] ^ ( crcFolder >> 8 );
                crc32   = CrcTable[ ( byte )( crc32     ^ value ) ] ^ ( crc32     >> 8 );
            }

            ++tmp;
        }

        var size = ( int )( tmp - ptr );
        crc64    = ~( ( ulong )crcFolder << 32 ) | crcFile;
        crc32Ret = ( int )~crc32;
        return size;
    }

    public static unsafe int ComputeCrc32AsciiLowerAndSize( byte* ptr, out int crc32Ret, out bool isLower, out bool isAscii )
    {
        var tmp   = ptr;
        var crc32 = uint.MaxValue;
        isLower = true;
        isAscii = true;
        while( true )
        {
            var value = *tmp;
            if( value == 0 )
            {
                break;
            }

            if( ByteStringFunctions.AsciiToLower( *tmp ) != *tmp )
            {
                isLower = false;
            }

            if( value > 0x80 )
            {
                isAscii = false;
            }

            crc32 = CrcTable[ ( byte )( crc32 ^ value ) ] ^ ( crc32 >> 8 );
            ++tmp;
        }

        var size = ( int )( tmp - ptr );
        crc32Ret = ( int )~crc32;
        return size;
    }

    [DllImport( "msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false )]
    private static extern unsafe IntPtr memcpy( void* dest, void* src, int count );

    public static unsafe void MemCpyUnchecked( void* dest, void* src, int count )
        => memcpy( dest, src, count );


    [DllImport( "msvcrt.dll", EntryPoint = "memcmp", CallingConvention = CallingConvention.Cdecl, SetLastError = false )]
    private static extern unsafe int memcmp( void* b1, void* b2, int count );

    public static unsafe int MemCmpUnchecked( void* ptr1, void* ptr2, int count )
        => memcmp( ptr1, ptr2, count );


    [DllImport( "msvcrt.dll", EntryPoint = "_memicmp", CallingConvention = CallingConvention.Cdecl, SetLastError = false )]
    private static extern unsafe int memicmp( void* b1, void* b2, int count );

    public static unsafe int MemCmpCaseInsensitiveUnchecked( void* ptr1, void* ptr2, int count )
        => memicmp( ptr1, ptr2, count );

    [DllImport( "msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false )]
    private static extern unsafe void* memset( void* dest, int c, int count );

    public static unsafe void* MemSet( void* dest, byte value, int count )
        => memset( dest, value, count );
}