using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.Util;

namespace Penumbra.Collections;

public partial class ModCollection
{
    public enum Type : byte
    {
        Inactive,
        Default,
        Character,
        Current,
    }

    public sealed partial class Manager : IDisposable, IEnumerable< ModCollection >
    {
        public delegate void CollectionChangeDelegate( ModCollection? oldCollection, ModCollection? newCollection, Type type,
            string? characterName = null );

        private readonly Mod.Mod.Manager _modManager;

        private readonly List< ModCollection > _collections = new()
        {
            Empty,
        };

        public ModCollection this[ Index idx ]
            => _collections[ idx ];

        public ModCollection this[ int idx ]
            => _collections[ idx ];

        public ModCollection? this[ string name ]
            => ByName( name, out var c ) ? c : null;

        public bool ByName( string name, [NotNullWhen( true )] out ModCollection? collection )
            => _collections.FindFirst( c => string.Equals( c.Name, name, StringComparison.InvariantCultureIgnoreCase ), out collection );

        public IEnumerator< ModCollection > GetEnumerator()
            => _collections.Skip( 1 ).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public Manager( Mod.Mod.Manager manager )
        {
            _modManager = manager;

            _modManager.ModsRediscovered += OnModsRediscovered;
            _modManager.ModChange        += OnModChanged;
            ReadCollections();
            LoadCollections();
        }

        public void Dispose()
        {
            _modManager.ModsRediscovered -= OnModsRediscovered;
            _modManager.ModChange        -= OnModChanged;
        }

        private void OnModsRediscovered()
        {
            ForceCacheUpdates();
            Default.SetFiles();
        }

        private void AddDefaultCollection()
        {
            var idx = _collections.IndexOf( c => c.Name == DefaultCollection );
            if( idx >= 0 )
            {
                _defaultNameIdx = idx;
                return;
            }

            var defaultCollection = CreateNewEmpty( DefaultCollection );
            defaultCollection.Save();
            _defaultNameIdx = _collections.Count;
            _collections.Add( defaultCollection );
        }

        private void ApplyInheritancesAndFixSettings( IEnumerable< IReadOnlyList< string > > inheritances )
        {
            foreach( var (collection, inheritance) in this.Zip( inheritances ) )
            {
                var changes = false;
                foreach( var subCollectionName in inheritance )
                {
                    if( !ByName( subCollectionName, out var subCollection ) )
                    {
                        changes = true;
                        PluginLog.Warning( $"Inherited collection {subCollectionName} for {collection.Name} does not exist, removed." );
                    }
                    else if( !collection.AddInheritance( subCollection ) )
                    {
                        changes = true;
                        PluginLog.Warning( $"{collection.Name} can not inherit from {subCollectionName}, removed." );
                    }
                }

                foreach( var (setting, mod) in collection.Settings.Zip( _modManager.Mods ).Where( s => s.First != null ) )
                {
                    changes |= setting!.FixInvalidSettings( mod.Meta );
                }

                if( changes )
                {
                    collection.Save();
                }
            }
        }

        private void ReadCollections()
        {
            var collectionDir = new DirectoryInfo( CollectionDirectory );
            var inheritances  = new List< IReadOnlyList< string > >();
            if( collectionDir.Exists )
            {
                foreach( var file in collectionDir.EnumerateFiles( "*.json" ) )
                {
                    var collection = LoadFromFile( file, out var inheritance );
                    if( collection == null || collection.Name.Length == 0 )
                    {
                        continue;
                    }

                    if( file.Name != $"{collection.Name.RemoveInvalidPathSymbols()}.json" )
                    {
                        PluginLog.Warning( $"Collection {file.Name} does not correspond to {collection.Name}." );
                    }

                    if( this[ collection.Name ] != null )
                    {
                        PluginLog.Warning( $"Duplicate collection found: {collection.Name} already exists." );
                    }
                    else
                    {
                        inheritances.Add( inheritance );
                        collection.Index = _collections.Count;
                        _collections.Add( collection );
                    }
                }
            }

            AddDefaultCollection();
            ApplyInheritancesAndFixSettings( inheritances );
        }

        public bool AddCollection( string name, ModCollection? duplicate )
        {
            var nameFixed = name.RemoveInvalidPathSymbols().ToLowerInvariant();
            if( nameFixed.Length == 0
            || nameFixed         == Empty.Name.ToLowerInvariant()
            || _collections.Any( c => c.Name.RemoveInvalidPathSymbols().ToLowerInvariant() == nameFixed ) )
            {
                PluginLog.Warning( $"The new collection {name} would lead to the same path as one that already exists." );
                return false;
            }

            var newCollection = duplicate?.Duplicate( name ) ?? CreateNewEmpty( name );
            newCollection.Index = _collections.Count;
            _collections.Add( newCollection );
            newCollection.Save();
            CollectionChanged?.Invoke( null, newCollection, Type.Inactive );
            SetCollection( newCollection.Index, Type.Current );
            return true;
        }

        public bool RemoveCollection( ModCollection collection )
            => RemoveCollection( collection.Index );

        public bool RemoveCollection( int idx )
        {
            if( idx <= Empty.Index || idx >= _collections.Count )
            {
                PluginLog.Error( "Can not remove the empty collection." );
                return false;
            }

            if( idx == _defaultNameIdx )
            {
                PluginLog.Error( "Can not remove the default collection." );
                return false;
            }

            if( idx == _currentIdx )
            {
                SetCollection( _defaultNameIdx, Type.Current );
            }
            else if( _currentIdx > idx )
            {
                --_currentIdx;
            }

            if( idx == _defaultIdx )
            {
                SetCollection( -1, Type.Default );
            }
            else if( _defaultIdx > idx )
            {
                --_defaultIdx;
            }

            if( _defaultNameIdx > idx )
            {
                --_defaultNameIdx;
            }

            foreach( var (characterName, characterIdx) in _character.ToList() )
            {
                if( idx == characterIdx )
                {
                    SetCollection( -1, Type.Character, characterName );
                }
                else if( characterIdx > idx )
                {
                    _character[ characterName ] = characterIdx - 1;
                }
            }

            var collection = _collections[ idx ];
            collection.Delete();
            _collections.RemoveAt( idx );
            for( var i = idx; i < _collections.Count; ++i )
            {
                --_collections[ i ].Index;
            }

            CollectionChanged?.Invoke( collection, null, Type.Inactive );
            return true;
        }
    }
}