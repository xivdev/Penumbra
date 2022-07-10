using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Data;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Mods;

namespace Penumbra.Api;

public interface IPenumbraApiBase
{
    // The API version is staggered in two parts.
    // The major/Breaking version only increments if there are changes breaking backwards compatibility.
    // The minor/Feature version increments any time there is something added
    // and resets when Breaking is incremented.
    public (int Breaking, int Feature) ApiVersion { get; }
    public bool Valid { get; }
}

public delegate void ChangedItemHover( object? item );
public delegate void ChangedItemClick( MouseButton button, object? item );
public delegate void GameObjectRedrawn( IntPtr objectPtr, int objectTableIndex );
public delegate void ModSettingChanged( ModSettingChange type, string collectionName, string modDirectory, bool inherited );

public delegate void CreatingCharacterBaseDelegate( IntPtr gameObject, ModCollection collection, IntPtr modelId, IntPtr customize,
    IntPtr equipData );

public enum PenumbraApiEc
{
    Success            = 0,
    NothingChanged     = 1,
    CollectionMissing  = 2,
    ModMissing         = 3,
    OptionGroupMissing = 4,
    OptionMissing      = 5,

    CharacterCollectionExists = 6,
    LowerPriority             = 7,
    InvalidGamePath           = 8,
    FileMissing               = 9,
    InvalidManipulation       = 10,
    InvalidArgument           = 11,
    UnknownError              = 255,
}

public interface IPenumbraApi : IPenumbraApiBase
{
    // Obtain the currently set mod directory from the configuration.
    public string GetModDirectory();

    // Obtain the entire current penumbra configuration as a json encoded string.
    public string GetConfiguration();

    // Triggered when the user hovers over a listed changed object in a mod tab.
    // Can be used to append tooltips.
    public event ChangedItemHover? ChangedItemTooltip;

    // Events that are fired before and after the content of a mod settings panel are drawn.
    // Both are fired inside the child window of the settings panel itself.
    public event Action< string >? PreSettingsPanelDraw;
    public event Action< string >? PostSettingsPanelDraw;

    // Triggered when the user clicks a listed changed object in a mod tab.
    public event ChangedItemClick? ChangedItemClicked;
    public event GameObjectRedrawn? GameObjectRedrawn;

    // Triggered when a character base is created and a corresponding gameObject could be found,
    // before the Draw Object is actually created, so customize and equipdata can be manipulated beforehand.
    public event CreatingCharacterBaseDelegate? CreatingCharacterBase;

    // Queue redrawing of all actors of the given name with the given RedrawType.
    public void RedrawObject( string name, RedrawType setting );

    // Queue redrawing of the specific actor with the given RedrawType. Should only be used when the actor is sure to be valid.
    public void RedrawObject( GameObject gameObject, RedrawType setting );

    // Queue redrawing of the actor with the given object table index, if it exists, with the given RedrawType.
    public void RedrawObject( int tableIndex, RedrawType setting );

    // Queue redrawing of all currently available actors with the given RedrawType.
    public void RedrawAll( RedrawType setting );

    // Resolve a given gamePath via Penumbra using the Default and Forced collections.
    // Returns the given gamePath if penumbra would not manipulate it.
    public string ResolvePath( string gamePath );

    // Resolve a given gamePath via Penumbra using the character collection for the given name (if it exists) and the Forced collections.
    // Returns the given gamePath if penumbra would not manipulate it.
    public string ResolvePath( string gamePath, string characterName );

    // Resolve a given gamePath via Penumbra using any applicable character collections for the current character.
    // Returns the given gamePath if penumbra would not manipulate it.
    public string ResolvePlayerPath( string gamePath );

    // Reverse resolves a given modded local path into its replacement in form of all applicable game paths for given character collection.
    public string[] ReverseResolvePath( string moddedPath, string characterName );

    // Reverse resolves a given modded local path into its replacement in form of all applicable game paths
    // using the collection applying to the player character.
    public string[] ReverseResolvePlayerPath( string moddedPath );

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

    // Try to reload an existing mod by its directory name or mod name.
    // Can return ModMissing or success.
    // Reload is the same as if triggered by button press and might delete the mod if it is not valid anymore.
    public PenumbraApiEc ReloadMod( string modDirectory, string modName );

    // Try to add a new mod inside the mod root directory (modDirectory should only be the name, not the full name).
    // Returns FileMissing if the directory does not exist or success otherwise.
    // Note that success does only imply a successful call, not a successful mod load.
    public PenumbraApiEc AddMod( string modDirectory );

    // Obtain a base64 encoded, zipped json-string with a prepended version-byte of the current manipulations
    // for the collection currently associated with the player.
    public string GetPlayerMetaManipulations();

    // Obtain a base64 encoded, zipped json-string with a prepended version-byte of the current manipulations
    // for the given collection associated with the character name, or the default collection.
    public string GetMetaManipulations( string characterName );


    // ############## Mod Settings #################

    // Obtain the potential settings of a mod specified by its directory name first or mod name second.
    // Returns null if the mod could not be found.
    public IDictionary< string, (IList< string >, SelectType) >? GetAvailableModSettings( string modDirectory, string modName );

    // Obtain the enabled state, the priority, the settings of a mod specified by its directory name first or mod name second,
    // and whether these settings are inherited, or null if the collection does not set them at all.
    // If allowInheritance is false, only the collection itself will be checked.
    public (PenumbraApiEc, (bool, int, IDictionary< string, IList< string > >, bool)?) GetCurrentModSettings( string collectionName,
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

    public PenumbraApiEc TrySetModSettings( string collectionName, string modDirectory, string modName, string optionGroupName,
        IReadOnlyList< string > options );

    // This event gets fired when any setting in any collection changes.
    public event ModSettingChanged? ModSettingChanged;

    // Create a temporary collection without actual settings but with a cache.
    // If no character collection for this character exists or forceOverwriteCharacter is true,
    // associate this collection to a specific character.
    // Can return Okay, CharacterCollectionExists or NothingChanged, as well as the name of the new temporary collection on success.
    public (PenumbraApiEc, string) CreateTemporaryCollection( string tag, string character, bool forceOverwriteCharacter );

    // Remove the temporary collection associated with characterName if it exists.
    // Can return Okay or NothingChanged.
    public PenumbraApiEc RemoveTemporaryCollection( string characterName );

    // Set a temporary mod with the given paths, manipulations and priority and the name tag to all collections.
    // Can return Okay, InvalidGamePath, or InvalidManipulation.
    public PenumbraApiEc AddTemporaryModAll( string tag, Dictionary< string, string > paths, string manipString, int priority );

    // Set a temporary mod with the given paths, manipulations and priority and the name tag to the collection with the given name, which can be temporary.
    // Can return Okay, MissingCollection InvalidGamePath, or InvalidManipulation.
    public PenumbraApiEc AddTemporaryMod( string tag, string collectionName, Dictionary< string, string > paths, string manipString,
        int priority );

    // Remove the temporary mod with the given tag and priority from the temporary mods applying to all collections, if it exists.
    // Can return Okay or NothingDone.
    public PenumbraApiEc RemoveTemporaryModAll( string tag, int priority );

    // Remove the temporary mod with the given tag and priority from the temporary mods applying to the collection of the given name, which can be temporary.
    // Can return Okay or NothingDone.
    public PenumbraApiEc RemoveTemporaryMod( string tag, string collectionName, int priority );
}