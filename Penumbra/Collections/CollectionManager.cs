using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Collections;

public enum CollectionType : byte
{
    Inactive,
    Default,
    Character,
    Current,
}

public sealed partial class CollectionManager2 : IDisposable, IEnumerable< ModCollection2 >
{
    public delegate void CollectionChangeDelegate( ModCollection2? oldCollection, ModCollection2? newCollection, CollectionType type,
        string? characterName = null );

    private readonly ModManager _modManager;

    private readonly List< ModCollection2 > _collections = new();

    public ModCollection2 this[ Index idx ]
        => idx.Value == -1 ? ModCollection2.Empty : _collections[ idx ];

    public ModCollection2? this[ string name ]
        => ByName( name, out var c ) ? c : null;

    public bool ByName( string name, [NotNullWhen( true )] out ModCollection2? collection )
        => _collections.FindFirst( c => string.Equals( c.Name, name, StringComparison.InvariantCultureIgnoreCase ), out collection );

    public IEnumerator< ModCollection2 > GetEnumerator()
        => _collections.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public CollectionManager2( ModManager manager )
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
        UpdateCaches();
        Default.SetFiles();
    }

    private void AddDefaultCollection()
    {
        var idx = _collections.IndexOf( c => c.Name == ModCollection2.DefaultCollection );
        if( idx >= 0 )
        {
            _defaultNameIdx = idx;
            return;
        }

        var defaultCollection = ModCollection2.CreateNewEmpty( ModCollection2.DefaultCollection );
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
        var collectionDir = new DirectoryInfo( ModCollection2.CollectionDirectory );
        var inheritances  = new List< IReadOnlyList< string > >();
        if( collectionDir.Exists )
        {
            foreach( var file in collectionDir.EnumerateFiles( "*.json" ) )
            {
                var collection = ModCollection2.LoadFromFile( file, out var inheritance );
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
                    _collections.Add( collection );
                }
            }
        }

        AddDefaultCollection();
        ApplyInheritancesAndFixSettings( inheritances );
    }

    public bool AddCollection( string name, ModCollection2? duplicate )
    {
        var nameFixed = name.RemoveInvalidPathSymbols().ToLowerInvariant();
        if( nameFixed.Length == 0
        || nameFixed         == ModCollection2.Empty.Name.ToLowerInvariant()
        || _collections.Any( c => c.Name.RemoveInvalidPathSymbols().ToLowerInvariant() == nameFixed ) )
        {
            PluginLog.Warning( $"The new collection {name} would lead to the same path as one that already exists." );
            return false;
        }

        var newCollection = duplicate?.Duplicate( name ) ?? ModCollection2.CreateNewEmpty( name );
        _collections.Add( newCollection );
        newCollection.Save();
        CollectionChanged?.Invoke( null, newCollection, CollectionType.Inactive );
        SetCollection( _collections.Count - 1, CollectionType.Current );
        return true;
    }

    public bool RemoveCollection( int idx )
    {
        if( idx < 0 || idx >= _collections.Count )
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
            SetCollection( _defaultNameIdx, CollectionType.Current );
        }
        else if( _currentIdx > idx )
        {
            --_currentIdx;
        }

        if( idx == _defaultIdx )
        {
            SetCollection( -1, CollectionType.Default );
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
                SetCollection( -1, CollectionType.Character, characterName );
            }
            else if( characterIdx > idx )
            {
                _character[ characterName ] = characterIdx - 1;
            }
        }

        var collection = _collections[ idx ];
        collection.Delete();
        _collections.RemoveAt( idx );
        CollectionChanged?.Invoke( collection, null, CollectionType.Inactive );
        return true;
    }
}