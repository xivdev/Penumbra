using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Plugin;
using Penumbra.Models;

namespace Penumbra
{
    public class ResourceMod
    {
        public ModMeta Meta { get; set; }

        public DirectoryInfo ModBasePath { get; set; }

        public List< FileInfo > ModFiles { get; } = new List< FileInfo >();

        public void RefreshModFiles()
        {
            if( ModBasePath == null )
            {
                PluginLog.LogError( "no basepath has been set on {ResourceModName}", Meta.Name );
                return;
            }

            // we don't care about any _files_ in the root dir, but any folders should be a game folder/file combo
            foreach( var dir in ModBasePath.EnumerateDirectories() )
            {
                foreach( var file in dir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
                {
                    ModFiles.Add( file );
                }
            }
        }
    }
}