using System;
using System.IO;
using System.Linq;
using Lumina.Data;
using Penumbra.Game;

namespace Penumbra.Meta.Files
{
    // EQDP file structure:
    // [Identifier][BlockSize:ushort][BlockCount:ushort]
    //   BlockCount x [BlockHeader:ushort]
    //   Containing offsets for blocks, ushort.Max means collapsed.
    //   Offsets are based on the end of the header, so 0 means IdentifierSize + 4 + BlockCount x 2.
    //     ExpandedBlockCount x [Entry]
    public class EqdpFile
    {
        private const ushort BlockHeaderSize = 2;
        private const ushort PreambleSize    = 4;
        private const ushort CollapsedBlock  = ushort.MaxValue;
        private const ushort IdentifierSize  = 2;
        private const ushort EqdpEntrySize   = 2;
        private const int    FileAlignment   = 1 << 9;

        private EqdpFile( EqdpFile clone )
        {
            Identifier         = clone.Identifier;
            BlockSize          = clone.BlockSize;
            TotalBlockCount    = clone.TotalBlockCount;
            ExpandedBlockCount = clone.ExpandedBlockCount;
            Blocks             = new EqdpEntry[clone.TotalBlockCount][];
            for( var i = 0; i < TotalBlockCount; ++i )
            {
                if( clone.Blocks[ i ] != null )
                {
                    Blocks[ i ] = ( EqdpEntry[] )clone.Blocks[ i ]!.Clone();
                }
            }
        }

        public ref EqdpEntry this[ ushort setId ]
            => ref GetTrueEntry( setId );


        public EqdpFile Clone()
            => new( this );

        private ushort Identifier { get; }
        private ushort BlockSize { get; }
        private ushort TotalBlockCount { get; }
        private ushort ExpandedBlockCount { get; set; }
        private EqdpEntry[]?[] Blocks { get; }

        private int BlockIdx( ushort id )
            => ( ushort )( id / BlockSize );

        private int SubIdx( ushort id )
            => ( ushort )( id % BlockSize );

        private bool ExpandBlock( int idx )
        {
            if( idx < TotalBlockCount && Blocks[ idx ] == null )
            {
                Blocks[ idx ] = new EqdpEntry[BlockSize];
                ++ExpandedBlockCount;
                return true;
            }

            return false;
        }

        private bool CollapseBlock( int idx )
        {
            if( idx >= TotalBlockCount || Blocks[ idx ] == null )
            {
                return false;
            }

            Blocks[ idx ] = null;
            --ExpandedBlockCount;
            return true;
        }

        public bool SetEntry( ushort idx, EqdpEntry entry )
        {
            var block = BlockIdx( idx );
            if( block >= TotalBlockCount )
            {
                return false;
            }

            if( entry != 0 )
            {
                ExpandBlock( block );
                if( Blocks[ block ]![ SubIdx( idx ) ] != entry )
                {
                    Blocks[ block ]![ SubIdx( idx ) ] = entry;
                    return true;
                }
            }
            else
            {
                var array = Blocks[ block ];
                if( array != null )
                {
                    array[ SubIdx( idx ) ] = entry;
                    if( array.All( e => e == 0 ) )
                    {
                        CollapseBlock( block );
                    }

                    return true;
                }
            }

            return false;
        }

        public EqdpEntry GetEntry( ushort idx )
        {
            var block = BlockIdx( idx );
            var array = block < Blocks.Length ? Blocks[ block ] : null;
            return array?[ SubIdx( idx ) ] ?? 0;
        }

        private ref EqdpEntry GetTrueEntry( ushort idx )
        {
            var block = BlockIdx( idx );
            if( block >= TotalBlockCount )
            {
                throw new ArgumentOutOfRangeException();
            }

            ExpandBlock( block );
            var array = Blocks[ block ]!;
            return ref array[ SubIdx( idx ) ];
        }

        private void WriteHeaders( BinaryWriter bw )
        {
            ushort offset = 0;
            foreach( var block in Blocks )
            {
                if( block == null )
                {
                    bw.Write( CollapsedBlock );
                    continue;
                }

                bw.Write( offset );
                offset += BlockSize;
            }
        }

        private static void WritePadding( BinaryWriter bw, int paddingSize )
        {
            var buffer = new byte[paddingSize];
            bw.Write( buffer, 0, paddingSize );
        }

        private void WriteBlocks( BinaryWriter bw )
        {
            foreach( var entry in Blocks.Where( block => block != null )
               .SelectMany( block => block ) )
            {
                bw.Write( ( ushort )entry );
            }
        }

        public byte[] WriteBytes()
        {
            var dataSize = PreambleSize + IdentifierSize + BlockHeaderSize * TotalBlockCount + ExpandedBlockCount * BlockSize * EqdpEntrySize;
            var paddingSize = FileAlignment - ( dataSize & ( FileAlignment - 1 ) );
            using var mem =
                new MemoryStream( dataSize + paddingSize );
            using var bw = new BinaryWriter( mem );
            bw.Write( Identifier );
            bw.Write( BlockSize );
            bw.Write( TotalBlockCount );

            WriteHeaders( bw );
            WriteBlocks( bw );
            WritePadding( bw, paddingSize );

            return mem.ToArray();
        }

        public EqdpFile( FileResource file )
        {
            file.Reader.BaseStream.Seek( 0, SeekOrigin.Begin );

            Identifier         = file.Reader.ReadUInt16();
            BlockSize          = file.Reader.ReadUInt16();
            TotalBlockCount    = file.Reader.ReadUInt16();
            Blocks             = new EqdpEntry[TotalBlockCount][];
            ExpandedBlockCount = 0;
            for( var i = 0; i < TotalBlockCount; ++i )
            {
                var offset = file.Reader.ReadUInt16();
                if( offset != CollapsedBlock )
                {
                    ExpandBlock( ( ushort )i );
                }
            }

            foreach( var array in Blocks.Where( array => array != null ) )
            {
                for( var i = 0; i < BlockSize; ++i )
                {
                    array![ i ] = ( EqdpEntry )file.Reader.ReadUInt16();
                }
            }
        }
    }
}