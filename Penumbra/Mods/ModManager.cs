using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Penumbra.Meta;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods
{
    // The ModManager handles the basic mods installed to the mod directory,
    // as well as all saved collections.
    // It also handles manual changes to mods that require changes in all collections,
    // updating the state of a mod from the filesystem,
    // and collection swapping.
    public class ModManager
    {
        private readonly Plugin _plugin;
        public DirectoryInfo BasePath { get; private set; }

        public Dictionary< string, ModData > Mods { get; } = new();
        public Dictionary< string, ModCollection > Collections { get; } = new();

        public ModCollection CurrentCollection { get; private set; }

        public ModManager( Plugin plugin )
        {
            _plugin  = plugin;
            BasePath = new DirectoryInfo( plugin.Configuration!.ModDirectory );
            ReadCollections();
            CurrentCollection = Collections.Values.First();
            if( !SetCurrentCollection( plugin.Configuration!.CurrentCollection ) )
            {
                PluginLog.Debug( "Last choice of collection {Name} is not available, reset to Default.",
                    plugin.Configuration!.CurrentCollection );

                if( SetCurrentCollection( ModCollection.DefaultCollection ) )
                {
                    PluginLog.Error( "Could not load any collection. Default collection unavailable." );
                    CurrentCollection = new ModCollection();
                }
            }
        }

        public bool SetCurrentCollection( string name )
        {
            if( Collections.TryGetValue( name, out var collection ) )
            {
                CurrentCollection = collection;
                if( CurrentCollection.Cache == null )
                {
                    CurrentCollection.CreateCache( BasePath, Mods );
                }

                return true;
            }

            return false;
        }

        public void ReadCollections()
        {
            var collectionDir = ModCollection.CollectionDir( _plugin.PluginInterface! );
            if( collectionDir.Exists )
            {
                foreach( var file in collectionDir.EnumerateFiles( "*.json" ) )
                {
                    var collection = ModCollection.LoadFromFile( file );
                    if( collection != null )
                    {
                        if( file.Name != $"{collection.Name.RemoveInvalidPathSymbols()}.json" )
                        {
                            PluginLog.Warning( $"Collection {file.Name} does not correspond to {collection.Name}." );
                        }

                        if( Collections.ContainsKey( collection.Name ) )
                        {
                            PluginLog.Warning( $"Duplicate collection found: {collection.Name} already exists." );
                        }
                        else
                        {
                            Collections.Add( collection.Name, collection );
                        }
                    }
                }
            }

            if( !Collections.ContainsKey( ModCollection.DefaultCollection ) )
            {
                var defaultCollection = new ModCollection();
                SaveCollection( defaultCollection );
                Collections.Add( defaultCollection.Name, defaultCollection );
            }
        }

        public void SaveCollection( ModCollection collection )
            => collection.Save( _plugin.PluginInterface! );


        public bool AddCollection( string name, Dictionary< string, ModSettings > settings )
        {
            var nameFixed = name.RemoveInvalidPathSymbols().ToLowerInvariant();
            if( Collections.Values.Any( c => c.Name.RemoveInvalidPathSymbols().ToLowerInvariant() == nameFixed ) )
            {
                PluginLog.Warning( $"The new collection {name} would lead to the same path as one that already exists." );
                return false;
            }

            var newCollection = new ModCollection( name, settings );
            Collections.Add( name, newCollection );
            SaveCollection( newCollection );
            CurrentCollection = newCollection;
            return true;
        }

        public bool RemoveCollection( string name )
        {
            if( name == ModCollection.DefaultCollection )
            {
                PluginLog.Error( "Can not remove the default collection." );
                return false;
            }

            if( Collections.TryGetValue( name, out var collection ) )
            {
                if( CurrentCollection == collection )
                {
                    SetCurrentCollection( ModCollection.DefaultCollection );
                }

                collection.Delete( _plugin.PluginInterface! );
                Collections.Remove( name );
                return true;
            }

            return false;
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

            foreach( var collection in Collections.Values.Where( c => c.Cache != null ) )
            {
                collection.CreateCache( BasePath, Mods );
            }
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
                foreach( var collection in Collections.Values.Where( c => c.Cache != null ) )
                {
                    collection.Cache!.RemoveMod( modFolder );
                }
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
            foreach( var collection in Collections.Values )
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

            foreach( var collection in Collections.Values )
            {
                if( metaChanges )
                {
                    collection.UpdateSetting( mod );
                    if( nameChange )
                    {
                        collection.Cache?.SortMods();
                    }
                }

                if( fileChanges.HasFlag( ResourceChange.Files )
                 && collection.Settings.TryGetValue( mod.BasePath.Name, out var settings )
                 && settings.Enabled )
                {
                    collection.Cache?.CalculateEffectiveFileList();
                }

                if( recomputeMeta )
                {
                    collection.Cache?.UpdateMetaManipulations();
                }
            }

            return true;
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