using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    public struct MetaManagerGmp : IDisposable
    {
        public          ExpandedGmpFile?                   File          = null;
        public readonly Dictionary< GmpManipulation, Mod > Manipulations = new();

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

        public bool ApplyMod( GmpManipulation m, Mod mod )
        {
#if USE_GMP
            Manipulations[ m ] =   mod;
            File               ??= new ExpandedGmpFile();
            return m.Apply( File );
#else
            return false;
#endif
        }

        public bool RevertMod( GmpManipulation m )
        {
#if USE_GMP
            if( Manipulations.Remove( m ) )
            {
                var def   = ExpandedGmpFile.GetDefault( m.SetId );
                var manip = new GmpManipulation( def, m.SetId );
                return manip.Apply( File! );
            }
#endif
            return false;
        }

        public void Dispose()
        {
            File?.Dispose();
            File = null;
            Manipulations.Clear();
        }
    }
}