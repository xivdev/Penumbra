using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Data;
using Newtonsoft.Json;
using OtterGui;
using Penumbra.Collections;
using Penumbra.Interop.Resolver;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Penumbra.Api.Enums;
using Penumbra.GameData.Actors;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Api;

public class PenumbraApi : IDisposable, IPenumbraApi
{
    public (int, int) ApiVersion
        => ( 4, 17 );

    private Penumbra?        _penumbra;
    private Lumina.GameData? _lumina;

    private readonly Dictionary< ModCollection, ModCollection.ModSettingChangeDelegate > _delegates = new();

    public event Action< string >? PreSettingsPanelDraw;
    public event Action< string >? PostSettingsPanelDraw;

    public event GameObjectRedrawnDelegate? GameObjectRedrawn
    {
        add
        {
            CheckInitialized();
            _penumbra!.ObjectReloader.GameObjectRedrawn += value;
        }
        remove
        {
            CheckInitialized();
            _penumbra!.ObjectReloader.GameObjectRedrawn -= value;
        }
    }

    public event ModSettingChangedDelegate? ModSettingChanged;

    public event CreatingCharacterBaseDelegate? CreatingCharacterBase
    {
        add
        {
            CheckInitialized();
            PathResolver.DrawObjectState.CreatingCharacterBase += value;
        }
        remove
        {
            CheckInitialized();
            PathResolver.DrawObjectState.CreatingCharacterBase -= value;
        }
    }

    public event CreatedCharacterBaseDelegate? CreatedCharacterBase
    {
        add
        {
            CheckInitialized();
            PathResolver.DrawObjectState.CreatedCharacterBase += value;
        }
        remove
        {
            CheckInitialized();
            PathResolver.DrawObjectState.CreatedCharacterBase -= value;
        }
    }

    public bool Valid
        => _penumbra != null;

    public unsafe PenumbraApi( Penumbra penumbra )
    {
        _penumbra = penumbra;
        _lumina = ( Lumina.GameData? )Dalamud.GameData.GetType()
           .GetField( "gameData", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( Dalamud.GameData );
        foreach( var collection in Penumbra.CollectionManager )
        {
            SubscribeToCollection( collection );
        }

        Penumbra.CollectionManager.CollectionChanged += SubscribeToNewCollections;
        Penumbra.ResourceLoader.ResourceLoaded       += OnResourceLoaded;
        Penumbra.ModManager.ModPathChanged           += ModPathChangeSubscriber;
    }

    public unsafe void Dispose()
    {
        Penumbra.ResourceLoader.ResourceLoaded       -= OnResourceLoaded;
        Penumbra.CollectionManager.CollectionChanged -= SubscribeToNewCollections;
        Penumbra.ModManager.ModPathChanged           -= ModPathChangeSubscriber;
        _penumbra                                    =  null;
        _lumina                                      =  null;
        foreach( var collection in Penumbra.CollectionManager )
        {
            if( _delegates.TryGetValue( collection, out var del ) )
            {
                collection.ModSettingChanged -= del;
            }
        }
    }

    public event ChangedItemClick? ChangedItemClicked;

    public string GetModDirectory()
    {
        CheckInitialized();
        return Penumbra.Config.ModDirectory;
    }

    private unsafe void OnResourceLoaded( ResourceHandle* _, Utf8GamePath originalPath, FullPath? manipulatedPath,
        ResolveData resolveData )
    {
        if( resolveData.AssociatedGameObject != IntPtr.Zero )
        {
            GameObjectResourceResolved?.Invoke( resolveData.AssociatedGameObject, originalPath.ToString(),
                manipulatedPath?.ToString() ?? originalPath.ToString() );
        }
    }

    public event Action< string, bool >? ModDirectoryChanged
    {
        add
        {
            CheckInitialized();
            Penumbra.ModManager.ModDirectoryChanged += value;
        }
        remove
        {
            CheckInitialized();
            Penumbra.ModManager.ModDirectoryChanged -= value;
        }
    }

    public bool GetEnabledState()
        => Penumbra.Config.EnableMods;

    public event Action< bool >? EnabledChange
    {
        add
        {
            CheckInitialized();
            _penumbra!.EnabledChange += value;
        }
        remove
        {
            CheckInitialized();
            _penumbra!.EnabledChange -= value;
        }
    }

    public string GetConfiguration()
    {
        CheckInitialized();
        return JsonConvert.SerializeObject( Penumbra.Config, Formatting.Indented );
    }

    public event ChangedItemHover? ChangedItemTooltip;
    public event GameObjectResourceResolvedDelegate? GameObjectResourceResolved;

    public void RedrawObject( int tableIndex, RedrawType setting )
    {
        CheckInitialized();
        _penumbra!.ObjectReloader.RedrawObject( tableIndex, setting );
    }

    public void RedrawObject( string name, RedrawType setting )
    {
        CheckInitialized();
        _penumbra!.ObjectReloader.RedrawObject( name, setting );
    }

    public void RedrawObject( GameObject? gameObject, RedrawType setting )
    {
        CheckInitialized();
        _penumbra!.ObjectReloader.RedrawObject( gameObject, setting );
    }

    public void RedrawAll( RedrawType setting )
    {
        CheckInitialized();
        _penumbra!.ObjectReloader.RedrawAll( setting );
    }

    public string ResolveDefaultPath( string path )
    {
        CheckInitialized();
        return ResolvePath( path, Penumbra.ModManager, Penumbra.CollectionManager.Default );
    }

    public string ResolveInterfacePath( string path )
    {
        CheckInitialized();
        return ResolvePath( path, Penumbra.ModManager, Penumbra.CollectionManager.Interface );
    }

    public string ResolvePlayerPath( string path )
    {
        CheckInitialized();
        return ResolvePath( path, Penumbra.ModManager, PathResolver.PlayerCollection() );
    }

    // TODO: cleanup when incrementing API level
    public string ResolvePath( string path, string characterName )
        => ResolvePath( path, characterName, ushort.MaxValue );

    public string ResolveGameObjectPath( string path, int gameObjectIdx )
    {
        CheckInitialized();
        AssociatedCollection( gameObjectIdx, out var collection );
        return ResolvePath( path, Penumbra.ModManager, collection );
    }

    public string ResolvePath( string path, string characterName, ushort worldId )
    {
        CheckInitialized();
        return ResolvePath( path, Penumbra.ModManager,
            Penumbra.CollectionManager.Individual( NameToIdentifier( characterName, worldId ) ) );
    }

    // TODO: cleanup when incrementing API level
    public string[] ReverseResolvePath( string path, string characterName )
        => ReverseResolvePath( path, characterName, ushort.MaxValue );

    public string[] ReverseResolvePath( string path, string characterName, ushort worldId )
    {
        CheckInitialized();
        if( !Penumbra.Config.EnableMods )
        {
            return new[] { path };
        }

        var ret = Penumbra.CollectionManager.Individual( NameToIdentifier( characterName, worldId ) ).ReverseResolvePath( new FullPath( path ) );
        return ret.Select( r => r.ToString() ).ToArray();
    }

    public string[] ReverseResolveGameObjectPath( string path, int gameObjectIdx )
    {
        CheckInitialized();
        if( !Penumbra.Config.EnableMods )
        {
            return new[] { path };
        }

        AssociatedCollection( gameObjectIdx, out var collection );
        var ret = collection.ReverseResolvePath( new FullPath( path ) );
        return ret.Select( r => r.ToString() ).ToArray();
    }

    public string[] ReverseResolvePlayerPath( string path )
    {
        CheckInitialized();
        if( !Penumbra.Config.EnableMods )
        {
            return new[] { path };
        }

        var ret = PathResolver.PlayerCollection().ReverseResolvePath( new FullPath( path ) );
        return ret.Select( r => r.ToString() ).ToArray();
    }

    public T? GetFile< T >( string gamePath ) where T : FileResource
        => GetFileIntern< T >( ResolveDefaultPath( gamePath ) );

    public T? GetFile< T >( string gamePath, string characterName ) where T : FileResource
        => GetFileIntern< T >( ResolvePath( gamePath, characterName ) );

    public IReadOnlyDictionary< string, object? > GetChangedItemsForCollection( string collectionName )
    {
        CheckInitialized();
        try
        {
            if( !Penumbra.CollectionManager.ByName( collectionName, out var collection ) )
            {
                collection = ModCollection.Empty;
            }

            if( collection.HasCache )
            {
                return collection.ChangedItems.ToDictionary( kvp => kvp.Key, kvp => kvp.Value.Item2 );
            }

            Penumbra.Log.Warning( $"Collection {collectionName} does not exist or is not loaded." );
            return new Dictionary< string, object? >();
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not obtain Changed Items for {collectionName}:\n{e}" );
            throw;
        }
    }

    public IList< string > GetCollections()
    {
        CheckInitialized();
        return Penumbra.CollectionManager.Skip( 1 ).Select( c => c.Name ).ToArray();
    }

    public string GetCurrentCollection()
    {
        CheckInitialized();
        return Penumbra.CollectionManager.Current.Name;
    }

    public string GetDefaultCollection()
    {
        CheckInitialized();
        return Penumbra.CollectionManager.Default.Name;
    }

    public string GetInterfaceCollection()
    {
        CheckInitialized();
        return Penumbra.CollectionManager.Interface.Name;
    }

    // TODO: cleanup when incrementing API level
    public (string, bool) GetCharacterCollection( string characterName )
        => GetCharacterCollection( characterName, ushort.MaxValue );

    public (string, bool) GetCharacterCollection( string characterName, ushort worldId )
    {
        CheckInitialized();
        return Penumbra.CollectionManager.Individuals.TryGetCollection( NameToIdentifier( characterName, worldId ), out var collection )
            ? ( collection.Name, true )
            : ( Penumbra.CollectionManager.Default.Name, false );
    }

    public (IntPtr, string) GetDrawObjectInfo( IntPtr drawObject )
    {
        CheckInitialized();
        var (obj, collection) = PathResolver.IdentifyDrawObject( drawObject );
        return ( obj, collection.ModCollection.Name );
    }

    public int GetCutsceneParentIndex( int actorIdx )
    {
        CheckInitialized();
        return _penumbra!.PathResolver.CutsceneActor( actorIdx );
    }

    public IList< (string, string) > GetModList()
    {
        CheckInitialized();
        return Penumbra.ModManager.Select( m => ( m.ModPath.Name, m.Name.Text ) ).ToArray();
    }

    public IDictionary< string, (IList< string >, GroupType) >? GetAvailableModSettings( string modDirectory, string modName )
    {
        CheckInitialized();
        return Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod )
            ? mod.Groups.ToDictionary( g => g.Name, g => ( ( IList< string > )g.Select( o => o.Name ).ToList(), g.Type ) )
            : null;
    }

    public (PenumbraApiEc, (bool, int, IDictionary< string, IList< string > >, bool)?) GetCurrentModSettings( string collectionName,
        string modDirectory, string modName, bool allowInheritance )
    {
        CheckInitialized();
        if( !Penumbra.CollectionManager.ByName( collectionName, out var collection ) )
        {
            return ( PenumbraApiEc.CollectionMissing, null );
        }

        if( !Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod ) )
        {
            return ( PenumbraApiEc.ModMissing, null );
        }

        var settings = allowInheritance ? collection.Settings[ mod.Index ] : collection[ mod.Index ].Settings;
        if( settings == null )
        {
            return ( PenumbraApiEc.Success, null );
        }

        var shareSettings = settings.ConvertToShareable( mod );
        return ( PenumbraApiEc.Success,
            ( shareSettings.Enabled, shareSettings.Priority, shareSettings.Settings, collection.Settings[ mod.Index ] != null ) );
    }

    public PenumbraApiEc ReloadMod( string modDirectory, string modName )
    {
        CheckInitialized();
        if( !Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod ) )
        {
            return PenumbraApiEc.ModMissing;
        }

        Penumbra.ModManager.ReloadMod( mod.Index );
        return PenumbraApiEc.Success;
    }

    public PenumbraApiEc AddMod( string modDirectory )
    {
        CheckInitialized();
        var dir = new DirectoryInfo( Path.Join( Penumbra.ModManager.BasePath.FullName, Path.GetFileName( modDirectory ) ) );
        if( !dir.Exists )
        {
            return PenumbraApiEc.FileMissing;
        }

        Penumbra.ModManager.AddMod( dir );
        return PenumbraApiEc.Success;
    }

    public PenumbraApiEc DeleteMod( string modDirectory, string modName )
    {
        CheckInitialized();
        if( !Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod ) )
        {
            return PenumbraApiEc.NothingChanged;
        }

        Penumbra.ModManager.DeleteMod( mod.Index );
        return PenumbraApiEc.Success;
    }

    public event Action< string >? ModDeleted;
    public event Action< string >? ModAdded;
    public event Action< string, string >? ModMoved;

    private void ModPathChangeSubscriber( ModPathChangeType type, Mod mod, DirectoryInfo? oldDirectory,
        DirectoryInfo? newDirectory )
    {
        switch( type )
        {
            case ModPathChangeType.Deleted when oldDirectory != null:
                ModDeleted?.Invoke( oldDirectory.Name );
                break;
            case ModPathChangeType.Added when newDirectory != null:
                ModAdded?.Invoke( newDirectory.Name );
                break;
            case ModPathChangeType.Moved when newDirectory != null && oldDirectory != null:
                ModMoved?.Invoke( oldDirectory.Name, newDirectory.Name );
                break;
        }
    }

    public (PenumbraApiEc, string, bool) GetModPath( string modDirectory, string modName )
    {
        CheckInitialized();
        if( !Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod )
        || !_penumbra!.ModFileSystem.FindLeaf( mod, out var leaf ) )
        {
            return ( PenumbraApiEc.ModMissing, string.Empty, false );
        }

        var fullPath = leaf.FullName();

        return ( PenumbraApiEc.Success, fullPath, !ModFileSystem.ModHasDefaultPath( mod, fullPath ) );
    }

    public PenumbraApiEc SetModPath( string modDirectory, string modName, string newPath )
    {
        CheckInitialized();
        if( newPath.Length == 0 )
        {
            return PenumbraApiEc.InvalidArgument;
        }

        if( !Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod )
        || !_penumbra!.ModFileSystem.FindLeaf( mod, out var leaf ) )
        {
            return PenumbraApiEc.ModMissing;
        }

        try
        {
            _penumbra.ModFileSystem.RenameAndMove( leaf, newPath );
            return PenumbraApiEc.Success;
        }
        catch
        {
            return PenumbraApiEc.PathRenameFailed;
        }
    }

    public PenumbraApiEc TryInheritMod( string collectionName, string modDirectory, string modName, bool inherit )
    {
        CheckInitialized();
        if( !Penumbra.CollectionManager.ByName( collectionName, out var collection ) )
        {
            return PenumbraApiEc.CollectionMissing;
        }

        if( !Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod ) )
        {
            return PenumbraApiEc.ModMissing;
        }


        return collection.SetModInheritance( mod.Index, inherit ) ? PenumbraApiEc.Success : PenumbraApiEc.NothingChanged;
    }

    public PenumbraApiEc TrySetMod( string collectionName, string modDirectory, string modName, bool enabled )
    {
        CheckInitialized();
        if( !Penumbra.CollectionManager.ByName( collectionName, out var collection ) )
        {
            return PenumbraApiEc.CollectionMissing;
        }

        if( !Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod ) )
        {
            return PenumbraApiEc.ModMissing;
        }

        return collection.SetModState( mod.Index, enabled ) ? PenumbraApiEc.Success : PenumbraApiEc.NothingChanged;
    }

    public PenumbraApiEc TrySetModPriority( string collectionName, string modDirectory, string modName, int priority )
    {
        CheckInitialized();
        if( !Penumbra.CollectionManager.ByName( collectionName, out var collection ) )
        {
            return PenumbraApiEc.CollectionMissing;
        }

        if( !Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod ) )
        {
            return PenumbraApiEc.ModMissing;
        }

        return collection.SetModPriority( mod.Index, priority ) ? PenumbraApiEc.Success : PenumbraApiEc.NothingChanged;
    }

    public PenumbraApiEc TrySetModSetting( string collectionName, string modDirectory, string modName, string optionGroupName,
        string optionName )
    {
        CheckInitialized();
        if( !Penumbra.CollectionManager.ByName( collectionName, out var collection ) )
        {
            return PenumbraApiEc.CollectionMissing;
        }

        if( !Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod ) )
        {
            return PenumbraApiEc.ModMissing;
        }

        var groupIdx = mod.Groups.IndexOf( g => g.Name == optionGroupName );
        if( groupIdx < 0 )
        {
            return PenumbraApiEc.OptionGroupMissing;
        }

        var optionIdx = mod.Groups[ groupIdx ].IndexOf( o => o.Name == optionName );
        if( optionIdx < 0 )
        {
            return PenumbraApiEc.OptionMissing;
        }

        var setting = mod.Groups[ groupIdx ].Type == GroupType.Multi ? 1u << optionIdx : ( uint )optionIdx;

        return collection.SetModSetting( mod.Index, groupIdx, setting ) ? PenumbraApiEc.Success : PenumbraApiEc.NothingChanged;
    }

    public PenumbraApiEc TrySetModSettings( string collectionName, string modDirectory, string modName, string optionGroupName,
        IReadOnlyList< string > optionNames )
    {
        CheckInitialized();
        if( !Penumbra.CollectionManager.ByName( collectionName, out var collection ) )
        {
            return PenumbraApiEc.CollectionMissing;
        }

        if( !Penumbra.ModManager.TryGetMod( modDirectory, modName, out var mod ) )
        {
            return PenumbraApiEc.ModMissing;
        }

        var groupIdx = mod.Groups.IndexOf( g => g.Name == optionGroupName );
        if( groupIdx < 0 )
        {
            return PenumbraApiEc.OptionGroupMissing;
        }

        var group = mod.Groups[ groupIdx ];

        uint setting = 0;
        if( group.Type == GroupType.Single )
        {
            var optionIdx = optionNames.Count == 0 ? -1 : group.IndexOf( o => o.Name == optionNames[ ^1 ] );
            if( optionIdx < 0 )
            {
                return PenumbraApiEc.OptionMissing;
            }

            setting = ( uint )optionIdx;
        }
        else
        {
            foreach( var name in optionNames )
            {
                var optionIdx = group.IndexOf( o => o.Name == name );
                if( optionIdx < 0 )
                {
                    return PenumbraApiEc.OptionMissing;
                }

                setting |= 1u << optionIdx;
            }
        }

        return collection.SetModSetting( mod.Index, groupIdx, setting ) ? PenumbraApiEc.Success : PenumbraApiEc.NothingChanged;
    }


    public PenumbraApiEc CopyModSettings( string? collectionName, string modDirectoryFrom, string modDirectoryTo )
    {
        CheckInitialized();

        var sourceModIdx = Penumbra.ModManager.FirstOrDefault( m => string.Equals( m.ModPath.Name, modDirectoryFrom, StringComparison.OrdinalIgnoreCase ) )?.Index ?? -1;
        var targetModIdx = Penumbra.ModManager.FirstOrDefault( m => string.Equals( m.ModPath.Name, modDirectoryTo, StringComparison.OrdinalIgnoreCase ) )?.Index   ?? -1;
        if( string.IsNullOrEmpty( collectionName ) )
        {
            foreach( var collection in Penumbra.CollectionManager )
            {
                collection.CopyModSettings( sourceModIdx, modDirectoryFrom, targetModIdx, modDirectoryTo );
            }
        }
        else if( Penumbra.CollectionManager.ByName( collectionName, out var collection ) )
        {
            collection.CopyModSettings( sourceModIdx, modDirectoryFrom, targetModIdx, modDirectoryTo );
        }
        else
        {
            return PenumbraApiEc.CollectionMissing;
        }

        return PenumbraApiEc.Success;
    }

    public (PenumbraApiEc, string) CreateTemporaryCollection( string tag, string character, bool forceOverwriteCharacter )
    {
        CheckInitialized();

        if( !ActorManager.VerifyPlayerName( character.AsSpan() ) || tag.Length == 0 )
        {
            return ( PenumbraApiEc.InvalidArgument, string.Empty );
        }

        var identifier = NameToIdentifier( character, ushort.MaxValue );
        if( !identifier.IsValid )
        {
            return ( PenumbraApiEc.InvalidArgument, string.Empty );
        }

        if( !forceOverwriteCharacter && Penumbra.CollectionManager.Individuals.Individuals.ContainsKey( identifier )
        || Penumbra.TempMods.Collections.Individuals.ContainsKey( identifier ) )
        {
            return ( PenumbraApiEc.CharacterCollectionExists, string.Empty );
        }

        var name = $"{tag}_{character}";
        var ret  = CreateNamedTemporaryCollection( name );
        if( ret != PenumbraApiEc.Success )
        {
            return ( ret, name );
        }

        if( Penumbra.TempMods.AddIdentifier( name, identifier ) )
        {
            return ( PenumbraApiEc.Success, name );
        }

        Penumbra.TempMods.RemoveTemporaryCollection( name );
        return ( PenumbraApiEc.UnknownError, string.Empty );
    }

    public PenumbraApiEc CreateNamedTemporaryCollection( string name )
    {
        CheckInitialized();
        if( name.Length == 0 || Mod.ReplaceBadXivSymbols( name ) != name )
        {
            return PenumbraApiEc.InvalidArgument;
        }

        return Penumbra.TempMods.CreateTemporaryCollection( name ).Length > 0
            ? PenumbraApiEc.Success
            : PenumbraApiEc.CollectionExists;
    }

    public PenumbraApiEc AssignTemporaryCollection( string collectionName, int actorIndex, bool forceAssignment )
    {
        CheckInitialized();

        if( actorIndex < 0 || actorIndex >= Dalamud.Objects.Length )
        {
            return PenumbraApiEc.InvalidArgument;
        }

        var identifier = Penumbra.Actors.FromObject( Dalamud.Objects[ actorIndex ], false, false );
        if( !identifier.IsValid )
        {
            return PenumbraApiEc.InvalidArgument;
        }

        if( !Penumbra.TempMods.CollectionByName( collectionName, out var collection ) )
        {
            return PenumbraApiEc.CollectionMissing;
        }

        if( !forceAssignment
        && ( Penumbra.TempMods.Collections.Individuals.ContainsKey( identifier ) || Penumbra.CollectionManager.Individuals.Individuals.ContainsKey( identifier ) ) )
        {
            return PenumbraApiEc.CharacterCollectionExists;
        }

        var group = Penumbra.TempMods.Collections.GetGroup( identifier );
        return Penumbra.TempMods.AddIdentifier( collection, group )
            ? PenumbraApiEc.Success
            : PenumbraApiEc.UnknownError;
    }

    public PenumbraApiEc RemoveTemporaryCollection( string character )
    {
        CheckInitialized();
        return Penumbra.TempMods.RemoveByCharacterName( character )
            ? PenumbraApiEc.Success
            : PenumbraApiEc.NothingChanged;
    }

    public PenumbraApiEc RemoveTemporaryCollectionByName( string name )
    {
        CheckInitialized();
        return Penumbra.TempMods.RemoveTemporaryCollection( name )
            ? PenumbraApiEc.Success
            : PenumbraApiEc.NothingChanged;
    }

    public PenumbraApiEc AddTemporaryModAll( string tag, Dictionary< string, string > paths, string manipString, int priority )
    {
        CheckInitialized();
        if( !ConvertPaths( paths, out var p ) )
        {
            return PenumbraApiEc.InvalidGamePath;
        }

        if( !ConvertManips( manipString, out var m ) )
        {
            return PenumbraApiEc.InvalidManipulation;
        }

        return Penumbra.TempMods.Register( tag, null, p, m, priority ) switch
        {
            RedirectResult.Success => PenumbraApiEc.Success,
            _                      => PenumbraApiEc.UnknownError,
        };
    }

    public PenumbraApiEc AddTemporaryMod( string tag, string collectionName, Dictionary< string, string > paths, string manipString,
        int priority )
    {
        CheckInitialized();
        if( !Penumbra.TempMods.CollectionByName( collectionName, out var collection )
        && !Penumbra.CollectionManager.ByName( collectionName, out collection ) )
        {
            return PenumbraApiEc.CollectionMissing;
        }

        if( !ConvertPaths( paths, out var p ) )
        {
            return PenumbraApiEc.InvalidGamePath;
        }

        if( !ConvertManips( manipString, out var m ) )
        {
            return PenumbraApiEc.InvalidManipulation;
        }

        return Penumbra.TempMods.Register( tag, collection, p, m, priority ) switch
        {
            RedirectResult.Success => PenumbraApiEc.Success,
            _                      => PenumbraApiEc.UnknownError,
        };
    }

    public PenumbraApiEc RemoveTemporaryModAll( string tag, int priority )
    {
        CheckInitialized();
        return Penumbra.TempMods.Unregister( tag, null, priority ) switch
        {
            RedirectResult.Success       => PenumbraApiEc.Success,
            RedirectResult.NotRegistered => PenumbraApiEc.NothingChanged,
            _                            => PenumbraApiEc.UnknownError,
        };
    }

    public PenumbraApiEc RemoveTemporaryMod( string tag, string collectionName, int priority )
    {
        CheckInitialized();
        if( !Penumbra.TempMods.CollectionByName( collectionName, out var collection )
        && !Penumbra.CollectionManager.ByName( collectionName, out collection ) )
        {
            return PenumbraApiEc.CollectionMissing;
        }

        return Penumbra.TempMods.Unregister( tag, collection, priority ) switch
        {
            RedirectResult.Success       => PenumbraApiEc.Success,
            RedirectResult.NotRegistered => PenumbraApiEc.NothingChanged,
            _                            => PenumbraApiEc.UnknownError,
        };
    }

    public string GetPlayerMetaManipulations()
    {
        CheckInitialized();
        var collection = PathResolver.PlayerCollection();
        var set        = collection.MetaCache?.Manipulations.ToArray() ?? Array.Empty< MetaManipulation >();
        return Functions.ToCompressedBase64( set, MetaManipulation.CurrentVersion );
    }

    // TODO: cleanup when incrementing API
    public string GetMetaManipulations( string characterName )
        => GetMetaManipulations( characterName, ushort.MaxValue );

    public string GetMetaManipulations( string characterName, ushort worldId )
    {
        CheckInitialized();
        var identifier = NameToIdentifier( characterName, worldId );
        var collection = Penumbra.TempMods.Collections.TryGetCollection( identifier, out var c )
            ? c
            : Penumbra.CollectionManager.Individual( identifier );
        var set = collection.MetaCache?.Manipulations.ToArray() ?? Array.Empty< MetaManipulation >();
        return Functions.ToCompressedBase64( set, MetaManipulation.CurrentVersion );
    }

    public string GetGameObjectMetaManipulations( int gameObjectIdx )
    {
        CheckInitialized();
        AssociatedCollection( gameObjectIdx, out var collection );
        var set = collection.MetaCache?.Manipulations.ToArray() ?? Array.Empty< MetaManipulation >();
        return Functions.ToCompressedBase64( set, MetaManipulation.CurrentVersion );
    }

    internal bool HasTooltip
        => ChangedItemTooltip != null;

    internal void InvokeTooltip( object? it )
        => ChangedItemTooltip?.Invoke( it );

    internal void InvokeClick( MouseButton button, object? it )
        => ChangedItemClicked?.Invoke( button, it );


    private void CheckInitialized()
    {
        if( !Valid )
        {
            throw new Exception( "PluginShare is not initialized." );
        }
    }

    // Return the collection associated to a current game object. If it does not exist, return the default collection.
    // If the index is invalid, returns false and the default collection.
    private unsafe bool AssociatedCollection( int gameObjectIdx, out ModCollection collection )
    {
        collection = Penumbra.CollectionManager.Default;
        if( gameObjectIdx < 0 || gameObjectIdx >= Dalamud.Objects.Length )
        {
            return false;
        }

        var ptr  = ( FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* )Dalamud.Objects.GetObjectAddress( gameObjectIdx );
        var data = PathResolver.IdentifyCollection( ptr, false );
        if( data.Valid )
        {
            collection = data.ModCollection;
        }

        return true;
    }

    // Resolve a path given by string for a specific collection.
    private static string ResolvePath( string path, Mod.Manager _, ModCollection collection )
    {
        if( !Penumbra.Config.EnableMods )
        {
            return path;
        }

        var gamePath = Utf8GamePath.FromString( path, out var p, true ) ? p : Utf8GamePath.Empty;
        var ret      = collection.ResolvePath( gamePath );
        return ret?.ToString() ?? path;
    }

    // Get a file for a resolved path.
    private T? GetFileIntern< T >( string resolvedPath ) where T : FileResource
    {
        CheckInitialized();
        try
        {
            if( Path.IsPathRooted( resolvedPath ) )
            {
                return _lumina?.GetFileFromDisk< T >( resolvedPath );
            }

            return Dalamud.GameData.GetFile< T >( resolvedPath );
        }
        catch( Exception e )
        {
            Penumbra.Log.Warning( $"Could not load file {resolvedPath}:\n{e}" );
            return null;
        }
    }


    // Convert a dictionary of strings to a dictionary of gamepaths to full paths.
    // Only returns true if all paths can successfully be converted and added.
    private static bool ConvertPaths( IReadOnlyDictionary< string, string > redirections,
        [NotNullWhen( true )] out Dictionary< Utf8GamePath, FullPath >? paths )
    {
        paths = new Dictionary< Utf8GamePath, FullPath >( redirections.Count );
        foreach( var (gString, fString) in redirections )
        {
            if( !Utf8GamePath.FromString( gString, out var path, false ) )
            {
                paths = null;
                return false;
            }

            var fullPath = new FullPath( fString );
            if( !paths.TryAdd( path, fullPath ) )
            {
                paths = null;
                return false;
            }
        }

        return true;
    }

    // Convert manipulations from a transmitted base64 string to actual manipulations.
    // The empty string is treated as an empty set.
    // Only returns true if all conversions are successful and distinct.
    private static bool ConvertManips( string manipString,
        [NotNullWhen( true )] out HashSet< MetaManipulation >? manips )
    {
        if( manipString.Length == 0 )
        {
            manips = new HashSet< MetaManipulation >();
            return true;
        }

        if( Functions.FromCompressedBase64< MetaManipulation[] >( manipString, out var manipArray ) != MetaManipulation.CurrentVersion )
        {
            manips = null;
            return false;
        }

        manips = new HashSet< MetaManipulation >( manipArray!.Length );
        foreach( var manip in manipArray.Where( m => m.ManipulationType != MetaManipulation.Type.Unknown ) )
        {
            if( !manips.Add( manip ) )
            {
                manips = null;
                return false;
            }
        }

        return true;
    }

    private void SubscribeToCollection( ModCollection c )
    {
        var name = c.Name;

        void Del( ModSettingChange type, int idx, int _, int _2, bool inherited )
            => ModSettingChanged?.Invoke( type, name, idx >= 0 ? Penumbra.ModManager[ idx ].ModPath.Name : string.Empty, inherited );

        _delegates[ c ]     =  Del;
        c.ModSettingChanged += Del;
    }

    private void SubscribeToNewCollections( CollectionType type, ModCollection? oldCollection, ModCollection? newCollection, string _ )
    {
        if( type != CollectionType.Inactive )
        {
            return;
        }

        if( oldCollection != null && _delegates.TryGetValue( oldCollection, out var del ) )
        {
            oldCollection.ModSettingChanged -= del;
        }

        if( newCollection != null )
        {
            SubscribeToCollection( newCollection );
        }
    }

    public void InvokePreSettingsPanel( string modDirectory )
        => PreSettingsPanelDraw?.Invoke( modDirectory );

    public void InvokePostSettingsPanel( string modDirectory )
        => PostSettingsPanelDraw?.Invoke( modDirectory );

    // TODO: replace all usages with ActorIdentifier stuff when incrementing API
    private static ActorIdentifier NameToIdentifier( string name, ushort worldId )
    {
        // Verified to be valid name beforehand.
        var b = ByteString.FromStringUnsafe( name, false );
        return Penumbra.Actors.CreatePlayer( b, worldId );
    }
}