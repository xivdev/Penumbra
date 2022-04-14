using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Lumina.Data;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;

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

    private static string ResolvePath( string path, Mods.Mod2.Manager _, ModCollection collection )
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
                return collection.ChangedItems;
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
}