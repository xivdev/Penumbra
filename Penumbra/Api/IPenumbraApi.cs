using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Data;
using Penumbra.GameData.Enums;

namespace Penumbra.Api;

public interface IPenumbraApiBase
{
    public int ApiVersion { get; }
    public bool Valid { get; }
}

public delegate void ChangedItemHover( object? item );
public delegate void ChangedItemClick( MouseButton button, object? item );

public interface IPenumbraApi : IPenumbraApiBase
{
    // Obtain the currently set mod directory from the configuration.
    public string GetModDirectory();

    // Obtain the entire current penumbra configuration.
    public IPluginConfiguration GetConfiguration();

    // Triggered when the user hovers over a listed changed object in a mod tab.
    // Can be used to append tooltips.
    public event ChangedItemHover? ChangedItemTooltip;

    // Triggered when the user clicks a listed changed object in a mod tab.
    public event ChangedItemClick? ChangedItemClicked;

    // Queue redrawing of all actors of the given name with the given RedrawType.
    public void RedrawObject( string name, RedrawType setting );

    // Queue redrawing of the specific actor with the given RedrawType. Should only be used when the actor is sure to be valid.
    public void RedrawObject( GameObject gameObject, RedrawType setting );

    // Queue redrawing of all currently available actors with the given RedrawType.
    public void RedrawAll( RedrawType setting );

    // Resolve a given gamePath via Penumbra using the Default and Forced collections.
    // Returns the given gamePath if penumbra would not manipulate it.
    public string ResolvePath( string gamePath );

    // Resolve a given gamePath via Penumbra using the character collection for the given name (if it exists) and the Forced collections.
    // Returns the given gamePath if penumbra would not manipulate it.
    public string ResolvePath( string gamePath, string characterName );

    // Try to load a given gamePath with the resolved path from Penumbra.
    public T? GetFile< T >( string gamePath ) where T : FileResource;

    // Try to load a given gamePath with the resolved path from Penumbra.
    public T? GetFile< T >( string gamePath, string characterName ) where T : FileResource;

    // Gets a dictionary of effected items from a collection
    public IReadOnlyDictionary< string, object? > GetChangedItemsForCollection( string collectionName );

    // Obtain a list of the names of all currently installed collections.
    public IList< string > GetCollections();

    // Obtain the name of the currently selected collection.
    public string GetCurrentCollection();

    // Obtain the name of the default collection.
    public string GetDefaultCollection();

    // Obtain the name of the collection associated with characterName and whether it is configured or inferred from default.
    public (string, bool) GetCharacterCollection( string characterName );

    // Obtain the game object associated with a given draw object and the name of the collection associated with this game object.
    public (IntPtr, string) GetDrawObjectInfo( IntPtr drawObject );

    // Obtain a list of all installed mods. The first string is their directory name, the second string is their mod name.
    public IList< (string, string) > GetModList();
}