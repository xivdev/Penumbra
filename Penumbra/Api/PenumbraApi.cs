using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Lumina.Data;
using Newtonsoft.Json;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Mods;

namespace Penumbra.Api;

public class PenumbraApi : IDisposable, IPenumbraApi
{
    public int ApiVersion { get; } = 4;
    private Penumbra?        _penumbra;
    private Lumina.GameData? _lumina;

    public bool Valid
        => _penumbra != null;

    public PenumbraApi( Penumbra penumbra )
    {
        _penumbra = penumbra;
        _lumina = ( Lumina.GameData? )Dalamud.GameData.GetType()
           .GetField( "gameData", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( Dalamud.GameData );
    }

    public void Dispose()
    {
        _penumbra = null;
        _lumina   = null;
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

    private static string ResolvePath( string path, Mods.Mod.Manager _, ModCollection collection )
    {
        if( !Penumbra.Config.EnableMods )
        {
            return path;
        }

        var gamePath = Utf8GamePath.FromString( path, out var p, true ) ? p : Utf8GamePath.Empty;
        var ret      = collection.ResolvePath( gamePath );
        return ret?.ToString() ?? path;
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

    public Dictionary< string, (string[], SelectType) >? GetAvailableModSettings( string modDirectory, string modName )
        => throw new NotImplementedException();

    public (PenumbraApiEc, (bool, int, Dictionary< string, string[] >, bool)?) GetCurrentModSettings( string collectionName, string modDirectory, string modName,
        bool allowInheritance )
        => throw new NotImplementedException();

    public PenumbraApiEc TryInheritMod( string collectionName, string modDirectory, string modName, bool inherit )
        => throw new NotImplementedException();

    public PenumbraApiEc TrySetMod( string collectionName, string modDirectory, string modName, bool enabled )
        => throw new NotImplementedException();

    public PenumbraApiEc TrySetModPriority( string collectionName, string modDirectory, string modName, int priority )
        => throw new NotImplementedException();

    public PenumbraApiEc TrySetModSetting( string collectionName, string modDirectory, string modName, string optionGroupName, string option )
        => throw new NotImplementedException();

    public PenumbraApiEc TrySetModSetting( string collectionName, string modDirectory, string modName, string optionGroupName, string[] options )
        => throw new NotImplementedException();

    public PenumbraApiEc CreateTemporaryCollection( string collectionName, string? character, bool forceOverwriteCharacter )
        => throw new NotImplementedException();

    public PenumbraApiEc RemoveTemporaryCollection( string collectionName )
        => throw new NotImplementedException();

    public PenumbraApiEc SetFileRedirection( string tag, string collectionName, string gamePath, string fullPath, int priority )
        => throw new NotImplementedException();

    public PenumbraApiEc SetMetaManipulation( string tag, string collectionName, string manipulationBase64, int priority )
        => throw new NotImplementedException();

    public PenumbraApiEc RemoveFileRedirection( string tag, string collectionName, string gamePath )
        => throw new NotImplementedException();

    public PenumbraApiEc RemoveMetaManipulation( string tag, string collectionName, string manipulationBase64 )
        => throw new NotImplementedException();
}