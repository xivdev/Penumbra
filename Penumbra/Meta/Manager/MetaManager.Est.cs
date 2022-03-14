using System;
using System.Collections.Generic;
using System.Diagnostics;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    public struct MetaManagerEst : IDisposable
    {
        public EstFile? FaceFile = null;
        public EstFile? HairFile = null;
        public EstFile? BodyFile = null;
        public EstFile? HeadFile = null;

        public readonly Dictionary< EstManipulation, Mod.Mod > Manipulations = new();

        public MetaManagerEst()
        { }

        [Conditional( "USE_EST" )]
        public void SetFiles()
        {
            SetFile( FaceFile, CharacterUtility.FaceEstIdx );
            SetFile( HairFile, CharacterUtility.HairEstIdx );
            SetFile( BodyFile, CharacterUtility.BodyEstIdx );
            SetFile( HeadFile, CharacterUtility.HeadEstIdx );
        }

        [Conditional( "USE_EST" )]
        public void Reset()
        {
            FaceFile?.Reset();
            HairFile?.Reset();
            BodyFile?.Reset();
            HeadFile?.Reset();
            Manipulations.Clear();
        }

        public bool ApplyMod( EstManipulation m, Mod.Mod mod )
        {
#if USE_EST
            if( !Manipulations.TryAdd( m, mod ) )
            {
                return false;
            }

            var file = m.Slot switch
            {
                EstManipulation.EstType.Hair => HairFile ??= new EstFile( EstManipulation.EstType.Hair ),
                EstManipulation.EstType.Face => FaceFile ??= new EstFile( EstManipulation.EstType.Face ),
                EstManipulation.EstType.Body => BodyFile ??= new EstFile( EstManipulation.EstType.Body ),
                EstManipulation.EstType.Head => HeadFile ??= new EstFile( EstManipulation.EstType.Head ),
                _                            => throw new ArgumentOutOfRangeException(),
            };
            return m.Apply( file );
#else
            return false;
#endif
        }

        public void Dispose()
        {
            FaceFile?.Dispose();
            HairFile?.Dispose();
            BodyFile?.Dispose();
            HeadFile?.Dispose();
            FaceFile = null;
            HairFile = null;
            BodyFile = null;
            HeadFile = null;
            Manipulations.Clear();
        }
    }
}