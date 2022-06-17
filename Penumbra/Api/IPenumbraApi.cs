using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Data;
using OtterGui.Classes;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Api;

public interface IPenumbraApiBase
{
    public int ApiVersion { get; }
    public bool Valid { get; }
}

public delegate void ChangedItemHover( object? item );
public delegate void ChangedItemClick( MouseButton button, object? item );
public delegate void GameObjectRedrawn( IntPtr objectPtr, int objectTableIndex );

public enum PenumbraApiEc
{
    Okay               = 0,
    NothingChanged     = 1,
    CollectionMissing  = 2,
    ModMissing         = 3,
    OptionGroupMissing = 4,
    SettingMissing     = 5,

    CharacterCollectionExists = 6,
    LowerPriority             = 7,
    InvalidGamePath           = 8,
    FileMissing               = 9,
    InvalidManipulation       = 10,
}

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
    public event GameObjectRedrawn? GameObjectRedrawn;

    // Queue redrawing of all actors of the given name with the given RedrawType.
    public void RedrawObject( string name, RedrawType setting );

    // Queue redrawing of the actor with the given object table index, if it exists, with the given RedrawType.
    public void RedrawObject( int tableIndex, RedrawType setting );

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

    // Reverse resolves a given modded local path into its replacement in form of all applicable game path for given character
    public IList<string> ReverseResolvePath( string moddedPath, string characterName );

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


    // ############## Mod Settings #################

    // Obtain the potential settings of a mod specified by its directory name first or mod name second.
    // Returns null if the mod could not be found.
    public IDictionary< string, (IList<string>, SelectType) >? GetAvailableModSettings( string modDirectory, string modName );

    // Obtain the enabled state, the priority, the settings of a mod specified by its directory name first or mod name second,
    // and whether these settings are inherited, or null if the collection does not set them at all.
    // If allowInheritance is false, only the collection itself will be checked.
    public (PenumbraApiEc, (bool, int, IDictionary< string, IList<string> >, bool)?) GetCurrentModSettings( string collectionName,
        string modDirectory, string modName, bool allowInheritance );

    // Try to set the inheritance state in the given collection of a mod specified by its directory name first or mod name second.
    // Returns Okay, NothingChanged, CollectionMissing or ModMissing.
    public PenumbraApiEc TryInheritMod( string collectionName, string modDirectory, string modName, bool inherit );

    // Try to set the enabled state in the given collection of a mod specified by its directory name first or mod name second. Also removes inheritance.
    // Returns Okay, NothingChanged, CollectionMissing or ModMissing.
    public PenumbraApiEc TrySetMod( string collectionName, string modDirectory, string modName, bool enabled );

    // Try to set the priority in the given collection of a mod specified by its directory name first or mod name second. Also removes inheritance.
    // Returns Okay, NothingChanged, CollectionMissing or ModMissing.
    public PenumbraApiEc TrySetModPriority( string collectionName, string modDirectory, string modName, int priority );

    // Try to set a specific option group in the given collection of a mod specified by its directory name first or mod name second. Also removes inheritance.
    // If the group is a Single Selection group, options should be a single string, otherwise the array of enabled options.
    // Returns Okay, NothingChanged, CollectionMissing or ModMissing, OptionGroupMissing or SettingMissing.
    // If any setting can not be found, it will not change anything.
    public PenumbraApiEc TrySetModSetting( string collectionName, string modDirectory, string modName, string optionGroupName, string option );

    public PenumbraApiEc TrySetModSetting( string collectionName, string modDirectory, string modName, string optionGroupName,
        IReadOnlyList<string> options );


    // Create a temporary collection without actual settings but with a cache.
    // If character is non-zero and either no character collection for this character exists or forceOverwriteCharacter is true,
    // associate this collection to a specific character.
    // Can return Okay, CharacterCollectionExists or NothingChanged.
    public PenumbraApiEc CreateTemporaryCollection( string collectionName, string? character, bool forceOverwriteCharacter );

    // Remove a temporary collection if it exists.
    // Can return Okay or NothingChanged.
    public PenumbraApiEc RemoveTemporaryCollection( string collectionName );


    // Set or remove a specific file redirection or meta manipulation under the name of Tag and with a given priority
    // for a given collection, which may be temporary.
    // Can return Okay, CollectionMissing, InvalidPath, FileMissing, LowerPriority, or NothingChanged.
    public PenumbraApiEc SetFileRedirection( string tag, string collectionName, string gamePath, string fullPath, int priority );

    // Can return Okay, CollectionMissing, InvalidManipulation, LowerPriority, or NothingChanged.
    public PenumbraApiEc SetMetaManipulation( string tag, string collectionName, string manipulationBase64, int priority );

    // Can return Okay, CollectionMissing, InvalidPath, or NothingChanged.
    public PenumbraApiEc RemoveFileRedirection( string tag, string collectionName, string gamePath );

    // Can return Okay, CollectionMissing, InvalidManipulation, or NothingChanged.
    public PenumbraApiEc RemoveMetaManipulation( string tag, string collectionName, string manipulationBase64 );
}