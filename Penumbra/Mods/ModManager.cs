using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Plugin;
using Penumbra.Meta;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods
{
    // The ModManager handles the basic mods installed to the mod directory.
    // It also contains the CollectionManager that handles all collections.
    public class ModManager
    {
        public DirectoryInfo BasePath { get; private set; }

        public Dictionary< string, ModData > Mods { get; } = new();
        public CollectionManager Collections { get; }

        public ModManager( Plugin plugin )
        {
            BasePath    = new DirectoryInfo( plugin.Configuration.ModDirectory );
            Collections = new CollectionManager( plugin, this );
        }

        public void DiscoverMods( DirectoryInfo basePath )
        {
            BasePath = basePath;
            DiscoverMods();
        }

        public void DiscoverMods()
        {
            Mods.Clear();
            if( !BasePath.Exists )
            {
                PluginLog.Debug( "The mod directory {Directory} does not exist.", BasePath.FullName );
                try
                {
                    Directory.CreateDirectory( BasePath.FullName );
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"The mod directory {BasePath.FullName} does not exist and could not be created:\n{e}" );
                    return;
                }
            }

            foreach( var modFolder in BasePath.EnumerateDirectories() )
            {
                var mod = ModData.LoadMod( modFolder );
                if( mod == null )
                {
                    continue;
                }

                Mods.Add( modFolder.Name, mod );
            }

            Collections.RecreateCaches();
        }

        public void DeleteMod( DirectoryInfo modFolder )
        {
            modFolder.Refresh();
            if( modFolder.Exists )
            {
                try
                {
                    Directory.Delete( modFolder.FullName, true );
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"Could not delete the mod {modFolder.Name}:\n{e}" );
                }

                Mods.Remove( modFolder.Name );
                Collections.RemoveModFromCaches( modFolder );
            }
        }

        public bool AddMod( DirectoryInfo modFolder )
        {
            var mod = ModData.LoadMod( modFolder );
            if( mod == null )
            {
                return false;
            }

            if( Mods.ContainsKey( modFolder.Name ) )
            {
                return false;
            }

            Mods.Add( modFolder.Name, mod );
            foreach( var collection in Collections.Collections.Values )
            {
                collection.AddMod( mod );
            }

            return true;
        }

        public bool UpdateMod( ModData mod, bool recomputeMeta = false )
        {
            var oldName     = mod.Meta.Name;
            var metaChanges = mod.Meta.RefreshFromFile( mod.MetaFile );
            var fileChanges = mod.Resources.RefreshModFiles( mod.BasePath );

            if( !( recomputeMeta || metaChanges || fileChanges == 0 ) )
            {
                return false;
            }

            var nameChange = !string.Equals( oldName, mod.Meta.Name, StringComparison.InvariantCulture );

            recomputeMeta |= fileChanges.HasFlag( ResourceChange.Meta );
            if( recomputeMeta )
            {
                mod.Resources.MetaManipulations.Update( mod.Resources.MetaFiles, mod.BasePath, mod.Meta );
                mod.Resources.MetaManipulations.SaveToFile( MetaCollection.FileName( mod.BasePath ) );
            }

            Collections.UpdateCollections( mod, metaChanges, fileChanges, nameChange, recomputeMeta );

            return true;
        }

        public string? ResolveSwappedOrReplacementPath( GamePath gameResourcePath )
        {
            var ret = Collections.ActiveCollection.ResolveSwappedOrReplacementPath( gameResourcePath );
            ret ??= Collections.ForcedCollection.ResolveSwappedOrReplacementPath( gameResourcePath );
            return ret;
        }

        //         private void FileSystemWatcherOnChanged( object sender, FileSystemEventArgs e )
        //         {
        // #if DEBUG
        //             PluginLog.Verbose( "file changed: {FullPath}", e.FullPath );
        // #endif
        //
        //             if( _plugin.ImportInProgress )
        //             {
        //                 return;
        //             }
        //
        //             if( _plugin.Configuration.DisableFileSystemNotifications )
        //             {
        //                 return;
        //             }
        //
        //             var file = e.FullPath;
        //
        //             if( !ResolvedFiles.Any( x => x.Value.FullName == file ) )
        //             {
        //                 return;
        //             }
        //
        //             PluginLog.Log( "a loaded file has been modified - file: {FullPath}", file );
        //             _plugin.GameUtils.ReloadPlayerResources();
        //         }
        // 
        //         private void FileSystemPasta()
        //         {
        //              haha spaghet
        //              _fileSystemWatcher?.Dispose();
        //              _fileSystemWatcher = new FileSystemWatcher( _basePath.FullName )
        //              {
        //                  NotifyFilter = NotifyFilters.LastWrite |
        //                                 NotifyFilters.FileName |
        //                                 NotifyFilters.DirectoryName,
        //                  IncludeSubdirectories = true,
        //                  EnableRaisingEvents = true
        //              };
        //             
        //              _fileSystemWatcher.Changed += FileSystemWatcherOnChanged;
        //              _fileSystemWatcher.Created += FileSystemWatcherOnChanged;
        //              _fileSystemWatcher.Deleted += FileSystemWatcherOnChanged;
        //              _fileSystemWatcher.Renamed += FileSystemWatcherOnChanged;
        //         }
    }
}