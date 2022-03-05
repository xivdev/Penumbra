using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Meta;

namespace Penumbra.Mod;

[Flags]
public enum ResourceChange
{
    None  = 0,
    Files = 1,
    Meta  = 2,
}

// Contains static mod data that should only change on filesystem changes.
public class ModResources
{
    public List< FullPath > ModFiles { get; private set; } = new();
    public List< FullPath > MetaFiles { get; private set; } = new();

    public MetaCollection MetaManipulations { get; private set; } = new();


    private void ForceManipulationsUpdate( ModMeta meta, DirectoryInfo basePath )
    {
        MetaManipulations.Update( MetaFiles, basePath, meta );
        MetaManipulations.SaveToFile( MetaCollection.FileName( basePath ) );
    }

    public void SetManipulations( ModMeta meta, DirectoryInfo basePath, bool validate = true )
    {
        var newManipulations = MetaCollection.LoadFromFile( MetaCollection.FileName( basePath ) );
        if( newManipulations == null )
        {
            ForceManipulationsUpdate( meta, basePath );
        }
        else
        {
            MetaManipulations = newManipulations;
            if( validate && !MetaManipulations.Validate( meta ) )
            {
                ForceManipulationsUpdate( meta, basePath );
            }
        }
    }

    // Update the current set of files used by the mod,
    // returns true if anything changed.
    public ResourceChange RefreshModFiles( DirectoryInfo basePath )
    {
        List< FullPath > tmpFiles = new(ModFiles.Count);
        List< FullPath > tmpMetas = new(MetaFiles.Count);
        // we don't care about any _files_ in the root dir, but any folders should be a game folder/file combo
        foreach( var file in basePath.EnumerateDirectories()
                   .SelectMany( dir => dir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
                   .Select( f => new FullPath( f ) )
                   .OrderBy( f => f.FullName ) )
        {
            switch( file.Extension.ToLowerInvariant() )
            {
                case ".meta":
                case ".rgsp":
                    tmpMetas.Add( file );
                    break;
                default:
                    tmpFiles.Add( file );
                    break;
            }
        }

        ResourceChange changes = 0;
        if( !tmpFiles.Select( f => f.FullName ).SequenceEqual( ModFiles.Select( f => f.FullName ) ) )
        {
            ModFiles =  tmpFiles;
            changes  |= ResourceChange.Files;
        }

        if( !tmpMetas.Select( f => f.FullName ).SequenceEqual( MetaFiles.Select( f => f.FullName ) ) )
        {
            MetaFiles =  tmpMetas;
            changes   |= ResourceChange.Meta;
        }

        return changes;
    }
}