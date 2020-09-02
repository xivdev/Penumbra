using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Penumbra.Models;
using Newtonsoft.Json;

namespace Penumbra
{
    public class ModManager
    {
        public DirectoryInfo BasePath { get; set; }

        public readonly Dictionary< string, ResourceMod > AvailableMods = new Dictionary< string, ResourceMod >();

        public readonly Dictionary< string, FileInfo > ResolvedFiles = new Dictionary< string, FileInfo >();

        public ModManager( DirectoryInfo basePath )
        {
            BasePath = basePath;
        }

        public ModManager()
        {
        }

        public void DiscoverMods()
        {
            if( BasePath == null )
            {
                return;
            }

            if( !BasePath.Exists )
            {
                return;
            }

            AvailableMods.Clear();
            ResolvedFiles.Clear();

            // get all mod dirs
            foreach( var modDir in BasePath.EnumerateDirectories() )
            {
                var metaFile = modDir.EnumerateFiles().FirstOrDefault( f => f.Name == "meta.json" );

                if( metaFile == null )
                {
                    PluginLog.LogError( "mod meta is missing for resource mod: {ResourceModLocation}", modDir );
                    continue;
                }

                var meta = JsonConvert.DeserializeObject< Models.ModMeta >( File.ReadAllText( metaFile.FullName ) );

                var mod = new ResourceMod
                {
                    Meta = meta,
                    ModBasePath = modDir
                };

                AvailableMods[ modDir.Name ] = mod;
                mod.RefreshModFiles();
            }

            // todo: sort the mods by priority here so that the file discovery works correctly

            foreach( var mod in AvailableMods.Select( m => m.Value ) )
            {
                // fixup path
                var baseDir = mod.ModBasePath.FullName;

                foreach( var file in mod.ModFiles )
                {
                    var path = file.FullName.Substring( baseDir.Length ).ToLowerInvariant()
                        .TrimStart( '\\' ).Replace( '\\', '/' );

                    // todo: notify when collisions happen? or some extra state on the file? not sure yet
                    // this code is shit all the same

                    if( !ResolvedFiles.ContainsKey( path ) )
                    {
                        ResolvedFiles[ path ] = file;
                    }
                    else
                    {
                        PluginLog.LogError(
                            "a different mod already fucks this file: {FilePath}",
                            ResolvedFiles[ path ].FullName
                        );
                    }
                }
            }
        }

        public FileInfo GetCandidateForGameFile( string resourcePath )
        {
            return ResolvedFiles.TryGetValue( resourcePath.ToLowerInvariant(), out var fileInfo ) ? fileInfo : null;
        }
    }
}