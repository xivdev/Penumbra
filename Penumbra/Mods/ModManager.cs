using System.Collections.Generic;
using System.IO;
using Penumbra.Models;

namespace Penumbra.Mods
{
    public class ModManager
    {
        public readonly Dictionary< string, FileInfo > ResolvedFiles = new();

        public ModCollection Mods { get; set; }

        public ResourceMod[] AvailableMods => Mods?.EnabledMods;

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
                Directory.CreateDirectory( basePath.FullName );
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


        public FileInfo GetCandidateForGameFile( string resourcePath )
        {
            return ResolvedFiles.TryGetValue( resourcePath.ToLowerInvariant(), out var fileInfo ) ? fileInfo : null;
        }
    }
}