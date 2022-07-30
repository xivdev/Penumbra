using System;
using System.IO;
using OtterGui;

namespace Penumbra.Mods;

public partial class Mod : IMod
{
    public partial class Editor : IDisposable
    {
        private readonly Mod _mod;

        public Editor( Mod mod, ISubMod? option )
        {
            _mod    = mod;
            _subMod = null!;
            SetSubMod( option );
            UpdateFiles();
            ScanModels();
        }

        public void Cancel()
        {
            DuplicatesFinished = true;
        }

        public void Dispose()
            => Cancel();

        // Does not delete the base directory itself even if it is completely empty at the end.
        private static void ClearEmptySubDirectories( DirectoryInfo baseDir )
        {
            foreach( var subDir in baseDir.GetDirectories() )
            {
                ClearEmptySubDirectories( subDir );
                if( subDir.GetFiles().Length == 0 && subDir.GetDirectories().Length == 0 )
                {
                    subDir.Delete();
                }
            }
        }

        // Apply a option action to all available option in a mod, including the default option.
        private static void ApplyToAllOptions( Mod mod, Action< ISubMod, int, int > action )
        {
            action( mod.Default, -1, 0 );
            foreach( var (group, groupIdx) in mod.Groups.WithIndex() )
            {
                for( var optionIdx = 0; optionIdx < group.Count; ++optionIdx )
                {
                    action( group[ optionIdx ], groupIdx, optionIdx );
                }
            }
        }
    }
}