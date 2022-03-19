using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods;

public enum CollectionType : byte
{
    Inactive,
    Default,
    Forced,
    Character,
    Current,
}

public delegate void CollectionChangeDelegate( ModCollection? oldCollection, ModCollection? newCollection, CollectionType type,
    string? characterName = null );

// Contains all collections and respective functions, as well as the collection settings.
public class CollectionManager
{
    private readonly ModManager _manager;

    public string CollectionChangedTo { get; private set; } = string.Empty;
    public Dictionary< string, ModCollection > Collections { get; } = new(StringComparer.InvariantCultureIgnoreCase);
    public Dictionary< string, ModCollection > CharacterCollection { get; } = new();

    public ModCollection CurrentCollection { get; private set; } = ModCollection.Empty;
    public ModCollection DefaultCollection { get; private set; } = ModCollection.Empty;
    public ModCollection ForcedCollection { get; private set; } = ModCollection.Empty;
    public ModCollection ActiveCollection { get; private set; } = ModCollection.Empty;

    // Is invoked after the collections actually changed.
    public event CollectionChangeDelegate? CollectionChanged;

    public CollectionManager( ModManager manager )
    {
        _manager = manager;

        ReadCollections();
        LoadConfigCollections( Penumbra.Config );
        SetActiveCollection( DefaultCollection, string.Empty );
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
            ActiveCollection = newActive;
            Penumbra.ResidentResources.Reload();
            ActiveCollection.SetFiles();
        }
        else
        {
            ActiveCollection = newActive;
        }

        return true;
    }

    public bool ResetActiveCollection()
        => SetActiveCollection( DefaultCollection, string.Empty );

    public void CreateNecessaryCaches()
    {
        AddCache( DefaultCollection );
        AddCache( ForcedCollection );
        foreach( var (_, collection) in CharacterCollection )
        {
            AddCache( collection );
        }
    }

    public void RecreateCaches()
    {
        foreach( var collection in Collections.Values.Where( c => c.Cache != null ) )
        {
            collection.CreateCache( _manager.StructuredMods.AllMods( _manager.Config.SortFoldersFirst ) );
        }

        CreateNecessaryCaches();
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
            Penumbra.ResidentResources.Reload();
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
        CollectionChanged?.Invoke( null, newCollection, CollectionType.Inactive );
        newCollection.Save();
        SetCollection( newCollection, CollectionType.Current );
        return true;
    }

    public bool RemoveCollection( string name )
    {
        if( name == ModCollection.DefaultCollection )
        {
            PluginLog.Error( "Can not remove the default collection." );
            return false;
        }

        if( !Collections.TryGetValue( name, out var collection ) )
        {
            return false;
        }

        CollectionChanged?.Invoke( collection, null, CollectionType.Inactive );
        if( CurrentCollection == collection )
        {
            SetCollection( Collections[ ModCollection.DefaultCollection ], CollectionType.Current );
        }

        if( ForcedCollection == collection )
        {
            SetCollection( ModCollection.Empty, CollectionType.Forced );
        }

        if( DefaultCollection == collection )
        {
            SetCollection( ModCollection.Empty, CollectionType.Default );
        }

        foreach( var (characterName, characterCollection) in CharacterCollection.ToArray() )
        {
            if( characterCollection == collection )
            {
                SetCollection( ModCollection.Empty, CollectionType.Character, characterName );
            }
        }

        collection.Delete();
        Collections.Remove( name );
        return true;
    }

    private void AddCache( ModCollection collection )
    {
        if( collection.Cache == null && collection.Name != string.Empty )
        {
            collection.CreateCache( _manager.StructuredMods.AllMods( _manager.Config.SortFoldersFirst ) );
        }
    }

    private void RemoveCache( ModCollection collection )
    {
        if( collection.Name != ForcedCollection.Name
        && collection.Name  != CurrentCollection.Name
        && collection.Name  != DefaultCollection.Name
        && CharacterCollection.All( kvp => kvp.Value.Name != collection.Name ) )
        {
            collection.ClearCache();
        }
    }

    public void SetCollection( ModCollection newCollection, CollectionType type, string? characterName = null )
    {
        var oldCollection = type switch
        {
            CollectionType.Default => DefaultCollection,
            CollectionType.Forced  => ForcedCollection,
            CollectionType.Current => CurrentCollection,
            CollectionType.Character => characterName?.Length > 0
                ? CharacterCollection.TryGetValue( characterName, out var c )
                    ? c
                    : ModCollection.Empty
                : null,
            _ => null,
        };

        if( oldCollection == null || newCollection.Name == oldCollection.Name )
        {
            return;
        }

        AddCache( newCollection );
        RemoveCache( oldCollection );
        switch( type )
        {
            case CollectionType.Default:
                DefaultCollection                 = newCollection;
                Penumbra.Config.DefaultCollection = newCollection.Name;
                if( CollectionChangedTo.Length == 0 )
                {
                    SetActiveCollection( newCollection, string.Empty );
                }

                break;
            case CollectionType.Forced:
                ForcedCollection                 = newCollection;
                Penumbra.Config.ForcedCollection = newCollection.Name;
                break;
            case CollectionType.Current:
                CurrentCollection                 = newCollection;
                Penumbra.Config.CurrentCollection = newCollection.Name;
                break;
            case CollectionType.Character:
                if( CollectionChangedTo == characterName && CharacterCollection.ContainsKey( characterName ) )
                {
                    SetActiveCollection( newCollection, CollectionChangedTo );
                }

                CharacterCollection[ characterName! ]                  = newCollection;
                Penumbra.Config.CharacterCollections[ characterName! ] = newCollection.Name;

                break;
        }

        CollectionChanged?.Invoke( oldCollection, newCollection, type, characterName );

        Penumbra.Config.Save();
    }

    public bool CreateCharacterCollection( string characterName )
    {
        if( CharacterCollection.ContainsKey( characterName ) )
        {
            return false;
        }

        CharacterCollection[ characterName ]                  = ModCollection.Empty;
        Penumbra.Config.CharacterCollections[ characterName ] = string.Empty;
        Penumbra.Config.Save();
        CollectionChanged?.Invoke( null, ModCollection.Empty, CollectionType.Character, characterName );
        return true;
    }

    public void RemoveCharacterCollection( string characterName )
    {
        if( CharacterCollection.TryGetValue( characterName, out var collection ) )
        {
            RemoveCache( collection );
            CharacterCollection.Remove( characterName );
            CollectionChanged?.Invoke( collection, null, CollectionType.Character, characterName );
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
        CurrentCollection = Collections[ ModCollection.DefaultCollection ];
        if( CurrentCollection.Cache == null )
        {
            CurrentCollection.CreateCache( _manager.StructuredMods.AllMods( _manager.Config.SortFoldersFirst ) );
        }

        config.CurrentCollection = ModCollection.DefaultCollection;
        return true;
    }

    private bool LoadForcedCollection( Configuration config )
    {
        if( config.ForcedCollection.Length == 0 )
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
        if( config.DefaultCollection.Length == 0 )
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
        foreach( var (player, collectionName) in config.CharacterCollections.ToArray() )
        {
            if( collectionName.Length == 0 )
            {
                CharacterCollection.Add( player, ModCollection.Empty );
            }
            else if( Collections.TryGetValue( collectionName, out var charCollection ) )
            {
                AddCache( charCollection );
                CharacterCollection.Add( player, charCollection );
            }
            else
            {
                PluginLog.Error( $"Last choice of <{player}>'s Collection {collectionName} is not available, reset to None." );
                CharacterCollection.Add( player, ModCollection.Empty );
                config.CharacterCollections[ player ] = string.Empty;
                configChanged                         = true;
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
                if( collection == null || collection.Name == string.Empty )
                {
                    continue;
                }

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

        if( !Collections.ContainsKey( ModCollection.DefaultCollection ) )
        {
            var defaultCollection = new ModCollection();
            defaultCollection.Save();
            Collections.Add( defaultCollection.Name, defaultCollection );
        }
    }
}