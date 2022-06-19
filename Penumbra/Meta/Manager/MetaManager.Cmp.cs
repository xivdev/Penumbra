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
    public struct MetaManagerCmp : IDisposable
    {
        public          CmpFile?                           File          = null;
        public readonly Dictionary< RspManipulation, IMod > Manipulations = new();

        public MetaManagerCmp()
        { }

        [Conditional( "USE_CMP" )]
        public void SetFiles()
            => SetFile( File, CharacterUtility.HumanCmpIdx );

        [Conditional( "USE_CMP" )]
        public static void ResetFiles()
            => SetFile( null, CharacterUtility.HumanCmpIdx );

        [Conditional( "USE_CMP" )]
        public void Reset()
        {
            if( File == null )
            {
                return;
            }

            File.Reset( Manipulations.Keys.Select( m => ( m.SubRace, m.Attribute ) ) );
            Manipulations.Clear();
        }

        public bool ApplyMod( RspManipulation m, IMod mod )
        {
#if USE_CMP
            Manipulations[ m ] =   mod;
            File               ??= new CmpFile();
            return m.Apply( File );
#else
            return false;
#endif
        }

        public bool RevertMod( RspManipulation m )
        {
#if USE_CMP
            if( Manipulations.Remove( m ) )
            {
                var def   = CmpFile.GetDefault( m.SubRace, m.Attribute );
                var manip = new RspManipulation( m.SubRace, m.Attribute, def );
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