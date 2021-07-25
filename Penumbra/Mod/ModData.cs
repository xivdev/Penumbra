using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Penumbra.GameData;
using Penumbra.Util;

namespace Penumbra.Mod
{
    // ModData contains all permanent information about a mod,
    // and is independent of collections or settings.
    // It only changes when the user actively changes the mod or their filesystem.
    public class ModData
    {
        public DirectoryInfo BasePath;
        public ModMeta       Meta;
        public ModResources  Resources;
        public string        SortOrder;
        public SortedList< string, object? > ChangedItems { get; } = new();
        public FileInfo MetaFile { get; set; }

        private ModData( DirectoryInfo basePath, ModMeta meta, ModResources resources )
        {
            BasePath  = basePath;
            Meta      = meta;
            Resources = resources;
            MetaFile  = MetaFileInfo( basePath );
            SortOrder = meta.Name.Replace( '/', '\\' );
            ComputeChangedItems();
        }

        public void ComputeChangedItems()
        {
            ChangedItems.Clear();
            foreach( var file in Resources.ModFiles.Select( f => new RelPath( f, BasePath ) ) )
            {
                foreach( var path in ModFunctions.GetAllFiles( file, Meta ) )
                {
                    ObjectIdentifier.Identify( ChangedItems, path );
                }
            }

            foreach( var path in Meta.FileSwaps.Keys )
            {
                ObjectIdentifier.Identify( ChangedItems, path );
            }
        }

        public static FileInfo MetaFileInfo( DirectoryInfo basePath )
            => new( Path.Combine( basePath.FullName, "meta.json" ) );

        public static ModData? LoadMod( DirectoryInfo basePath )
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

            return new ModData( basePath, meta, data );
        }

        public void SaveMeta()
            => Meta.SaveToFile( MetaFile );
    }
}