using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Logging;
using Penumbra.Meta.Manager;
using Penumbra.Mod;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Collections;

public sealed partial class CollectionManager2
{
    // Is invoked after the collections actually changed.
    public event CollectionChangeDelegate? CollectionChanged;

    private int _currentIdx     = -1;
    private int _defaultIdx     = -1;
    private int _defaultNameIdx = 0;

    public ModCollection2 Current
        => this[ _currentIdx ];

    public ModCollection2 Default
        => this[ _defaultIdx ];

    private readonly Dictionary< string, int > _character = new();

    public ModCollection2 Character( string name )
        => _character.TryGetValue( name, out var idx ) ? _collections[ idx ] : Default;

    public bool HasCharacterCollections
        => _character.Count > 0;

    private void OnModChanged( ModChangeType type, int idx, ModData mod )
    {
        switch( type )
        {
            case ModChangeType.Added:
                foreach( var collection in _collections )
                {
                    collection.AddMod( mod );
                }

                foreach( var collection in _collections.Where( c => c.HasCache && c[ ^1 ].Settings?.Enabled == true ) )
                {
                    collection.UpdateCache();
                }

                break;
            case ModChangeType.Removed:
                var list = new List< ModSettings? >( _collections.Count );
                foreach( var collection in _collections )
                {
                    list.Add( collection[ idx ].Settings );
                    collection.RemoveMod( mod, idx );
                }

                foreach( var (collection, _) in _collections.Zip( list ).Where( c => c.First.HasCache && c.Second?.Enabled == true ) )
                {
                    collection.UpdateCache();
                }

                break;
            case ModChangeType.Changed:
                foreach( var collection in _collections.Where(
                            collection => collection.Settings[ idx ]?.FixInvalidSettings( mod.Meta ) ?? false ) )
                {
                    collection.Save();
                }

                foreach( var collection in _collections.Where( c => c.HasCache && c[ idx ].Settings?.Enabled == true ) )
                {
                    collection.UpdateCache();
                }

                break;
            default: throw new ArgumentOutOfRangeException( nameof( type ), type, null );
        }
    }

    private void CreateNecessaryCaches()
    {
        if( _defaultIdx >= 0 )
        {
            Default.CreateCache();
        }

        if( _currentIdx >= 0 )
        {
            Current.CreateCache();
        }

        foreach( var idx in _character.Values.Where( i => i >= 0 ) )
        {
            _collections[ idx ].CreateCache();
        }
    }

    public void UpdateCaches()
    {
        foreach( var collection in _collections )
        {
            collection.UpdateCache();
        }
    }

    private void RemoveCache( int idx )
    {
        if( idx != _defaultIdx && idx != _currentIdx && _character.All( kvp => kvp.Value != idx ) )
        {
            _collections[ idx ].ClearCache();
        }
    }

    public void SetCollection( string name, CollectionType type, string? characterName = null )
        => SetCollection( GetIndexForCollectionName( name ), type, characterName );

    public void SetCollection( ModCollection2 collection, CollectionType type, string? characterName = null )
        => SetCollection( GetIndexForCollectionName( collection.Name ), type, characterName );

    public void SetCollection( int newIdx, CollectionType type, string? characterName = null )
    {
        var oldCollectionIdx = type switch
        {
            CollectionType.Default => _defaultIdx,
            CollectionType.Current => _currentIdx,
            CollectionType.Character => characterName?.Length > 0
                ? _character.TryGetValue( characterName, out var c )
                    ? c
                    : _defaultIdx
                : -2,
            _ => -2,
        };

        if( oldCollectionIdx == -2 || newIdx == oldCollectionIdx )
        {
            return;
        }

        var newCollection = this[ newIdx ];
        if( newIdx >= 0 )
        {
            newCollection.CreateCache();
        }

        RemoveCache( oldCollectionIdx );
        switch( type )
        {
            case CollectionType.Default:
                _defaultIdx                       = newIdx;
                Penumbra.Config.DefaultCollection = newCollection.Name;
                Penumbra.ResidentResources.Reload();
                Default.SetFiles();
                break;
            case CollectionType.Current:
                _currentIdx                       = newIdx;
                Penumbra.Config.CurrentCollection = newCollection.Name;
                break;
            case CollectionType.Character:
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

        _character[ characterName ]                           = -1;
        Penumbra.Config.CharacterCollections[ characterName ] = ModCollection2.Empty.Name;
        Penumbra.Config.Save();
        CollectionChanged?.Invoke( null, ModCollection2.Empty, CollectionType.Character, characterName );
        return true;
    }

    public void RemoveCharacterCollection( string characterName )
    {
        if( _character.TryGetValue( characterName, out var collection ) )
        {
            RemoveCache( collection );
            _character.Remove( characterName );
            CollectionChanged?.Invoke( this[ collection ], null, CollectionType.Character, characterName );
        }

        if( Penumbra.Config.CharacterCollections.Remove( characterName ) )
        {
            Penumbra.Config.Save();
        }
    }

    private int GetIndexForCollectionName( string name )
    {
        if( name.Length == 0 || name == ModCollection2.DefaultCollection )
        {
            return -1;
        }

        var idx = _collections.IndexOf( c => c.Name == Penumbra.Config.DefaultCollection );
        return idx < 0 ? -2 : idx;
    }

    public void LoadCollections()
    {
        var configChanged = false;
        _defaultIdx = GetIndexForCollectionName( Penumbra.Config.DefaultCollection );
        if( _defaultIdx == -2 )
        {
            PluginLog.Error( $"Last choice of Default Collection {Penumbra.Config.DefaultCollection} is not available, reset to None." );
            _defaultIdx                       = -1;
            Penumbra.Config.DefaultCollection = this[ _defaultIdx ].Name;
            configChanged                     = true;
        }

        _currentIdx = GetIndexForCollectionName( Penumbra.Config.CurrentCollection );
        if( _currentIdx == -2 )
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
            if( idx == -2 )
            {
                PluginLog.Error( $"Last choice of <{player}>'s Collection {collectionName} is not available, reset to None." );
                _character.Add( player, -1 );
                Penumbra.Config.CharacterCollections[ player ] = ModCollection2.Empty.Name;
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