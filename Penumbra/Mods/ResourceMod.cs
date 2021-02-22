using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.Models;
using Penumbra.Util;

namespace Penumbra.Mods
{
    public class ResourceMod
    {
        public ResourceMod( ModMeta meta, DirectoryInfo dir )
        {
            Meta        = meta;
            ModBasePath = dir;
        }

        public ModMeta Meta { get; set; }

        public DirectoryInfo ModBasePath { get; set; }

        public List< FileInfo > ModFiles { get; } = new();

        public Dictionary< string, List< GamePath > > FileConflicts { get; } = new();

        public void RefreshModFiles()
        {
            ModFiles.Clear();
            // we don't care about any _files_ in the root dir, but any folders should be a game folder/file combo
            foreach( var file in ModBasePath.EnumerateDirectories()
                .SelectMany( dir => dir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) ) )
            {
                ModFiles.Add( file );
            }
        }

        public void AddConflict( string modName, GamePath path )
        {
            if( FileConflicts.TryGetValue( modName, out var arr ) )
            {
                if( !arr.Contains( path ) )
                {
                    arr.Add( path );
                }

                return;
            }

            FileConflicts[ modName ] = new List< GamePath > { path };
        }
    }
}