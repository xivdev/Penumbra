using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.Interop;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods
{
    // Contains all collections and respective functions, as well as the collection settings.
    public class CollectionManager
    {
        private readonly ModManager _manager;

        public string CollectionChangedTo { get; private set; } = string.Empty;
        public Dictionary< string, ModCollection > Collections { get; } = new();

        public ModCollection CurrentCollection { get; private set; } = null!;
        public ModCollection DefaultCollection { get; private set; } = null!;
        public ModCollection ForcedCollection { get; private set; } = ModCollection.Empty;
        public Dictionary< string, ModCollection > CharacterCollection { get; } = new();

        public ModCollection ActiveCollection { get; private set; }

        public CollectionManager( ModManager manager )
        {
            _manager = manager;

            ReadCollections();
            LoadConfigCollections( Penumbra.Config );
            ActiveCollection = DefaultCollection;
        }

        public bool SetActiveCollection( ModCollection newActive, string name )
        {
            CollectionChangedTo = name;
            if( newActive == ActiveCollection )
            {
                return false;
            }

            if( ActiveCollection.Cache?.MetaManipulations.Count > 0 || newActive.Cache?.MetaManipulations.Count > 0 )
            {
                var resourceManager = Service< ResidentResources >.Get();
                resourceManager.ReloadPlayerResources();
            }

            ActiveCollection = newActive;
            return true;
        }

        public bool ResetActiveCollection()
            => SetActiveCollection( DefaultCollection, string.Empty );

        public void RecreateCaches()
        {
            if( !_manager.TempWritable )
            {
                PluginLog.Error( "No temporary directory available." );
                return;
            }

            foreach( var collection in Collections.Values.Where( c => c.Cache != null ) )
            {
                collection.CreateCache( _manager.TempPath, _manager.StructuredMods.AllMods( _manager.Config.SortFoldersFirst ) );
            }
        }

        public void RemoveModFromCaches( DirectoryInfo modDir )
        {
            foreach( var collection in Collections.Values )
            {
                collection.Cache?.RemoveMod( modDir );
            }
        }

        internal void UpdateCollections( ModData mod, bool metaChanges, ResourceChange fileChanges, bool nameChange, bool reloadMeta )
        {
            foreach( var collection in Collections.Values )
            {
                if( metaChanges )
                {
                    collection.UpdateSetting( mod );
                }

                if( fileChanges.HasFlag( ResourceChange.Files )
                 && collection.Settings.TryGetValue( mod.BasePath.Name, out var settings )
                 && settings.Enabled )
                {
                    collection.Cache?.CalculateEffectiveFileList();
                }

                if( reloadMeta )
                {
                    collection.Cache?.UpdateMetaManipulations();
                }
            }

            if( reloadMeta && ActiveCollection.Settings.TryGetValue( mod.BasePath.Name, out var config ) && config.Enabled )
            {
                Service< ResidentResources >.Get().ReloadPlayerResources();
            }
        }

        public bool AddCollection( string name, Dictionary< string, ModSettings > settings )
        {
            var nameFixed = name.RemoveInvalidPathSymbols().ToLowerInvariant();
            if( nameFixed == string.Empty || Collections.Values.Any( c => c.Name.RemoveInvalidPathSymbols().ToLowerInvariant() == nameFixed ) )
            {
                PluginLog.Warning( $"The new collection {name} would lead to the same path as one that already exists." );
                return false;
            }

            var newCollection = new ModCollection( name, settings );
            Collections.Add( name, newCollection );
            newCollection.Save();
            SetCurrentCollection( newCollection );
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
                    SetCurrentCollection( Collections[ ModCollection.DefaultCollection ] );
                }

                if( ForcedCollection == collection )
                {
                    SetForcedCollection( ModCollection.Empty );
                }

                if( DefaultCollection == collection )
                {
                    SetDefaultCollection( ModCollection.Empty );
                }

                foreach( var kvp in CharacterCollection.ToArray() )
                {
                    if( kvp.Value == collection )
                    {
                        SetCharacterCollection( kvp.Key, ModCollection.Empty );
                    }
                }

                collection.Delete();
                Collections.Remove( name );
                return true;
            }

            return false;
        }

        public void AddCache( ModCollection collection )
        {
            if( !_manager.TempWritable )
            {
                PluginLog.Error( "No tmp directory available." );
                return;
            }

            if( collection.Cache == null && collection.Name != string.Empty )
            {
                collection.CreateCache( _manager.TempPath, _manager.StructuredMods.AllMods( _manager.Config.SortFoldersFirst ) );
            }
        }

        private void RemoveCache( ModCollection collection )
        {
            if( collection.Name != ForcedCollection.Name
             && collection.Name != CurrentCollection.Name
             && collection.Name != DefaultCollection.Name
             && CharacterCollection.All( kvp => kvp.Value.Name != collection.Name ) )
            {
                collection.ClearCache();
            }
        }

        private void SetCollection( ModCollection newCollection, ModCollection oldCollection, Action< ModCollection > setter,
            Action< string > configSetter )
        {
            if( newCollection.Name == oldCollection.Name )
            {
                return;
            }

            AddCache( newCollection );

            setter( newCollection );
            RemoveCache( oldCollection );
            configSetter( newCollection.Name );
            Penumbra.Config.Save();
        }

        public void SetDefaultCollection( ModCollection newCollection )
            => SetCollection( newCollection, DefaultCollection, c =>
            {
                if( !CollectionChangedTo.Any() )
                {
                    ActiveCollection = c;
                    var resourceManager = Service< ResidentResources >.Get();
                    resourceManager.ReloadPlayerResources();
                }

                DefaultCollection = c;
            }, s => Penumbra.Config.DefaultCollection = s );

        public void SetForcedCollection( ModCollection newCollection )
            => SetCollection( newCollection, ForcedCollection, c => ForcedCollection = c, s => Penumbra.Config.ForcedCollection = s );

        public void SetCurrentCollection( ModCollection newCollection )
            => SetCollection( newCollection, CurrentCollection, c => CurrentCollection = c, s => Penumbra.Config.CurrentCollection = s );

        public void SetCharacterCollection( string characterName, ModCollection newCollection )
            => SetCollection( newCollection,
                CharacterCollection.TryGetValue( characterName, out var oldCollection ) ? oldCollection : ModCollection.Empty,
                c =>
                {
                    if( CollectionChangedTo == characterName && CharacterCollection.TryGetValue( characterName, out var collection ) )
                    {
                        ActiveCollection = c;
                        var resourceManager = Service< ResidentResources >.Get();
                        resourceManager.ReloadPlayerResources();
                    }

                    CharacterCollection[ characterName ] = c;
                }, s => Penumbra.Config.CharacterCollections[ characterName ] = s );

        public bool CreateCharacterCollection( string characterName )
        {
            if( !CharacterCollection.ContainsKey( characterName ) )
            {
                CharacterCollection[ characterName ]                  = ModCollection.Empty;
                Penumbra.Config.CharacterCollections[ characterName ] = string.Empty;
                Penumbra.Config.Save();
                Penumbra.PlayerWatcher.AddPlayerToWatch( characterName );
                return true;
            }

            return false;
        }

        public void RemoveCharacterCollection( string characterName )
        {
            if( CharacterCollection.TryGetValue( characterName, out var collection ) )
            {
                RemoveCache( collection );
                CharacterCollection.Remove( characterName );
                Penumbra.PlayerWatcher.RemovePlayerFromWatch( characterName );
            }

            if( Penumbra.Config.CharacterCollections.Remove( characterName ) )
            {
                Penumbra.Config.Save();
            }
        }

        private bool LoadCurrentCollection( Configuration config )
        {
            if( Collections.TryGetValue( config.CurrentCollection, out var currentCollection ) )
            {
                CurrentCollection = currentCollection;
                AddCache( CurrentCollection );
                return false;
            }

            PluginLog.Error( $"Last choice of CurrentCollection {config.CurrentCollection} is not available, reset to Default." );
            CurrentCollection        = Collections[ ModCollection.DefaultCollection ];
            config.CurrentCollection = ModCollection.DefaultCollection;
            return true;
        }

        private bool LoadForcedCollection( Configuration config )
        {
            if( config.ForcedCollection == string.Empty )
            {
                ForcedCollection = ModCollection.Empty;
                return false;
            }

            if( Collections.TryGetValue( config.ForcedCollection, out var forcedCollection ) )
            {
                ForcedCollection = forcedCollection;
                AddCache( ForcedCollection );
                return false;
            }

            PluginLog.Error( $"Last choice of ForcedCollection {config.ForcedCollection} is not available, reset to None." );
            ForcedCollection        = ModCollection.Empty;
            config.ForcedCollection = string.Empty;
            return true;
        }

        private bool LoadDefaultCollection( Configuration config )
        {
            if( config.DefaultCollection == string.Empty )
            {
                DefaultCollection = ModCollection.Empty;
                return false;
            }

            if( Collections.TryGetValue( config.DefaultCollection, out var defaultCollection ) )
            {
                DefaultCollection = defaultCollection;
                AddCache( DefaultCollection );
                return false;
            }

            PluginLog.Error( $"Last choice of DefaultCollection {config.DefaultCollection} is not available, reset to None." );
            DefaultCollection        = ModCollection.Empty;
            config.DefaultCollection = string.Empty;
            return true;
        }

        private bool LoadCharacterCollections( Configuration config )
        {
            var configChanged = false;
            foreach( var kvp in config.CharacterCollections.ToArray() )
            {
                Penumbra.PlayerWatcher.AddPlayerToWatch( kvp.Key );
                if( kvp.Value == string.Empty )
                {
                    CharacterCollection.Add( kvp.Key, ModCollection.Empty );
                }
                else if( Collections.TryGetValue( kvp.Value, out var charCollection ) )
                {
                    AddCache( charCollection );
                    CharacterCollection.Add( kvp.Key, charCollection );
                }
                else
                {
                    PluginLog.Error( $"Last choice of <{kvp.Key}>'s Collection {kvp.Value} is not available, reset to None." );
                    CharacterCollection.Add( kvp.Key, ModCollection.Empty );
                    config.CharacterCollections[ kvp.Key ] = string.Empty;
                    configChanged                          = true;
                }
            }

            return configChanged;
        }

        private void LoadConfigCollections( Configuration config )
        {
            var configChanged = LoadCurrentCollection( config );
            configChanged |= LoadDefaultCollection( config );
            configChanged |= LoadForcedCollection( config );
            configChanged |= LoadCharacterCollections( config );

            if( configChanged )
            {
                config.Save();
            }
        }

        private void ReadCollections()
        {
            var collectionDir = ModCollection.CollectionDir();
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
                defaultCollection.Save();
                Collections.Add( defaultCollection.Name, defaultCollection );
            }
        }
    }
}