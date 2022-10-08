using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Data;
using System;
using System.Collections.Generic;
using Penumbra.Api.Enums;

namespace Penumbra.Api;

public interface IPenumbraApi : IPenumbraApiBase
{
    #region Game State

    // Obtain the currently set mod directory from the configuration.
    public string GetModDirectory();

    // Obtain the entire current penumbra configuration as a json encoded string.
    public string GetConfiguration();

    // Fired whenever a mod directory change is finished.
    // Gives the full path of the mod directory and whether Penumbra treats it as valid.
    public event Action< string, bool >? ModDirectoryChanged;

    #endregion

    #region UI

    // Triggered when the user hovers over a listed changed object in a mod tab.
    // Can be used to append tooltips.
    public event ChangedItemHover? ChangedItemTooltip;

    // Events that are fired before and after the content of a mod settings panel are drawn.
    // Both are fired inside the child window of the settings panel itself.
    public event Action< string >? PreSettingsPanelDraw;
    public event Action< string >? PostSettingsPanelDraw;

    // Triggered when the user clicks a listed changed object in a mod tab.
    public event ChangedItemClick? ChangedItemClicked;

    #endregion

    #region Redrawing

    // Queue redrawing of all actors of the given name with the given RedrawType.
    public void RedrawObject( string name, RedrawType setting );

    // Queue redrawing of the specific actor with the given RedrawType. Should only be used when the actor is sure to be valid.
    public void RedrawObject( GameObject gameObject, RedrawType setting );

    // Queue redrawing of the actor with the given object table index, if it exists, with the given RedrawType.
    public void RedrawObject( int tableIndex, RedrawType setting );

    // Queue redrawing of all currently available actors with the given RedrawType.
    public void RedrawAll( RedrawType setting );

    // Triggered whenever a game object is redrawn via Penumbra.
    public event GameObjectRedrawn? GameObjectRedrawn;

    #endregion

    #region Game State

    // Obtain the game object associated with a given draw object and the name of the collection associated with this game object.
    public (IntPtr, string) GetDrawObjectInfo( IntPtr drawObject );

    // Obtain the parent game object index for an unnamed cutscene actor by its index.
    public int GetCutsceneParentIndex( int actor );

    // Triggered when a character base is created and a corresponding gameObject could be found,
    // before the Draw Object is actually created, so customize and equipdata can be manipulated beforehand.
    public event CreatingCharacterBaseDelegate? CreatingCharacterBase;

    // Triggered after a character base was created if a corresponding gameObject could be found,
    // so you can apply flag changes after finishing.
    public event CreatedCharacterBaseDelegate? CreatedCharacterBase;

    // Triggered whenever a resource is redirected by Penumbra for a specific, identified game object.
    // Does not trigger if the resource is not requested for a known game object.
    public event GameObjectResourceResolvedDelegate? GameObjectResourceResolved;

    #endregion

    #region Resolving

    // Resolve a given gamePath via Penumbra using the Default collection.
    // Returns the given gamePath if penumbra would not manipulate it.
    public string ResolveDefaultPath( string gamePath );

    // Resolve a given gamePath via Penumbra using the Interface collection.
    // Returns the given gamePath if penumbra would not manipulate it.
    public string ResolveInterfacePath( string gamePath );

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

    #endregion

    #region Collections

    // Obtain a list of the names of all currently installed collections.
    public IList< string > GetCollections();

    // Obtain the name of the currently selected collection.
    public string GetCurrentCollection();

    // Obtain the name of the default collection.
    public string GetDefaultCollection();

    // Obtain the name of the interface collection.
    public string GetInterfaceCollection();

    // Obtain the name of the collection associated with characterName and whether it is configured or inferred from default.
    public (string, bool) GetCharacterCollection( string characterName );

    // Gets a dictionary of effected items from a collection
    public IReadOnlyDictionary< string, object? > GetChangedItemsForCollection( string collectionName );

    #endregion

    #region Meta

    // Obtain a base64 encoded, zipped json-string with a prepended version-byte of the current manipulations
    // for the collection currently associated with the player.
    public string GetPlayerMetaManipulations();

    // Obtain a base64 encoded, zipped json-string with a prepended version-byte of the current manipulations
    // for the given collection associated with the character name, or the default collection.
    public string GetMetaManipulations( string characterName );

    #endregion

    #region Mods

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

    // Try to delete a mod given by its modDirectory or its name.
    // Returns NothingDone if the mod can not be found or success otherwise.
    // Note that success does only imply a successful call, not successful deletion.
    public PenumbraApiEc DeleteMod( string modDirectory, string modName );

    // Get the internal full filesystem path including search order for the specified mod.
    // If success is returned, the second return value contains the full path
    // and a bool indicating whether this is the default path (false) or a manually set one (true).
    // Can return ModMissing or Success.
    public (PenumbraApiEc, string, bool) GetModPath( string modDirectory, string modName );

    // Set the internal search order and filesystem path of the specified mod to the given path.
    // Returns InvalidArgument if newPath is empty, ModMissing if the mod can not be found,
    // PathRenameFailed if newPath could not be set and Success otherwise.
    public PenumbraApiEc SetModPath( string modDirectory, string modName, string newPath );

    #endregion

    #region Mod Settings

    // Obtain the potential settings of a mod specified by its directory name first or mod name second.
    // Returns null if the mod could not be found.
    public IDictionary< string, (IList< string >, GroupType) >? GetAvailableModSettings( string modDirectory, string modName );

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

    #endregion

    #region Temporary

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

    #endregion
}