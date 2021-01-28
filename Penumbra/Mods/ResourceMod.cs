using System.Collections.Generic;
using System.IO;
using Dalamud.Plugin;
using Penumbra.Models;

namespace Penumbra.Mods
{
    public class ResourceMod
    {
        public ModMeta Meta { get; set; }

        public DirectoryInfo ModBasePath { get; set; }

        public List< FileInfo > ModFiles { get; } = new();

        public Dictionary< string, List< string > > FileConflicts { get; } = new();

        public void RefreshModFiles()
        {
            if( ModBasePath == null )
            {
                PluginLog.LogError( "no basepath has been set on {ResourceModName}", Meta.Name );
                return;
            }

            ModFiles.Clear();
            // we don't care about any _files_ in the root dir, but any folders should be a game folder/file combo
            foreach( var dir in ModBasePath.EnumerateDirectories() )
            {
                foreach( var file in dir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
                {
                    ModFiles.Add( file );
                }
            }

            // Only add if not in a sub-folder, otherwise it was already added.
            //foreach( var pair in Meta.Groups.FileToGameAndGroup )
            //    if (pair.Key.IndexOfAny(new[]{'/', '\\'}) < 0) 
            //        ModFiles.Add( new FileInfo(Path.Combine(ModBasePath.FullName, pair.Key)) );
        }

        public void AddConflict( string modName, string path )
        {
            if( FileConflicts.TryGetValue( modName, out var arr ) )
            {
                if( !arr.Contains( path ) )
                {
                    arr.Add( path );
                }

                return;
            }

            FileConflicts[ modName ] = new List< string >
            {
                path
            };
        }
    }
}