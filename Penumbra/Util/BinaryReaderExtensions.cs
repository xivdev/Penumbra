using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Penumbra.Util;

public static class BinaryReaderExtensions
{
    /// <summary>
    /// Reads a structure from the current stream position.
    /// </summary>
    /// <param name="br"></param>
    /// <typeparam name="T">The structure to read in to</typeparam>
    /// <returns>The file data as a structure</returns>
    public static T ReadStructure< T >( this BinaryReader br ) where T : struct
    {
        ReadOnlySpan< byte > data = br.ReadBytes( Unsafe.SizeOf< T >() );

        return MemoryMarshal.Read< T >( data );
    }

    /// <summary>
    /// Reads many structures from the current stream position.
    /// </summary>
    /// <param name="br"></param>
    /// <param name="count">The number of T to read from the stream</param>
    /// <typeparam name="T">The structure to read in to</typeparam>
    /// <returns>A list containing the structures read from the stream</returns>
    public static List< T > ReadStructures< T >( this BinaryReader br, int count ) where T : struct
    {
        var size = Marshal.SizeOf< T >();
        var data = br.ReadBytes( size * count );

        var list = new List< T >( count );

        for( var i = 0; i < count; i++ )
        {
            var offset = size * i;
            var span   = new ReadOnlySpan< byte >( data, offset, size );

            list.Add( MemoryMarshal.Read< T >( span ) );
        }

        return list;
    }

    /// <summary>
    /// Seeks this BinaryReader's position to the given offset. Syntactic sugar.
    /// </summary>
    public static void Seek( this BinaryReader br, long offset )
    {
        br.BaseStream.Position = offset;
    }
}