using System.Collections.Generic;
using System.IO;
using Penumbra.GameData.Util;
using Penumbra.Util;

namespace Penumbra.Mod
{
    // A complete Mod containing settings (i.e. dependent on a collection)
    // and the resulting cache.
    public class Mod
    {
        public ModSettings Settings { get; }
        public ModData Data { get; }
        public ModCache Cache { get; }

        public Mod( ModSettings settings, ModData data )
        {
            Settings = settings;
            Data     = data;
            Cache    = new ModCache();
        }

        public bool FixSettings()
            => Settings.FixInvalidSettings( Data.Meta );

        public HashSet< GamePath > GetFiles( FileInfo file )
        {
            var relPath = new RelPath( file, Data.BasePath );
            return ModFunctions.GetFilesForConfig( relPath, Settings, Data.Meta );
        }

        public override string ToString()
            => Data.Meta.Name;
    }
}