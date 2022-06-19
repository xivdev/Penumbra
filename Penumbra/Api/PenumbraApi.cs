using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Lumina.Data;
using Newtonsoft.Json;
using OtterGui;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Api;

public class PenumbraApi : IDisposable, IPenumbraApi
{
    public int ApiVersion
        => 4;

    private Penumbra?        _penumbra;
    private Lumina.GameData? _lumina;
    public event GameObjectRedrawn? GameObjectRedrawn;

    public bool Valid
        => _penumbra != null;

    public PenumbraApi( Penumbra penumbra )
    {
        _penumbra = penumbra;
        _lumina = ( Lumina.GameData? )Dalamud.GameData.GetType()
           .GetField( "gameData", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( Dalamud.GameData );
        _penumbra.ObjectReloader.GameObjectRedrawn += OnGameObjectRedrawn;
    }

    public void Dispose()
    {
        _penumbra!.ObjectReloader.GameObjectRedrawn -= OnGameObjectRedrawn;
        _penumbra                                   =  null;
        _lumina                                     =  null;
    }

    public event ChangedItemClick? ChangedItemClicked;

    public string GetModDirectory()
    {
        CheckInitialized();
        return Penumbra.Config.ModDirectory;
    }

    public IPluginConfiguration GetConfiguration()
    {
        CheckInitialized();
        return JsonConvert.DeserializeObject< Configuration >( JsonConvert.SerializeObject( Penumbra.Config ) );
    }

    public event ChangedItemHover? ChangedItemTooltip;

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

    public string ResolvePath( string path )
    {
        CheckInitialized();
        return ResolvePath( path, Penumbra.ModManager, Penumbra.CollectionManager.Default );
    }

    public string ResolvePath( string path, string characterName )
    {
        CheckInitialized();
        return ResolvePath( path, Penumbra.ModManager,
            Penumbra.CollectionManager.Character( characterName ) );
    }

    public IList< string > ReverseResolvePath( string path, string characterName )
    {
        CheckInitialized();
        if( !Penumbra.Config.EnableMods )
        {
            return new[] { path };
        }

        var ret = Penumbra.CollectionManager.Character( characterName ).ReverseResolvePath( new FullPath( path ) );
        return ret.Select( r => r.ToString() ).ToList();
    }

    public T? GetFile< T >( string gamePath ) where T : FileResource
        => GetFileIntern< T >( ResolvePath( gamePath ) );

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

            PluginLog.Warning( $"Collection {collectionName} does not exist or is not loaded." );
            return new Dictionary< string, object? >();
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not obtain Changed Items for {collectionName}:\n{e}" );
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

    public (string, bool) GetCharacterCollection( string characterName )
    {
        CheckInitialized();
        return Penumbra.CollectionManager.Characters.TryGetValue( characterName, out var collection )
            ? ( collection.Name, true )
            : ( Penumbra.CollectionManager.Default.Name, false );
    }

    public (IntPtr, string) GetDrawObjectInfo( IntPtr drawObject )
    {
        CheckInitialized();
        var (obj, collection) = _penumbra!.PathResolver.IdentifyDrawObject( drawObject );
        return ( obj, collection.Name );
    }

    public IList< (string, string) > GetModList()
    {
        CheckInitialized();
        return Penumbra.ModManager.Select( m => ( m.ModPath.Name, m.Name.Text ) ).ToArray();
    }

    public IDictionary< string, (IList< string >, SelectType) >? GetAvailableModSettings( string modDirectory, string modName )
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

        var setting = mod.Groups[ groupIdx ].Type == SelectType.Multi ? 1u << optionIdx : ( uint )optionIdx;

        return collection.SetModSetting( mod.Index, groupIdx, setting ) ? PenumbraApiEc.Success : PenumbraApiEc.NothingChanged;
    }

    public PenumbraApiEc TrySetModSettings( string collectionName, string modDirectory, string modName, string optionGroupName,
        IReadOnlyList< string > optionNames )
    {
        CheckInitialized();
        if( optionNames.Count == 0 )
        {
            return PenumbraApiEc.InvalidArgument;
        }

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
        if( group.Type == SelectType.Single )
        {
            var name      = optionNames[ ^1 ];
            var optionIdx = group.IndexOf( o => o.Name == optionNames[ ^1 ] );
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

    public (PenumbraApiEc, string) CreateTemporaryCollection( string tag, string character, bool forceOverwriteCharacter )
    {
        CheckInitialized();
        if( !forceOverwriteCharacter && Penumbra.CollectionManager.Characters.ContainsKey( character )
        || Penumbra.TempMods.Collections.ContainsKey( character ) )
        {
            return ( PenumbraApiEc.CharacterCollectionExists, string.Empty );
        }

        var name = Penumbra.TempMods.SetTemporaryCollection( tag, character );
        return ( PenumbraApiEc.Success, name );
    }

    public PenumbraApiEc RemoveTemporaryCollection( string character )
    {
        CheckInitialized();
        if( !Penumbra.TempMods.Collections.ContainsKey( character ) )
        {
            return PenumbraApiEc.NothingChanged;
        }

        Penumbra.TempMods.RemoveTemporaryCollection( character );
        return PenumbraApiEc.Success;
    }

    public PenumbraApiEc AddTemporaryModAll( string tag, IReadOnlyDictionary< string, string > paths, IReadOnlySet< string > manipCodes,
        int priority )
    {
        CheckInitialized();
        if( !ConvertPaths( paths, out var p ) )
        {
            return PenumbraApiEc.InvalidGamePath;
        }

        if( !ConvertManips( manipCodes, out var m ) )
        {
            return PenumbraApiEc.InvalidManipulation;
        }

        return Penumbra.TempMods.Register( tag, null, p, m, priority ) switch
        {
            RedirectResult.Success => PenumbraApiEc.Success,
            _                      => PenumbraApiEc.UnknownError,
        };
    }

    public PenumbraApiEc AddTemporaryMod( string tag, string collectionName, IReadOnlyDictionary< string, string > paths,
        IReadOnlySet< string > manipCodes, int priority )
    {
        CheckInitialized();
        if( !Penumbra.TempMods.Collections.TryGetValue( collectionName, out var collection )
        && !Penumbra.CollectionManager.ByName( collectionName, out collection ) )
        {
            return PenumbraApiEc.CollectionMissing;
        }

        if( !ConvertPaths( paths, out var p ) )
        {
            return PenumbraApiEc.InvalidGamePath;
        }

        if( !ConvertManips( manipCodes, out var m ) )
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
        if( !Penumbra.TempMods.Collections.TryGetValue( collectionName, out var collection )
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

    private void OnGameObjectRedrawn( IntPtr objectAddress, int objectTableIndex )
    {
        GameObjectRedrawn?.Invoke( objectAddress, objectTableIndex );
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
            PluginLog.Warning( $"Could not load file {resolvedPath}:\n{e}" );
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

    // Convert manipulations from transmitted base64 strings to actual manipulations.
    // Only returns true if all conversions are successful and distinct.
    private static bool ConvertManips( IReadOnlyCollection< string > manipStrings,
        [NotNullWhen( true )] out HashSet< MetaManipulation >? manips )
    {
        manips = new HashSet< MetaManipulation >( manipStrings.Count );
        foreach( var m in manipStrings )
        {
            if( Functions.FromCompressedBase64< MetaManipulation >( m, out var manip ) != MetaManipulation.CurrentVersion )
            {
                manips = null;
                return false;
            }

            if( !manips.Add( manip ) )
            {
                manips = null;
                return false;
            }
        }

        return true;
    }
}