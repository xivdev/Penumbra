using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    public struct MetaManagerEqdp : IDisposable
    {
        public ExpandedEqdpFile?[] Files = new ExpandedEqdpFile?[CharacterUtility.NumEqdpFiles];

        public readonly Dictionary< EqdpManipulation, Mod.Mod > Manipulations = new();

        public MetaManagerEqdp()
        { }

        [Conditional( "USE_EQDP" )]
        public void SetFiles()
        {
            foreach( var idx in CharacterUtility.EqdpIndices )
            {
                SetFile( Files[ idx - CharacterUtility.EqdpStartIdx ], idx );
            }
        }

        [Conditional( "USE_EQDP" )]
        public void Reset()
        {
            foreach( var file in Files )
            {
                file?.Reset( Manipulations.Keys.Where( m => m.FileIndex() == file.Index ).Select( m => ( int )m.SetId ) );
            }

            Manipulations.Clear();
        }

        public bool ApplyMod( EqdpManipulation m, Mod.Mod mod )
        {
#if USE_EQDP
            if( !Manipulations.TryAdd( m, mod ) )
            {
                return false;
            }

            var file = Files[ m.FileIndex() - 2 ] ??= new ExpandedEqdpFile( Names.CombinedRace( m.Gender, m.Race ), m.Slot.IsAccessory() );
            return m.Apply( file );
#else
            return false;
#endif
        }

        public ExpandedEqdpFile? File( GenderRace race, bool accessory )
            => Files[ CharacterUtility.EqdpIdx( race, accessory ) - CharacterUtility.EqdpStartIdx ];

        public void Dispose()
        {
            for( var i = 0; i < Files.Length; ++i )
            {
                Files[ i ]?.Dispose();
                Files[ i ] = null;
            }

            Manipulations.Clear();
        }
    }
}