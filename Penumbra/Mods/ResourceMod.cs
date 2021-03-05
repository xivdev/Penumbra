using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Penumbra.Importer;
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
        public Dictionary< FileInfo, TexToolsMeta > MetaManipulations { get; } = new();

        public Dictionary< string, List< GamePath > > FileConflicts { get; } = new();


        public void RefreshModFiles()
        {
            FileConflicts.Clear();
            ModFiles.Clear();
            MetaManipulations.Clear();
            // we don't care about any _files_ in the root dir, but any folders should be a game folder/file combo
            foreach( var file in ModBasePath.EnumerateDirectories()
                .SelectMany( dir => dir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) ) )
            {
                if( file.Extension == ".meta" )
                {
                    try
                    {
                        MetaManipulations[ file ] = new TexToolsMeta( File.ReadAllBytes( file.FullName ) );
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Could not parse meta file {file.FullName}:\n{e}" );
                    }
                }

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