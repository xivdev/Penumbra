using System.Collections.Generic;
using System.IO;
using Penumbra.Models;

namespace Penumbra.Mods
{
    public class ModManager
    {
        public readonly Dictionary< string, FileInfo > ResolvedFiles = new();
        public readonly Dictionary< string, string > SwappedFiles = new();

        public ModCollection Mods { get; set; }

        private DirectoryInfo _basePath;

        public void DiscoverMods()
        {
            if( _basePath == null )
            {
                return;
            }

            DiscoverMods( _basePath );
        }

        public void DiscoverMods( string basePath )
        {
            DiscoverMods( new DirectoryInfo( basePath ) );
        }

        public void DiscoverMods( DirectoryInfo basePath )
        {
            if( basePath == null )
            {
                return;
            }

            if( !basePath.Exists )
            {
                Mods = null;
                return;
            }

            _basePath = basePath;

            ResolvedFiles.Clear();

            Mods = new ModCollection( basePath );
            Mods.Load();
            Mods.Save();

            CalculateEffectiveFileList();
        }

        public void CalculateEffectiveFileList()
        {
            ResolvedFiles.Clear();
            SwappedFiles.Clear();

            var registeredFiles = new Dictionary< string, string >();

            foreach( var mod in Mods.GetOrderedAndEnabledModList() )
            {
                mod.FileConflicts?.Clear();
                
                // fixup path
                var baseDir = mod.ModBasePath.FullName;

                foreach( var file in mod.ModFiles )
                {
                    var path = file.FullName.Substring( baseDir.Length )
                        .TrimStart( '\\' ).Replace( '\\', '/' );

                    if( !ResolvedFiles.ContainsKey( path ) )
                    {
                        ResolvedFiles[ path ] = file;
                        registeredFiles[ path ] = mod.Meta.Name;
                    }
                    else if( registeredFiles.TryGetValue( path, out var modName ) )
                    {
                        mod.AddConflict( modName, path );
                    }
                }

                foreach( var swap in mod.Meta.FileSwaps )
                {
                    // just assume people put not fucked paths in here lol
                    if( !SwappedFiles.ContainsKey( swap.Value ) )
                    {
                        SwappedFiles[ swap.Key ] = swap.Value;
                        registeredFiles[ swap.Key ] = mod.Meta.Name;
                    }
                    else if( registeredFiles.TryGetValue( swap.Key, out var modName ) )
                    {
                        mod.AddConflict( modName, swap.Key );
                    }
                }
            }
        }

        public void ChangeModPriority( ModInfo info, bool up = false )
        {
            Mods.ReorderMod( info, up );
            CalculateEffectiveFileList();
        }

        public void DeleteMod( ResourceMod mod )
        {
            Directory.Delete( mod.ModBasePath.FullName, true );
            DiscoverMods();
        }


        public FileInfo GetCandidateForGameFile( string gameResourcePath )
        {
            var val = ResolvedFiles.TryGetValue( gameResourcePath, out var candidate );
            if( !val )
            {
                return null;
            }

            if( candidate.FullName.Length >= 260 || !candidate.Exists )
            {
                return null;
            }

            return candidate;
        }

        public string GetSwappedFilePath( string gameResourcePath )
        {
            return SwappedFiles.TryGetValue( gameResourcePath, out var swappedPath ) ? swappedPath : null;
        }

        public string ResolveSwappedOrReplacementFilePath( string gameResourcePath )
        {
            gameResourcePath = gameResourcePath.ToLowerInvariant();

            return GetCandidateForGameFile( gameResourcePath )?.FullName ?? GetSwappedFilePath( gameResourcePath );
        }
    }
}