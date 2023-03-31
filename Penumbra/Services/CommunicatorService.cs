using System;
using System.IO;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Util;

namespace Penumbra.Services;

public class CommunicatorService : IDisposable
{
    /// <summary>
    /// Triggered whenever collection setup is changed.
    /// <list type="number">
    ///     <item>Parameter is the type of the changed collection. (Inactive or Temporary for additions or deletions)</item>
    ///     <item>Parameter is the old collection, or null on additions.</item>
    ///     <item>Parameter is the new collection, or null on deletions.</item>
    ///     <item>Parameter is the display name for Individual collections or an empty string otherwise.</item>
    /// </list> </summary>
    public readonly EventWrapper<CollectionType, ModCollection?, ModCollection?, string> CollectionChange = new(nameof(CollectionChange));

    /// <summary>
    /// Triggered whenever a temporary mod for all collections is changed.
    /// <list type="number">
    ///     <item>Parameter added, deleted or edited temporary mod.</item>
    ///     <item>Parameter is whether the mod was newly created.</item>
    ///     <item>Parameter is whether the mod was deleted.</item>
    /// </list> </summary>
    public readonly EventWrapper<TemporaryMod, bool, bool> TemporaryGlobalModChange = new(nameof(TemporaryGlobalModChange));

    /// <summary>
    /// Triggered whenever a character base draw object is being created by the game.
    /// <list type="number">
    ///     <item>Parameter is the game object for which a draw object is created. </item>
    ///     <item>Parameter is the name of the applied collection. </item>
    ///     <item>Parameter is a pointer to the model id (an uint). </item>
    ///     <item>Parameter is a pointer to the customize array. </item>
    ///     <item>Parameter is a pointer to the equip data array. </item>
    /// </list> </summary>
    public readonly EventWrapper<nint, string, nint, nint, nint> CreatingCharacterBase = new(nameof(CreatingCharacterBase));

    /// <summary> <list type="number">
    ///     <item>Parameter is the game object for which a draw object is created. </item>
    ///     <item>Parameter is the name of the applied collection. </item>
    ///     <item>Parameter is the created draw object. </item>
    /// </list> </summary>
    public readonly EventWrapper<nint, string, nint> CreatedCharacterBase = new(nameof(CreatedCharacterBase));

    /// <summary>
    /// Triggered whenever mod meta data or local data is changed.
    /// <list type="number">
    ///     <item>Parameter is the type of data change for the mod, which can be multiple flags. </item>
    ///     <item>Parameter is the changed mod. </item>
    ///     <item>Parameter is the old name of the mod in case of a name change, and null otherwise. </item>
    /// </list> </summary>
    public readonly EventWrapper<ModDataChangeType, Mod, string?> ModDataChanged = new(nameof(ModDataChanged));

    /// <summary>
    /// Triggered whenever an option of a mod is changed inside the mod.
    /// <list type="number">
    ///     <item>Parameter is the type option change. </item>
    ///     <item>Parameter is the changed mod. </item>
    ///     <item>Parameter is the index of the changed group inside the mod. </item>
    ///     <item>Parameter is the index of the changed option inside the group or -1 if it does not concern a specific option. </item>
    ///     <item>Parameter is the index of the group an option was moved to. </item>
    /// </list> </summary>
    public readonly EventWrapper<ModOptionChangeType, Mod, int, int, int> ModOptionChanged = new(nameof(ModOptionChanged));


    /// <summary> Triggered whenever mods are prepared to be rediscovered. </summary>
    public readonly EventWrapper ModDiscoveryStarted = new(nameof(ModDiscoveryStarted));

    /// <summary> Triggered whenever a new mod discovery has finished. </summary>
    public readonly EventWrapper ModDiscoveryFinished = new(nameof(ModDiscoveryFinished));

    /// <summary>
    /// Triggered whenever the mod root directory changes.
    /// <list type="number">
    ///     <item>Parameter is the full path of the new directory. </item>
    ///     <item>Parameter is whether the new directory is valid. </item>
    /// </list>
    /// </summary>
    public readonly EventWrapper<string, bool> ModDirectoryChanged = new(nameof(ModDirectoryChanged));

    /// <summary>
    /// Triggered whenever a mod is added, deleted, moved or reloaded.
    /// <list type="number">
    ///     <item>Parameter is the type of change. </item>
    ///     <item>Parameter is the changed mod. </item>
    ///     <item>Parameter is the old directory on deletion, move or reload and null on addition. </item>
    ///     <item>Parameter is the new directory on addition, move or reload and null on deletion. </item>
    /// </list>
    /// </summary>
    public EventWrapper<ModPathChangeType, Mod, DirectoryInfo?, DirectoryInfo?> ModPathChanged = new(nameof(ModPathChanged));

    public void Dispose()
    {
        CollectionChange.Dispose();
        TemporaryGlobalModChange.Dispose();
        CreatingCharacterBase.Dispose();
        CreatedCharacterBase.Dispose();
        ModDataChanged.Dispose();
        ModOptionChanged.Dispose();
        ModDiscoveryStarted.Dispose();
        ModDiscoveryFinished.Dispose();
        ModDirectoryChanged.Dispose();
        ModPathChanged.Dispose();
    }
}
