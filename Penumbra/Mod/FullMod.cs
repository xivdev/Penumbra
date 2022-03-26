using System.Collections.Generic;
using System.IO;
using Penumbra.GameData.ByteString;

namespace Penumbra.Mod;

// A complete Mod containing settings (i.e. dependent on a collection)
// and the resulting cache.
public class FullMod
{
    public ModSettings Settings { get; }
    public Mod Data { get; }

    public FullMod( ModSettings settings, Mod data )
    {
        Settings = settings;
        Data     = data;
    }

    public bool FixSettings()
        => Settings.FixInvalidSettings( Data.Meta );

    public HashSet< Utf8GamePath > GetFiles( FileInfo file )
    {
        var relPath = Utf8RelPath.FromFile( file, Data.BasePath, out var p ) ? p : Utf8RelPath.Empty;
        return ModFunctions.GetFilesForConfig( relPath, Settings, Data.Meta );
    }

    public override string ToString()
        => Data.Meta.Name;
}