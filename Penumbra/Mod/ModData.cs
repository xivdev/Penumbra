using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.Mods;

namespace Penumbra.Mod;

public struct SortOrder : IComparable< SortOrder >
{
    public ModFolder ParentFolder { get; set; }

    private string _sortOrderName;

    public string SortOrderName
    {
        get => _sortOrderName;
        set => _sortOrderName = value.Replace( '/', '\\' );
    }

    public string SortOrderPath
        => ParentFolder.FullName;

    public string FullName
    {
        get
        {
            var path = SortOrderPath;
            return path.Length > 0 ? $"{path}/{SortOrderName}" : SortOrderName;
        }
    }


    public SortOrder( ModFolder parentFolder, string name )
    {
        ParentFolder   = parentFolder;
        _sortOrderName = name.Replace( '/', '\\' );
    }

    public string FullPath
        => SortOrderPath.Length > 0 ? $"{SortOrderPath}/{SortOrderName}" : SortOrderName;

    public int CompareTo( SortOrder other )
        => string.Compare( FullPath, other.FullPath, StringComparison.InvariantCultureIgnoreCase );
}

// ModData contains all permanent information about a mod,
// and is independent of collections or settings.
// It only changes when the user actively changes the mod or their filesystem.
public class ModData
{
    public DirectoryInfo BasePath;
    public ModMeta       Meta;
    public ModResources  Resources;

    public SortOrder SortOrder;

    public SortedList< string, object? > ChangedItems { get; } = new();
    public string LowerChangedItemsString { get; private set; } = string.Empty;
    public FileInfo MetaFile { get; set; }

    private ModData( ModFolder parentFolder, DirectoryInfo basePath, ModMeta meta, ModResources resources )
    {
        BasePath  = basePath;
        Meta      = meta;
        Resources = resources;
        MetaFile  = MetaFileInfo( basePath );
        SortOrder = new SortOrder( parentFolder, Meta.Name );
        SortOrder.ParentFolder.AddMod( this );

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

    public static ModData? LoadMod( ModFolder parentFolder, DirectoryInfo basePath )
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

        return new ModData( parentFolder, basePath, meta, data );
    }

    public void SaveMeta()
        => Meta.SaveToFile( MetaFile );

    public override string ToString()
        => SortOrder.FullPath;
}