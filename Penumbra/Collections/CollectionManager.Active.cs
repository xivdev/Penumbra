using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Collections;

public partial class ModCollection
{
    public sealed partial class Manager
    {
        // Is invoked after the collections actually changed.
        public event CollectionChangeDelegate? CollectionChanged;

        // The collection currently selected for changing settings.
        public ModCollection Current { get; private set; } = Empty;

        // The collection used for general file redirections and all characters not specifically named.
        public ModCollection Default { get; private set; } = Empty;

        // A single collection that can not be deleted as a fallback for the current collection.
        public ModCollection DefaultName { get; private set; } = Empty;

        // The list of character collections.
        private readonly Dictionary< string, ModCollection > _characters = new();

        public IReadOnlyDictionary< string, ModCollection > Characters
            => _characters;

        // If a name does not correspond to a character, return the default collection instead.
        public ModCollection Character( string name )
            => _characters.TryGetValue( name, out var c ) ? c : Default;

        public bool HasCharacterCollections
            => _characters.Count > 0;

        // Set a active collection, can be used to set Default, Current or Character collections.
        public void SetCollection( int newIdx, Type type, string? characterName = null )
        {
            var oldCollectionIdx = type switch
            {
                Type.Default => Default.Index,
                Type.Current => Current.Index,
                Type.Character => characterName?.Length > 0
                    ? _characters.TryGetValue( characterName, out var c )
                        ? c.Index
                        : Default.Index
                    : -1,
                _ => -1,
            };

            if( oldCollectionIdx == -1 || newIdx == oldCollectionIdx )
            {
                return;
            }

            var newCollection = this[ newIdx ];
            if( newIdx > Empty.Index )
            {
                newCollection.CreateCache( false );
            }

            RemoveCache( oldCollectionIdx );
            switch( type )
            {
                case Type.Default:
                    Default                           = newCollection;
                    Penumbra.Config.DefaultCollection = newCollection.Name;
                    Penumbra.ResidentResources.Reload();
                    Default.SetFiles();
                    break;
                case Type.Current:
                    Current                           = newCollection;
                    Penumbra.Config.CurrentCollection = newCollection.Name;
                    break;
                case Type.Character:
                    _characters[ characterName! ]                          = newCollection;
                    Penumbra.Config.CharacterCollections[ characterName! ] = newCollection.Name;
                    break;
            }

            CollectionChanged?.Invoke( this[ oldCollectionIdx ], newCollection, type, characterName );
            Penumbra.Config.Save();
        }

        public void SetCollection( ModCollection collection, Type type, string? characterName = null )
            => SetCollection( collection.Index, type, characterName );

        // Create a new character collection. Returns false if the character name already has a collection.
        public bool CreateCharacterCollection( string characterName )
        {
            if( _characters.ContainsKey( characterName ) )
            {
                return false;
            }

            _characters[ characterName ]                          = Empty;
            Penumbra.Config.CharacterCollections[ characterName ] = Empty.Name;
            Penumbra.Config.Save();
            CollectionChanged?.Invoke( null, Empty, Type.Character, characterName );
            return true;
        }

        // Remove a character collection if it exists.
        public void RemoveCharacterCollection( string characterName )
        {
            if( _characters.TryGetValue( characterName, out var collection ) )
            {
                RemoveCache( collection.Index );
                _characters.Remove( characterName );
                CollectionChanged?.Invoke( collection, null, Type.Character, characterName );
            }

            if( Penumbra.Config.CharacterCollections.Remove( characterName ) )
            {
                Penumbra.Config.Save();
            }
        }

        // Obtain the index of a collection by name.
        private int GetIndexForCollectionName( string name )
            => name.Length == 0 ? Empty.Index : _collections.IndexOf( c => c.Name == name );


        // Load default, current and character collections from config.
        // Then create caches. If a collection does not exist anymore, reset it to an appropriate default.
        public void LoadCollections()
        {
            var configChanged = false;
            var defaultIdx    = GetIndexForCollectionName( Penumbra.Config.DefaultCollection );
            if( defaultIdx < 0 )
            {
                PluginLog.Error( $"Last choice of Default Collection {Penumbra.Config.DefaultCollection} is not available, reset to None." );
                Default                           = Empty;
                Penumbra.Config.DefaultCollection = Default.Name;
                configChanged                     = true;
            }
            else
            {
                Default = this[ defaultIdx ];
            }

            var currentIdx = GetIndexForCollectionName( Penumbra.Config.CurrentCollection );
            if( currentIdx < 0 )
            {
                PluginLog.Error( $"Last choice of Current Collection {Penumbra.Config.CurrentCollection} is not available, reset to Default." );
                Current                           = DefaultName;
                Penumbra.Config.DefaultCollection = Current.Name;
                configChanged                     = true;
            }
            else
            {
                Current = this[ currentIdx ];
            }

            if( LoadCharacterCollections() || configChanged )
            {
                Penumbra.Config.Save();
            }

            CreateNecessaryCaches();
        }

        // Load character collections. If a player name comes up multiple times, the last one is applied.
        private bool LoadCharacterCollections()
        {
            var configChanged = false;
            foreach( var (player, collectionName) in Penumbra.Config.CharacterCollections.ToArray() )
            {
                var idx = GetIndexForCollectionName( collectionName );
                if( idx < 0 )
                {
                    PluginLog.Error( $"Last choice of <{player}>'s Collection {collectionName} is not available, reset to None." );
                    _characters.Add( player, Empty );
                    Penumbra.Config.CharacterCollections[ player ] = Empty.Name;
                    configChanged                                  = true;
                }
                else
                {
                    _characters.Add( player, this[ idx ] );
                }
            }

            return configChanged;
        }


        // Cache handling.
        private void CreateNecessaryCaches()
        {
            Default.CreateCache( true );
            Current.CreateCache( false );

            foreach( var collection in _characters.Values )
            {
                collection.CreateCache( false );
            }
        }

        private void RemoveCache( int idx )
        {
            if( idx != Default.Index && idx != Current.Index && _characters.Values.All( c => c.Index != idx ) )
            {
                _collections[ idx ].ClearCache();
            }
        }

        private void ForceCacheUpdates()
        {
            foreach( var collection in this )
            {
                collection.ForceCacheUpdate( collection == Default );
            }
        }

        // Recalculate effective files for active collections on events.
        private void OnModAddedActive( bool meta )
        {
            foreach( var collection in this.Where( c => c.HasCache && c[ ^1 ].Settings?.Enabled == true ) )
            {
                collection.CalculateEffectiveFileList( meta, collection == Penumbra.CollectionManager.Default );
            }
        }

        private void OnModRemovedActive( bool meta, IEnumerable< ModSettings? > settings )
        {
            foreach( var (collection, _) in this.Zip( settings ).Where( c => c.First.HasCache && c.Second?.Enabled == true ) )
            {
                collection.CalculateEffectiveFileList( meta, collection == Penumbra.CollectionManager.Default );
            }
        }

        private void OnModChangedActive( bool meta, int modIdx )
        {
            foreach( var collection in this.Where( c => c.HasCache && c[ modIdx ].Settings?.Enabled == true ) )
            {
                collection.CalculateEffectiveFileList( meta, collection == Penumbra.CollectionManager.Default );
            }
        }
    }
}