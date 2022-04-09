using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Collections;

public partial class ModCollection
{
    public enum Type : byte
    {
        Inactive,  // A collection was added or removed
        Default,   // The default collection was changed
        Character, // A character collection was changed
        Current,   // The current collection was changed.
    }

    public sealed partial class Manager : IDisposable, IEnumerable< ModCollection >
    {
        // On addition, oldCollection is null. On deletion, newCollection is null.
        // CharacterName is onls set for type == Character.
        public delegate void CollectionChangeDelegate( Type type, ModCollection? oldCollection, ModCollection? newCollection,
            string? characterName = null );

        private readonly Mod.Manager _modManager;

        // The empty collection is always available and always has index 0.
        // It can not be deleted or moved.
        private readonly List< ModCollection > _collections = new()
        {
            Empty,
        };

        public ModCollection this[ Index idx ]
            => _collections[ idx ];

        public ModCollection? this[ string name ]
            => ByName( name, out var c ) ? c : null;

        public int Count
            => _collections.Count;

        // Obtain a collection case-independently by name. 
        public bool ByName( string name, [NotNullWhen( true )] out ModCollection? collection )
            => _collections.FindFirst( c => string.Equals( c.Name, name, StringComparison.InvariantCultureIgnoreCase ), out collection );

        // Default enumeration skips the empty collection.
        public IEnumerator< ModCollection > GetEnumerator()
            => _collections.Skip( 1 ).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public Manager( Mod.Manager manager )
        {
            _modManager = manager;

            // The collection manager reacts to changes in mods by itself.
            _modManager.ModDiscoveryStarted  += OnModDiscoveryStarted;
            _modManager.ModDiscoveryFinished += OnModDiscoveryFinished;
            _modManager.ModChange            += OnModChanged;
            CollectionChanged                += SaveOnChange;
            ReadCollections();
            LoadCollections();
        }

        public void Dispose()
        {
            _modManager.ModDiscoveryStarted  -= OnModDiscoveryStarted;
            _modManager.ModDiscoveryFinished -= OnModDiscoveryFinished;
            _modManager.ModChange            -= OnModChanged;
        }

        // Add a new collection of the given name.
        // If duplicate is not-null, the new collection will be a duplicate of it.
        // If the name of the collection would result in an already existing filename, skip it.
        // Returns true if the collection was successfully created and fires a Inactive event.
        // Also sets the current collection to the new collection afterwards.
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
            CollectionChanged.Invoke( Type.Inactive, null, newCollection );
            SetCollection( newCollection.Index, Type.Current );
            return true;
        }

        // Remove the given collection if it exists and is neither the empty nor the default-named collection.
        // If the removed collection was active, it also sets the corresponding collection to the appropriate default.
        public bool RemoveCollection( int idx )
        {
            if( idx <= Empty.Index || idx >= _collections.Count )
            {
                PluginLog.Error( "Can not remove the empty collection." );
                return false;
            }

            if( idx == DefaultName.Index )
            {
                PluginLog.Error( "Can not remove the default collection." );
                return false;
            }

            if( idx == Current.Index )
            {
                SetCollection( DefaultName, Type.Current );
            }

            if( idx == Default.Index )
            {
                SetCollection( Empty, Type.Default );
            }

            foreach( var (characterName, _) in _characters.Where( c => c.Value.Index == idx ).ToList() )
            {
                SetCollection( Empty, Type.Character, characterName );
            }

            var collection = _collections[ idx ];
            collection.Delete();
            _collections.RemoveAt( idx );
            for( var i = idx; i < _collections.Count; ++i )
            {
                --_collections[ i ].Index;
            }

            CollectionChanged.Invoke( Type.Inactive, collection, null );
            return true;
        }

        public bool RemoveCollection( ModCollection collection )
            => RemoveCollection( collection.Index );

        private void OnModDiscoveryStarted()
        {
            foreach( var collection in this )
            {
                collection.PrepareModDiscovery();
            }
        }

        private void OnModDiscoveryFinished()
        {
            // First, re-apply all mod settings.
            foreach( var collection in this )
            {
                collection.ApplyModSettings();
            }

            // Afterwards, we update the caches. This can not happen in the same loop due to inheritance.
            foreach( var collection in this )
            {
                collection.ForceCacheUpdate( collection == Default );
            }
        }


        // A changed mod forces changes for all collections, active and inactive.
        private void OnModChanged( Mod.ChangeType type, Mod mod )
        {
            switch( type )
            {
                case Mod.ChangeType.Added:
                    foreach( var collection in this )
                    {
                        collection.AddMod( mod );
                    }

                    OnModAddedActive( mod.Resources.MetaManipulations.Count > 0 );
                    break;
                case Mod.ChangeType.Removed:
                    var settings = new List< ModSettings? >( _collections.Count );
                    foreach( var collection in this )
                    {
                        settings.Add( collection[ mod.Index ].Settings );
                        collection.RemoveMod( mod, mod.Index );
                    }

                    OnModRemovedActive( mod.Resources.MetaManipulations.Count > 0, settings );
                    break;
                case Mod.ChangeType.Changed:
                    foreach( var collection in this.Where(
                                collection => collection.Settings[ mod.Index ]?.FixInvalidSettings( mod.Meta ) ?? false ) )
                    {
                        collection.Save();
                    }

                    OnModChangedActive( mod.Resources.MetaManipulations.Count > 0, mod.Index );
                    break;
                default: throw new ArgumentOutOfRangeException( nameof( type ), type, null );
            }
        }

        // Add the collection with the default name if it does not exist.
        // It should always be ensured that it exists, otherwise it will be created.
        // This can also not be deleted, so there are always at least the empty and a collection with default name.
        private void AddDefaultCollection()
        {
            var idx = GetIndexForCollectionName( DefaultCollection );
            if( idx >= 0 )
            {
                DefaultName = this[ idx ];
                return;
            }

            var defaultCollection = CreateNewEmpty( DefaultCollection );
            defaultCollection.Save();
            defaultCollection.Index = _collections.Count;
            _collections.Add( defaultCollection );
        }

        // Inheritances can not be setup before all collections are read,
        // so this happens after reading the collections.
        private void ApplyInheritances( IEnumerable< IReadOnlyList< string > > inheritances )
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

                if( changes )
                {
                    collection.Save();
                }
            }
        }

        // Read all collection files in the Collection Directory.
        // Ensure that the default named collection exists, and apply inheritances afterwards.
        // Duplicate collection files are not deleted, just not added here.
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
            ApplyInheritances( inheritances );
        }
    }
}