using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.Mods;

namespace Penumbra.Mod;

// Mod contains all permanent information about a mod,
// and is independent of collections or settings.
// It only changes when the user actively changes the mod or their filesystem.
public partial class Mod
{
    public DirectoryInfo BasePath;
    public ModMeta       Meta;
    public ModResources  Resources;

    public SortOrder Order;

    public SortedList< string, object? > ChangedItems { get; } = new();
    public string LowerChangedItemsString { get; private set; } = string.Empty;
    public FileInfo MetaFile { get; set; }
    public int Index { get; private set; } = -1;

    private Mod( ModFolder parentFolder, DirectoryInfo basePath, ModMeta meta, ModResources resources )
    {
        BasePath  = basePath;
        Meta      = meta;
        Resources = resources;
        MetaFile  = MetaFileInfo( basePath );
        Order     = new SortOrder( parentFolder, Meta.Name );
        Order.ParentFolder.AddMod( this );
        ComputeChangedItems();
    }

    public void ComputeChangedItems()
    {
        var identifier = GameData.GameData.GetIdentifier();
        ChangedItems.Clear();
        foreach( var file in Resources.ModFiles.Select( f => f.ToRelPath( BasePath, out var p ) ? p : Utf8RelPath.Empty ) )
        {
            foreach( var path in ModFunctions.GetAllFiles( file, Meta ) )
            {
                identifier.Identify( ChangedItems, path.ToGamePath() );
            }
        }

        foreach( var path in Meta.FileSwaps.Keys )
        {
            identifier.Identify( ChangedItems, path.ToGamePath() );
        }

        LowerChangedItemsString = string.Join( "\0", ChangedItems.Keys.Select( k => k.ToLowerInvariant() ) );
    }

    public static FileInfo MetaFileInfo( DirectoryInfo basePath )
        => new(Path.Combine( basePath.FullName, "meta.json" ));

    public static Mod? LoadMod( ModFolder parentFolder, DirectoryInfo basePath )
    {
        basePath.Refresh();
        if( !basePath.Exists )
        {
            PluginLog.Error( $"Supplied mod directory {basePath} does not exist." );
            return null;
        }

        var metaFile = MetaFileInfo( basePath );
        if( !metaFile.Exists )
        {
            PluginLog.Debug( "No mod meta found for {ModLocation}.", basePath.Name );
            return null;
        }

        var meta = ModMeta.LoadFromFile( metaFile );
        if( meta == null )
        {
            return null;
        }

        var data = new ModResources();
        if( data.RefreshModFiles( basePath ).HasFlag( ResourceChange.Meta ) )
        {
            data.SetManipulations( meta, basePath );
        }

        return new Mod( parentFolder, basePath, meta, data );
    }

    public void SaveMeta()
        => Meta.SaveToFile( MetaFile );

    public override string ToString()
        => Order.FullPath;
}