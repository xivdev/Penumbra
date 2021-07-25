using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumina.Data;
using Penumbra.GameData.Enums;

namespace Penumbra.Meta.Files
{
    // EST Structure:
    // 1x [NumEntries : UInt32]
    // #NumEntries x [SetId : UInt16] [RaceId : UInt16]
    // #NumEntries x [SkeletonId : UInt16]
    public class EstFile
    {
        private const ushort EntryDescSize = 4;
        private const ushort EntrySize     = 2;

        private readonly Dictionary< GenderRace, Dictionary< ushort, ushort > > _entries = new();
        private uint NumEntries { get; set; }

        private EstFile( EstFile clone )
        {
            NumEntries = clone.NumEntries;
            _entries   = new Dictionary< GenderRace, Dictionary< ushort, ushort > >( clone._entries.Count );
            foreach( var kvp in clone._entries )
            {
                var dict = kvp.Value.ToDictionary( k => k.Key, k => k.Value );
                _entries.Add( kvp.Key, dict );
            }
        }

        public EstFile Clone()
            => new( this );

        private bool DeleteEntry( GenderRace gr, ushort setId )
        {
            if( !_entries.TryGetValue( gr, out var setDict ) )
            {
                return false;
            }

            if( !setDict.ContainsKey( setId ) )
            {
                return false;
            }

            setDict.Remove( setId );
            if( setDict.Count == 0 )
            {
                _entries.Remove( gr );
            }

            --NumEntries;
            return true;
        }

        private (bool, bool) AddEntry( GenderRace gr, ushort setId, ushort entry )
        {
            if( !_entries.TryGetValue( gr, out var setDict ) )
            {
                _entries[ gr ] = new Dictionary< ushort, ushort >();
                setDict        = _entries[ gr ];
            }

            if( setDict.TryGetValue( setId, out var oldEntry ) )
            {
                if( oldEntry == entry )
                {
                    return ( false, false );
                }

                setDict[ setId ] = entry;
                return ( false, true );
            }

            setDict[ setId ] = entry;
            return ( true, true );
        }

        public bool SetEntry( GenderRace gr, ushort setId, ushort entry )
        {
            if( entry == 0 )
            {
                return DeleteEntry( gr, setId );
            }

            var (addedNew, changed) = AddEntry( gr, setId, entry );
            if( !addedNew )
            {
                return changed;
            }

            ++NumEntries;
            return true;
        }

        public ushort GetEntry( GenderRace gr, ushort setId )
        {
            if( !_entries.TryGetValue( gr, out var setDict ) )
            {
                return 0;
            }

            return !setDict.TryGetValue( setId, out var entry ) ? ( ushort )0 : entry;
        }

        public byte[] WriteBytes()
        {
            using MemoryStream mem = new( ( int )( 4 + ( EntryDescSize + EntrySize ) * NumEntries ) );
            using BinaryWriter bw  = new( mem );

            bw.Write( NumEntries );
            foreach( var kvp1 in _entries )
            {
                foreach( var kvp2 in kvp1.Value )
                {
                    bw.Write( kvp2.Key );
                    bw.Write( ( ushort )kvp1.Key );
                }
            }

            foreach( var kvp2 in _entries.SelectMany( kvp1 => kvp1.Value ) )
            {
                bw.Write( kvp2.Value );
            }

            return mem.ToArray();
        }


        public EstFile( FileResource file )
        {
            file.Reader.BaseStream.Seek( 0, SeekOrigin.Begin );
            NumEntries = file.Reader.ReadUInt32();

            var currentEntryDescOffset = 4;
            var currentEntryOffset     = 4 + EntryDescSize * NumEntries;
            for( var i = 0; i < NumEntries; ++i )
            {
                file.Reader.BaseStream.Seek( currentEntryDescOffset, SeekOrigin.Begin );
                currentEntryDescOffset += EntryDescSize;
                var setId  = file.Reader.ReadUInt16();
                var raceId = ( GenderRace )file.Reader.ReadUInt16();
                if( !raceId.IsValid() )
                {
                    continue;
                }

                file.Reader.BaseStream.Seek( currentEntryOffset, SeekOrigin.Begin );
                currentEntryOffset += EntrySize;
                var entry = file.Reader.ReadUInt16();

                AddEntry( raceId, setId, entry );
            }
        }
    }
}