using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Mods;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Penumbra.GameData.Actors;

namespace Penumbra.Collections;

public partial class ModCollection
{
    public sealed partial class Manager : IDisposable, IEnumerable< ModCollection >
    {
        // On addition, oldCollection is null. On deletion, newCollection is null.
        // displayName is only set for type == Individual.
        public delegate void CollectionChangeDelegate( CollectionType collectionType, ModCollection? oldCollection,
            ModCollection? newCollection, string displayName = "" );

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
            => _collections.FindFirst( c => string.Equals( c.Name, name, StringComparison.OrdinalIgnoreCase ), out collection );

        // Default enumeration skips the empty collection.
        public IEnumerator< ModCollection > GetEnumerator()
            => _collections.Skip( 1 ).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public IEnumerable< ModCollection > GetEnumeratorWithEmpty()
            => _collections;

        public Manager( Mod.Manager manager )
        {
            _modManager = manager;

            // The collection manager reacts to changes in mods by itself.
            _modManager.ModDiscoveryStarted  += OnModDiscoveryStarted;
            _modManager.ModDiscoveryFinished += OnModDiscoveryFinished;
            _modManager.ModOptionChanged     += OnModOptionsChanged;
            _modManager.ModPathChanged       += OnModPathChange;
            CollectionChanged                += SaveOnChange;
            ReadCollections();
            LoadCollections();
            UpdateCurrentCollectionInUse();
        }

        public void Dispose()
        {
            _modManager.ModDiscoveryStarted  -= OnModDiscoveryStarted;
            _modManager.ModDiscoveryFinished -= OnModDiscoveryFinished;
            _modManager.ModOptionChanged     -= OnModOptionsChanged;
            _modManager.ModPathChanged       -= OnModPathChange;
        }

        // Returns true if the name is not empty, it is not the name of the empty collection
        // and no existing collection results in the same filename as name.
        public bool CanAddCollection( string name, out string fixedName )
        {
            if( !IsValidName( name ) )
            {
                fixedName = string.Empty;
                return false;
            }

            name = name.RemoveInvalidPathSymbols().ToLowerInvariant();
            if( name.Length == 0
            || name         == Empty.Name.ToLowerInvariant()
            || _collections.Any( c => c.Name.RemoveInvalidPathSymbols().ToLowerInvariant() == name ) )
            {
                fixedName = string.Empty;
                return false;
            }

            fixedName = name;
            return true;
        }

        // Add a new collection of the given name.
        // If duplicate is not-null, the new collection will be a duplicate of it.
        // If the name of the collection would result in an already existing filename, skip it.
        // Returns true if the collection was successfully created and fires a Inactive event.
        // Also sets the current collection to the new collection afterwards.
        public bool AddCollection( string name, ModCollection? duplicate )
        {
            if( !CanAddCollection( name, out var fixedName ) )
            {
                Penumbra.Log.Warning( $"The new collection {name} would lead to the same path {fixedName} as one that already exists." );
                return false;
            }

            var newCollection = duplicate?.Duplicate( name ) ?? CreateNewEmpty( name );
            newCollection.Index = _collections.Count;
            _collections.Add( newCollection );
            newCollection.Save();
            Penumbra.Log.Debug( $"Added collection {newCollection.AnonymizedName}." );
            CollectionChanged.Invoke( CollectionType.Inactive, null, newCollection );
            SetCollection( newCollection.Index, CollectionType.Current );
            return true;
        }

        // Remove the given collection if it exists and is neither the empty nor the default-named collection.
        // If the removed collection was active, it also sets the corresponding collection to the appropriate default.
        // Also removes the collection from inheritances of all other collections.
        public bool RemoveCollection( int idx )
        {
            if( idx <= Empty.Index || idx >= _collections.Count )
            {
                Penumbra.Log.Error( "Can not remove the empty collection." );
                return false;
            }

            if( idx == DefaultName.Index )
            {
                Penumbra.Log.Error( "Can not remove the default collection." );
                return false;
            }

            if( idx == Current.Index )
            {
                SetCollection( DefaultName.Index, CollectionType.Current );
            }

            if( idx == Default.Index )
            {
                SetCollection( Empty.Index, CollectionType.Default );
            }

            for( var i = 0; i < Individuals.Count; ++i )
            {
                if( Individuals[ i ].Collection.Index == idx )
                {
                    Individuals.ChangeCollection( i, Empty );
                }
            }
            var collection = _collections[ idx ];

            // Clear own inheritances.
            foreach( var inheritance in collection.Inheritance )
            {
                collection.ClearSubscriptions( inheritance );
            }

            collection.Delete();
            _collections.RemoveAt( idx );

            // Clear external inheritances.
            foreach( var c in _collections )
            {
                var inheritedIdx = c._inheritance.IndexOf( collection );
                if( inheritedIdx >= 0 )
                {
                    c.RemoveInheritance( inheritedIdx );
                }

                if( c.Index > idx )
                {
                    --c.Index;
                }
            }

            Penumbra.Log.Debug( $"Removed collection {collection.AnonymizedName}." );
            CollectionChanged.Invoke( CollectionType.Inactive, collection, null );
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
            foreach( var collection in this.Where( c => c.HasCache ) )
            {
                collection.ForceCacheUpdate();
            }
        }


        // A changed mod path forces changes for all collections, active and inactive.
        private void OnModPathChange( ModPathChangeType type, Mod mod, DirectoryInfo? oldDirectory,
            DirectoryInfo? newDirectory )
        {
            switch( type )
            {
                case ModPathChangeType.Added:
                    foreach( var collection in this )
                    {
                        collection.AddMod( mod );
                    }

                    OnModAddedActive( mod );
                    break;
                case ModPathChangeType.Deleted:
                    OnModRemovedActive( mod );
                    foreach( var collection in this )
                    {
                        collection.RemoveMod( mod, mod.Index );
                    }

                    break;
                case ModPathChangeType.Moved:
                    OnModMovedActive( mod );
                    foreach( var collection in this.Where( collection => collection.Settings[ mod.Index ] != null ) )
                    {
                        collection.Save();
                    }

                    break;
                case ModPathChangeType.StartingReload:
                    OnModRemovedActive( mod );
                    break;
                case ModPathChangeType.Reloaded:
                    OnModAddedActive( mod );
                    break;
                default: throw new ArgumentOutOfRangeException( nameof( type ), type, null );
            }
        }

        // Automatically update all relevant collections when a mod is changed.
        // This means saving if options change in a way where the settings may change and the collection has settings for this mod.
        // And also updating effective file and meta manipulation lists if necessary.
        private void OnModOptionsChanged( ModOptionChangeType type, Mod mod, int groupIdx, int optionIdx, int movedToIdx )
        {
            // Handle changes that break revertability.
            if( type == ModOptionChangeType.PrepareChange )
            {
                foreach( var collection in this.Where( c => c.HasCache ) )
                {
                    if( collection[ mod.Index ].Settings is { Enabled: true } )
                    {
                        collection._cache!.RemoveMod( mod, false );
                    }
                }

                return;
            }

            type.HandlingInfo( out var requiresSaving, out var recomputeList, out var reload );

            // Handle changes that require overwriting the collection.
            if( requiresSaving )
            {
                foreach( var collection in this )
                {
                    if( collection._settings[ mod.Index ]?.HandleChanges( type, mod, groupIdx, optionIdx, movedToIdx ) ?? false )
                    {
                        collection.Save();
                    }
                }
            }

            // Handle changes that reload the mod if the changes did not need to be prepared,
            // or re-add the mod if they were prepared.
            if( recomputeList )
            {
                foreach( var collection in this.Where( c => c.HasCache ) )
                {
                    if( collection[ mod.Index ].Settings is { Enabled: true } )
                    {
                        if( reload )
                        {
                            collection._cache!.ReloadMod( mod, true );
                        }
                        else
                        {
                            collection._cache!.AddMod( mod, true );
                        }
                    }
                }
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
                        Penumbra.Log.Warning( $"Inherited collection {subCollectionName} for {collection.Name} does not exist, removed." );
                    }
                    else if( !collection.AddInheritance( subCollection, false ) )
                    {
                        changes = true;
                        Penumbra.Log.Warning( $"{collection.Name} can not inherit from {subCollectionName}, removed." );
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
                        Penumbra.Log.Warning( $"Collection {file.Name} does not correspond to {collection.Name}." );
                    }

                    if( this[ collection.Name ] != null )
                    {
                        Penumbra.Log.Warning( $"Duplicate collection found: {collection.Name} already exists." );
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