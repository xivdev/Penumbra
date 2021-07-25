using Lumina.Data;
using Penumbra.GameData.Structs;

namespace Penumbra.Meta.Files
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
            => WriteBytes( _entries, e => ( ulong )e );

        public GmpFile Clone()
            => new( this );

        public GmpFile( FileResource file )
            => ReadFile( _entries, file, i => ( GmpEntry )i );

        public GmpEntry GetEntry( ushort setId )
            => GetEntry( _entries, setId, ( GmpEntry )0 );

        public bool SetEntry( ushort setId, GmpEntry entry )
            => SetEntry( _entries, setId, entry, e => e == 0, ( e1, e2 ) => e1 == e2 );

        public ref GmpEntry this[ ushort setId ]
            => ref GetTrueEntry( _entries, setId );
    }
}