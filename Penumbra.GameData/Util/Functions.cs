using System;
using System.Runtime.InteropServices;

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

    [DllImport( "msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false )]
    private static extern unsafe IntPtr memcpy( byte* dest, byte* src, int count );

    public static unsafe void MemCpyUnchecked( byte* dest, byte* src, int count )
        => memcpy( dest, src, count );


    [DllImport( "msvcrt.dll", EntryPoint = "memcmp", CallingConvention = CallingConvention.Cdecl, SetLastError = false )]
    private static extern unsafe int memcmp( byte* b1, byte* b2, int count );

    public static unsafe int MemCmpUnchecked( byte* ptr1, byte* ptr2, int count )
        => memcmp( ptr1, ptr2, count );


    [DllImport( "msvcrt.dll", EntryPoint = "_memicmp", CallingConvention = CallingConvention.Cdecl, SetLastError = false )]
    private static extern unsafe int memicmp( byte* b1, byte* b2, int count );

    public static unsafe int MemCmpCaseInsensitiveUnchecked( byte* ptr1, byte* ptr2, int count )
        => memicmp( ptr1, ptr2, count );
}