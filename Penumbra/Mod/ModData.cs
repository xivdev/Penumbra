using System.IO;
using Dalamud.Plugin;

namespace Penumbra.Mod
{
    public class ModData
    {
        public DirectoryInfo BasePath;
        public ModMeta       Meta;
        public ModResources  Resources;

        public FileInfo MetaFile { get; set; }

        private ModData( DirectoryInfo basePath, ModMeta meta, ModResources resources )
        {
            BasePath  = basePath;
            Meta      = meta;
            Resources = resources;
            MetaFile  = MetaFileInfo( basePath );
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