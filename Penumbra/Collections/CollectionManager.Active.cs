using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Collections;

public partial class ModCollection
{
    public sealed partial class Manager
    {
        // Is invoked after the collections actually changed.
        public event CollectionChangeDelegate? CollectionChanged;

        private int _currentIdx     = 1;
        private int _defaultIdx     = 0;
        private int _defaultNameIdx = 0;

        public ModCollection Current
            => this[ _currentIdx ];

        public ModCollection Default
            => this[ _defaultIdx ];

        private readonly Dictionary< string, int > _character = new();

        public ModCollection Character( string name )
            => _character.TryGetValue( name, out var idx ) ? this[ idx ] : Default;

        public IEnumerable< (string, ModCollection) > Characters
            => _character.Select( kvp => ( kvp.Key, this[ kvp.Value ] ) );

        public bool HasCharacterCollections
            => _character.Count > 0;

        private void OnModChanged( Mod.Mod.ChangeType type, int idx, Mod.Mod mod )
        {
            var meta = mod.Resources.MetaManipulations.Count > 0;
            switch( type )
            {
                case Mod.Mod.ChangeType.Added:
                    foreach( var collection in this )
                    {
                        collection.AddMod( mod );
                    }

                    foreach( var collection in this.Where( c => c.HasCache && c[ ^1 ].Settings?.Enabled == true ) )
                    {
                        collection.CalculateEffectiveFileList( meta, collection == Penumbra.CollectionManager.Default );
                    }

                    break;
                case Mod.Mod.ChangeType.Removed:
                    var list = new List< ModSettings? >( _collections.Count );
                    foreach( var collection in this )
                    {
                        list.Add( collection[ idx ].Settings );
                        collection.RemoveMod( mod, idx );
                    }

                    foreach( var (collection, _) in this.Zip( list ).Where( c => c.First.HasCache && c.Second?.Enabled == true ) )
                    {
                        collection.CalculateEffectiveFileList( meta, collection == Penumbra.CollectionManager.Default );
                    }

                    break;
                case Mod.Mod.ChangeType.Changed:
                    foreach( var collection in this.Where(
                                collection => collection.Settings[ idx ]?.FixInvalidSettings( mod.Meta ) ?? false ) )
                    {
                        collection.Save();
                    }

                    foreach( var collection in this.Where( c => c.HasCache && c[ idx ].Settings?.Enabled == true ) )
                    {
                        collection.CalculateEffectiveFileList( meta, collection == Penumbra.CollectionManager.Default );
                    }

                    break;
                default: throw new ArgumentOutOfRangeException( nameof( type ), type, null );
            }
        }

        private void CreateNecessaryCaches()
        {
            if( _defaultIdx > Empty.Index )
            {
                Default.CreateCache(true);
            }

            if( _currentIdx > Empty.Index )
            {
                Current.CreateCache(false);
            }

            foreach( var idx in _character.Values.Where( i => i > Empty.Index ) )
            {
                _collections[ idx ].CreateCache(false);
            }
        }

        public void ForceCacheUpdates()
        {
            foreach( var collection in this )
            {
                collection.ForceCacheUpdate(collection == Default);
            }
        }

        private void RemoveCache( int idx )
        {
            if( idx != _defaultIdx && idx != _currentIdx && _character.All( kvp => kvp.Value != idx ) )
            {
                _collections[ idx ].ClearCache();
            }
        }

        public void SetCollection( ModCollection collection, Type type, string? characterName = null )
            => SetCollection( collection.Index, type, characterName );

        public void SetCollection( int newIdx, Type type, string? characterName = null )
        {
            var oldCollectionIdx = type switch
            {
                Type.Default => _defaultIdx,
                Type.Current => _currentIdx,
                Type.Character => characterName?.Length > 0
                    ? _character.TryGetValue( characterName, out var c )
                        ? c
                        : _defaultIdx
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
                newCollection.CreateCache(false);
            }

            RemoveCache( oldCollectionIdx );
            switch( type )
            {
                case Type.Default:
                    _defaultIdx                       = newIdx;
                    Penumbra.Config.DefaultCollection = newCollection.Name;
                    Penumbra.ResidentResources.Reload();
                    Default.SetFiles();
                    break;
                case Type.Current:
                    _currentIdx                       = newIdx;
                    Penumbra.Config.CurrentCollection = newCollection.Name;
                    break;
                case Type.Character:
                    _character[ characterName! ]                           = newIdx;
                    Penumbra.Config.CharacterCollections[ characterName! ] = newCollection.Name;
                    break;
            }

            CollectionChanged?.Invoke( this[ oldCollectionIdx ], newCollection, type, characterName );
            Penumbra.Config.Save();
        }

        public bool CreateCharacterCollection( string characterName )
        {
            if( _character.ContainsKey( characterName ) )
            {
                return false;
            }

            _character[ characterName ]                           = Empty.Index;
            Penumbra.Config.CharacterCollections[ characterName ] = Empty.Name;
            Penumbra.Config.Save();
            CollectionChanged?.Invoke( null, Empty, Type.Character, characterName );
            return true;
        }

        public void RemoveCharacterCollection( string characterName )
        {
            if( _character.TryGetValue( characterName, out var collection ) )
            {
                RemoveCache( collection );
                _character.Remove( characterName );
                CollectionChanged?.Invoke( this[ collection ], null, Type.Character, characterName );
            }

            if( Penumbra.Config.CharacterCollections.Remove( characterName ) )
            {
                Penumbra.Config.Save();
            }
        }

        private int GetIndexForCollectionName( string name )
        {
            if( name.Length == 0 )
            {
                return Empty.Index;
            }

            return _collections.IndexOf( c => c.Name == name );
        }

        public void LoadCollections()
        {
            var configChanged = false;
            _defaultIdx = GetIndexForCollectionName( Penumbra.Config.DefaultCollection );
            if( _defaultIdx < 0 )
            {
                PluginLog.Error( $"Last choice of Default Collection {Penumbra.Config.DefaultCollection} is not available, reset to None." );
                _defaultIdx                       = Empty.Index;
                Penumbra.Config.DefaultCollection = this[ _defaultIdx ].Name;
                configChanged                     = true;
            }

            _currentIdx = GetIndexForCollectionName( Penumbra.Config.CurrentCollection );
            if( _currentIdx < 0 )
            {
                PluginLog.Error( $"Last choice of Current Collection {Penumbra.Config.CurrentCollection} is not available, reset to Default." );
                _currentIdx                       = _defaultNameIdx;
                Penumbra.Config.DefaultCollection = this[ _currentIdx ].Name;
                configChanged                     = true;
            }

            if( LoadCharacterCollections() || configChanged )
            {
                Penumbra.Config.Save();
            }

            CreateNecessaryCaches();
        }

        private bool LoadCharacterCollections()
        {
            var configChanged = false;
            foreach( var (player, collectionName) in Penumbra.Config.CharacterCollections.ToArray() )
            {
                var idx = GetIndexForCollectionName( collectionName );
                if( idx < 0 )
                {
                    PluginLog.Error( $"Last choice of <{player}>'s Collection {collectionName} is not available, reset to None." );
                    _character.Add( player, Empty.Index );
                    Penumbra.Config.CharacterCollections[ player ] = Empty.Name;
                    configChanged                                  = true;
                }
                else
                {
                    _character.Add( player, idx );
                }
            }

            return configChanged;
        }
    }
}