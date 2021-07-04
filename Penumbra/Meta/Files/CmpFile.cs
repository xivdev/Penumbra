using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.Game;
using Penumbra.Game.Enums;

namespace Penumbra.Meta.Files
{
    public class CmpFile
    {
        private const int RacialScalingStart = 0x2A800;

        private readonly byte[]     _byteData = new byte[RacialScalingStart];
        private readonly RspEntry[] _rspEntries;

        public CmpFile( byte[] bytes )
        {
            if( bytes.Length < RacialScalingStart )
            {
                throw new ArgumentOutOfRangeException();
            }

            Array.Copy( bytes, _byteData, RacialScalingStart );
            var rspEntryNum = ( bytes.Length - RacialScalingStart ) / RspEntry.ByteSize;
            var tmp         = new List< RspEntry >( rspEntryNum );
            for( var i = 0; i < rspEntryNum; ++i )
            {
                tmp.Add( new RspEntry( bytes, RacialScalingStart + i * RspEntry.ByteSize ) );
            }

            _rspEntries = tmp.ToArray();
        }

        public RspEntry this[ SubRace subRace ]
            => _rspEntries[ subRace.ToRspIndex() ];

        public bool Set( SubRace subRace, RspAttribute attribute, float value )
        {
            var entry    = _rspEntries[ subRace.ToRspIndex() ];
            var oldValue = entry[ attribute ];
            if( oldValue == value )
            {
                return false;
            }

            entry[ attribute ] = value;
            return true;
        }

        public byte[] WriteBytes()
        {
            using var s = new MemoryStream( RacialScalingStart + _rspEntries.Length * RspEntry.ByteSize );
            s.Write( _byteData, 0, _byteData.Length );
            foreach( var entry in _rspEntries )
            {
                var bytes = entry.ToBytes();
                s.Write( bytes, 0, bytes.Length );
            }

            return s.ToArray();
        }

        private CmpFile( byte[] data, RspEntry[] entries )
        {
            _byteData   = data.ToArray();
            _rspEntries = entries.Select( e => new RspEntry( e ) ).ToArray();
        }

        public CmpFile Clone()
            => new( _byteData, _rspEntries );
    }
}