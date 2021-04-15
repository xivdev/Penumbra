using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Penumbra.Util
{
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

            for( int i = 0; i < count; i++ )
            {
                var offset = size * i;
                var span = new ReadOnlySpan< byte >( data, offset, size );

                list.Add( MemoryMarshal.Read< T >( span ) );
            }

            return list;
        }

        public static T[] ReadStructuresAsArray< T >( this BinaryReader br, int count ) where T : struct
        {
            var size = Marshal.SizeOf< T >();
            var data = br.ReadBytes( size * count );

            // im a pirate arr
            var arr = new T[ count ];

            for( int i = 0; i < count; i++ )
            {
                var offset = size * i;
                var span = new ReadOnlySpan< byte >( data, offset, size );

                arr[ i ] = MemoryMarshal.Read< T >( span );
            }

            return arr;
        }

        /// <summary>
        /// Moves the BinaryReader position to offset, reads a string, then
        /// sets the reader position back to where it was when it started
        /// </summary>
        /// <param name="br"></param>
        /// <param name="offset">The offset to read a string starting from.</param>
        /// <returns></returns>
        public static string ReadStringOffsetData( this BinaryReader br, long offset )
        {
            return Encoding.UTF8.GetString( ReadRawOffsetData( br, offset ) );
        }

        /// <summary>
        /// Moves the BinaryReader position to offset, reads raw bytes until a null byte, then
        /// sets the reader position back to where it was when it started
        /// </summary>
        /// <param name="br"></param>
        /// <param name="offset">The offset to read data starting from.</param>
        /// <returns></returns>
        public static byte[] ReadRawOffsetData( this BinaryReader br, long offset )
        {
            var originalPosition = br.BaseStream.Position;
            br.BaseStream.Position = offset;
            
            var chars = new List< byte >();
            
            byte current;
            while( ( current = br.ReadByte() ) != 0 )
            {
                chars.Add( current );
            }

            br.BaseStream.Position = originalPosition;

            return chars.ToArray();
        }

        /// <summary>
        /// Seeks this BinaryReader's position to the given offset. Syntactic sugar.
        /// </summary>
        public static void Seek( this BinaryReader br, long offset ) {
            br.BaseStream.Position = offset;
        }

        /// <summary>
        /// Reads a byte and moves the stream position back to where it started before the operation
        /// </summary>
        /// <param name="br">The reader to use to read the byte</param>
        /// <returns>The byte that was read</returns>
        public static byte PeekByte( this BinaryReader br )
        {
            var data = br.ReadByte();
            br.BaseStream.Position--;
            return data;
        }

        /// <summary>
        /// Reads bytes and moves the stream position back to where it started before the operation
        /// </summary>
        /// <param name="br">The reader to use to read the bytes</param>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>The read bytes</returns>
        public static byte[] PeekBytes( this BinaryReader br, int count )
        {
            var data = br.ReadBytes( count );
            br.BaseStream.Position -= count;
            return data;
        }
    }
}
