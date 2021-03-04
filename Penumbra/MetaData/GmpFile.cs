using Lumina.Data;
using Penumbra.Game;

namespace Penumbra.MetaData
{
    // GmpFiles use the same structure as Eqp Files.
    // Entries are also one ulong.
    public sealed class GmpFile : EqpGmpBase
    {
        private readonly GmpEntry[]?[] _entries = new GmpEntry[TotalBlockCount][];

        protected override ulong ControlBlock
        {
            get => _entries[ 0 ]![ 0 ];
            set => _entries[ 0 ]![ 0 ] = ( GmpEntry )value;
        }

        private GmpFile( GmpFile clone )
        {
            ExpandedBlockCount = clone.ExpandedBlockCount;
            _entries           = clone.Clone( clone._entries );
        }

        public byte[] WriteBytes()
            => WriteBytes( _entries, E => ( ulong )E );

        public GmpFile Clone()
            => new( this );

        public GmpFile( FileResource file )
            => ReadFile( _entries, file, I => ( GmpEntry )I );

        public GmpEntry GetEntry( ushort setId )
            => GetEntry( _entries, setId, ( GmpEntry )0 );

        public bool SetEntry( ushort setId, GmpEntry entry )
            => SetEntry( _entries, setId, entry, E => E == 0, ( E1, E2 ) => E1 == E2 );

        public ref GmpEntry this[ ushort setId ]
            => ref GetTrueEntry( _entries, setId );
    }
}