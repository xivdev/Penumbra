using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Penumbra.GameData.Util;
using Penumbra.Meta;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods
{
    // The ModManager handles the basic mods installed to the mod directory.
    // It also contains the CollectionManager that handles all collections.
    public class ModManager
    {
        private readonly Plugin _plugin;
        public DirectoryInfo BasePath { get; private set; } = null!;

        public Dictionary< string, ModData > Mods { get; } = new();
        public CollectionManager Collections { get; }

        public bool Valid { get; private set; }

        public Configuration Config
            => _plugin.Configuration;

        private void SetBaseDirectory( string basePath )
        {
            if( basePath.Any() )
            {
                BasePath = new DirectoryInfo( basePath );
                Valid    = Path.IsPathRooted( basePath );
            }
            else
            {
                BasePath = new DirectoryInfo( "." );
                Valid    = false;
            }
        }

        public ModManager( Plugin plugin )
        {
            _plugin = plugin;
            SetBaseDirectory( plugin.Configuration.ModDirectory );
            MetaManager.ClearBaseDirectory( BasePath! );

            Collections = new CollectionManager( plugin, this );
        }

        public void DiscoverMods( string basePath )
        {
            SetBaseDirectory( basePath );
            DiscoverMods();
        }

        private void SetModOrders( Configuration config )
        {
            var changes = false;
            foreach( var kvp in config.ModSortOrder.ToArray() )
            {
                if( Mods.TryGetValue( kvp.Key, out var mod ) )
                {
                    mod.SortOrder = string.Join( "/", kvp.Value.Trim().Split( new[] { "/" }, StringSplitOptions.RemoveEmptyEntries ) );
                }
                else
                {
                    changes = true;
                    config.ModSortOrder.Remove( kvp.Key );
                }
            }

            if( changes )
            {
                config.Save();
            }
        }

        public void DiscoverMods()
        {
            Mods.Clear();
            if( Valid && !BasePath.Exists )
            {
                PluginLog.Debug( "The mod directory {Directory} does not exist.", BasePath.FullName );
                try
                {
                    Directory.CreateDirectory( BasePath.FullName );
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"The mod directory {BasePath.FullName} does not exist and could not be created:\n{e}" );
                    Valid = false;
                }
            }

            if( Valid )
            {
                foreach( var modFolder in BasePath.EnumerateDirectories() )
                {
                    var mod = ModData.LoadMod( modFolder );
                    if( mod == null )
                    {
                        continue;
                    }

                    Mods.Add( modFolder.Name, mod );
                }

                SetModOrders( _plugin.Configuration );
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

            if( Config.ModSortOrder.TryGetValue( mod.BasePath.Name, out var sortOrder ) )
            {
                mod.SortOrder = sortOrder;
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

        public bool UpdateMod( ModData mod, bool reloadMeta = false, bool recomputeMeta = false )
        {
            var oldName     = mod.Meta.Name;
            var metaChanges = mod.Meta.RefreshFromFile( mod.MetaFile );
            var fileChanges = mod.Resources.RefreshModFiles( mod.BasePath );

            if( !( recomputeMeta || metaChanges || fileChanges == 0 ) )
            {
                return false;
            }

            if( metaChanges || fileChanges.HasFlag( ResourceChange.Files ) )
            {
                mod.ComputeChangedItems();
                if( Config.ModSortOrder.TryGetValue( mod.BasePath.Name, out var sortOrder ) )
                {
                    mod.SortOrder = sortOrder;
                }
                else
                {
                    mod.SortOrder = mod.Meta.Name.Replace( '/', '\\' );
                }
            }

            var nameChange = !string.Equals( oldName, mod.Meta.Name, StringComparison.InvariantCulture );

            recomputeMeta |= fileChanges.HasFlag( ResourceChange.Meta );
            if( recomputeMeta )
            {
                mod.Resources.MetaManipulations.Update( mod.Resources.MetaFiles, mod.BasePath, mod.Meta );
                mod.Resources.MetaManipulations.SaveToFile( MetaCollection.FileName( mod.BasePath ) );
            }

            Collections.UpdateCollections( mod, metaChanges, fileChanges, nameChange, reloadMeta );

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