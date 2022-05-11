using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Util;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    public struct MetaManagerEqdp : IDisposable
    {
        public ExpandedEqdpFile?[] Files = new ExpandedEqdpFile?[CharacterUtility.NumEqdpFiles - 2]; // TODO: female Hrothgar

        public readonly Dictionary< EqdpManipulation, int > Manipulations = new();

        public MetaManagerEqdp()
        { }

        [Conditional( "USE_EQDP" )]
        public void SetFiles()
        {
            for( var i = 0; i < CharacterUtility.EqdpIndices.Length; ++i )
            {
                SetFile( Files[ i ], CharacterUtility.EqdpIndices[ i ] );
            }
        }

        [Conditional( "USE_EQDP" )]
        public static void ResetFiles()
        {
            foreach( var idx in CharacterUtility.EqdpIndices )
            {
                SetFile( null, idx );
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

        public bool ApplyMod( EqdpManipulation m, int modIdx )
        {
#if USE_EQDP
            Manipulations[ m ] = modIdx;
            var file = Files[ Array.IndexOf( CharacterUtility.EqdpIndices, m.FileIndex() ) ] ??=
                new ExpandedEqdpFile( Names.CombinedRace( m.Gender, m.Race ), m.Slot.IsAccessory() ); // TODO: female Hrothgar
            return m.Apply( file );
#else
            return false;
#endif
        }

        public ExpandedEqdpFile? File( GenderRace race, bool accessory )
            => Files[ Array.IndexOf( CharacterUtility.EqdpIndices, CharacterUtility.EqdpIdx( race, accessory ) ) ]; // TODO: female Hrothgar

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