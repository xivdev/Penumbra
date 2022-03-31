using System.IO;
using Dalamud.Logging;

namespace Penumbra.Mods;

public enum ModPathChangeType
{
    Added,
    Deleted,
    Moved,
}

public partial class Mod2
{
    public DirectoryInfo BasePath { get; private set; }
    public int Index { get; private set; } = -1;

    private FileInfo MetaFile
        => new(Path.Combine( BasePath.FullName, "meta.json" ));

    private Mod2( ModFolder parentFolder, DirectoryInfo basePath )
    {
        BasePath = basePath;
        Order    = new Mod.SortOrder( parentFolder, Name );
        //Order.ParentFolder.AddMod( this ); // TODO
        ComputeChangedItems();
    }

    public static Mod2? LoadMod( ModFolder parentFolder, DirectoryInfo basePath )
    {
        basePath.Refresh();
        if( !basePath.Exists )
        {
            PluginLog.Error( $"Supplied mod directory {basePath} does not exist." );
            return null;
        }

        var mod = new Mod2( parentFolder, basePath );

        var metaFile = mod.MetaFile;
        if( !metaFile.Exists )
        {
            PluginLog.Debug( "No mod meta found for {ModLocation}.", basePath.Name );
            return null;
        }

        mod.LoadMetaFromFile( metaFile );
        if( mod.Name.Length == 0 )
        {
            PluginLog.Error( $"Mod at {basePath} without name is not supported." );
        }

        mod.ReloadFiles();
        return mod;
    }
}