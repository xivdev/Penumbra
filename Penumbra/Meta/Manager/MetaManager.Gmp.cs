using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    public struct MetaManagerGmp : IDisposable
    {
        public          ExpandedGmpFile?                   File          = null;
        public readonly Dictionary< GmpManipulation, int > Manipulations = new();

        public MetaManagerGmp()
        { }


        [Conditional( "USE_GMP" )]
        public void SetFiles()
            => SetFile( File, CharacterUtility.GmpIdx );

        [Conditional( "USE_GMP" )]
        public static void ResetFiles()
            => SetFile( null, CharacterUtility.GmpIdx );

        [Conditional( "USE_GMP" )]
        public void Reset()
        {
            if( File != null )
            {
                File.Reset( Manipulations.Keys.Select( m => ( int )m.SetId ) );
                Manipulations.Clear();
            }
        }

        public bool ApplyMod( GmpManipulation m, int modIdx )
        {
#if USE_GMP
            Manipulations[ m ] =   modIdx;
            File               ??= new ExpandedGmpFile();
            return m.Apply( File );
#else
            return false;
#endif
        }

        public void Dispose()
        {
            File?.Dispose();
            File = null;
            Manipulations.Clear();
        }
    }
}