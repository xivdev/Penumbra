using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Util;

namespace Penumbra.Mods;

public partial class Mod
{
    public partial class Editor : IDisposable
    {
        private readonly Mod _mod;

        public Editor( Mod mod )
        {
            _mod            = mod;
            _availableFiles = GetAvailablePaths( mod );
            _usedPaths      = new SortedSet< FullPath >( mod.AllFiles );
            _missingPaths   = new SortedSet< FullPath >( UsedPaths.Where( f => !f.Exists ) );
            _unusedFiles    = new SortedSet< FullPath >( AvailableFiles.Where( p => !UsedPaths.Contains( p.Item1 ) ).Select( p => p.Item1 ) );
            _subMod         = _mod._default;
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