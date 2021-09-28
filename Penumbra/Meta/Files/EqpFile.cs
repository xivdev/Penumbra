using System;
using System.IO;
using System.Linq;
using Lumina.Data;
using Penumbra.GameData.Structs;

namespace Penumbra.Meta.Files
{
    // EQP Structure:
    // 64 x [Block collapsed or not bit]
    // 159 x [EquipmentParameter:ulong]
    // (CountSetBits(Block Collapsed or not) - 1) x 160 x [EquipmentParameter:ulong]
    // Item 0 does not exist and is sent to Item 1 instead.
    public sealed class EqpFile : EqpGmpBase
    {
        private readonly EqpEntry[]?[] _entries = new EqpEntry[TotalBlockCount][];

        protected override ulong ControlBlock
        {
            get => ( ulong )_entries[ 0 ]![ 0 ];
            set => _entries[ 0 ]![ 0 ] = ( EqpEntry )value;
        }

        private EqpFile( EqpFile clone )
        {
            ExpandedBlockCount = clone.ExpandedBlockCount;
            _entries           = clone.Clone( clone._entries );
        }

        public byte[] WriteBytes()
            => WriteBytes( _entries, e => ( ulong )e );

        public EqpFile Clone()
            => new( this );

        public EqpFile( FileResource file )
            => ReadFile( _entries, file, I => ( EqpEntry )I );

        public EqpEntry GetEntry( ushort setId )
            => GetEntry( _entries, setId, ( EqpEntry )0 );

        public bool SetEntry( ushort setId, EqpEntry entry )
            => SetEntry( _entries, setId, entry, e => e == 0, ( e1, e2 ) => e1 == e2 );

        public ref EqpEntry this[ ushort setId ]
            => ref GetTrueEntry( _entries, setId );
    }

    public class EqpGmpBase
    {
        protected const ushort ParameterSize   = 8;
        protected const ushort BlockSize       = 160;
        protected const ushort TotalBlockCount = 64;

        protected int ExpandedBlockCount { get; set; }

        private static int BlockIdx( ushort idx )
            => idx / BlockSize;

        private static int SubIdx( ushort idx )
            => idx % BlockSize;

        protected virtual ulong ControlBlock { get; set; }

        protected T[]?[] Clone< T >( T[]?[] clone )
        {
            var ret = new T[TotalBlockCount][];
            for( var i = 0; i < TotalBlockCount; ++i )
            {
                if( clone[ i ] != null )
                {
                    ret[ i ] = ( T[] )clone[ i ]!.Clone();
                }
            }

            return ret;
        }

        protected EqpGmpBase()
        { }

        protected bool ExpandBlock< T >( T[]?[] blocks, int idx )
        {
            if( idx >= TotalBlockCount || blocks[ idx ] != null )
            {
                return false;
            }

            blocks[ idx ] = new T[BlockSize];
            ++ExpandedBlockCount;
            ControlBlock |= 1ul << idx;
            return true;
        }

        protected bool CollapseBlock< T >( T[]?[] blocks, int idx )
        {
            if( idx >= TotalBlockCount || blocks[ idx ] == null )
            {
                return false;
            }

            blocks[ idx ] = null;
            --ExpandedBlockCount;
            ControlBlock &= ~( 1ul << idx );
            return true;
        }

        protected T GetEntry< T >( T[]?[] blocks, ushort idx, T defaultEntry )
        {
            // Skip the zeroth item.
            idx = idx == 0 ? ( ushort )1 : idx;
            var block = BlockIdx( idx );
            var array = block < blocks.Length ? blocks[ block ] : null;
            if( array == null )
            {
                return defaultEntry;
            }

            return array[ SubIdx( idx ) ];
        }

        protected ref T GetTrueEntry< T >( T[]?[] blocks, ushort idx )
        {
            // Skip the zeroth item.
            idx = idx == 0 ? ( ushort )1 : idx;
            var block = BlockIdx( idx );
            if( block >= TotalBlockCount )
            {
                throw new ArgumentOutOfRangeException();
            }

            ExpandBlock( blocks, block );
            var array = blocks[ block ]!;
            return ref array[ SubIdx( idx ) ];
        }

        protected byte[] WriteBytes< T >( T[]?[] blocks, Func< T, ulong > transform )
        {
            var       dataSize = ExpandedBlockCount * BlockSize * ParameterSize;
            using var mem      = new MemoryStream( dataSize );
            using var bw       = new BinaryWriter( mem );

            foreach( var parameter in blocks.Where( array => array != null )
               .SelectMany( array => array! ) )
            {
                bw.Write( transform( parameter ) );
            }

            return mem.ToArray();
        }

        protected void ReadFile< T >( T[]?[] blocks, FileResource file, Func< ulong, T > convert )
        {
            file.Reader.BaseStream.Seek( 0, SeekOrigin.Begin );
            var blockBits = file.Reader.ReadUInt64();
            // reset to 0 and just put the bitmask in the first block
            // item 0 is not accessible and it simplifies printing.
            file.Reader.BaseStream.Seek( 0, SeekOrigin.Begin );

            ExpandedBlockCount = 0;
            for( var i = 0; i < TotalBlockCount; ++i )
            {
                var flag = 1ul << i;
                if( ( blockBits & flag ) != flag )
                {
                    continue;
                }

                ++ExpandedBlockCount;

                var tmp = new T[BlockSize];
                for( var j = 0; j < BlockSize; ++j )
                {
                    tmp[ j ] = convert( file.Reader.ReadUInt64() );
                }

                blocks[ i ] = tmp;
            }
        }

        protected bool SetEntry< T >( T[]?[] blocks, ushort idx, T entry, Func< T, bool > isDefault, Func< T, T, bool > isEqual )
        {
            var block = BlockIdx( idx );
            if( block >= TotalBlockCount )
            {
                return false;
            }

            if( !isDefault( entry ) )
            {
                ExpandBlock( blocks, block );
                if( !isEqual( entry, blocks[ block ]![ SubIdx( idx ) ] ) )
                {
                    blocks[ block ]![ SubIdx( idx ) ] = entry;
                    return true;
                }
            }
            else
            {
                var array = blocks[ block ];
                if( array != null )
                {
                    array[ SubIdx( idx ) ] = entry;
                    if( array.All( e => e!.Equals( 0ul ) ) )
                    {
                        CollapseBlock( blocks, block );
                    }

                    return true;
                }
            }

            return false;
        }
    }
}