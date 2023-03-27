using System;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Services;

public class CommunicatorService : IDisposable
{
    /// <summary> <list type="number">
    ///     <item>Parameter is the type of the changed collection. (Inactive or Temporary for additions or deletions)</item>
    ///     <item>Parameter is the old collection, or null on additions.</item>
    ///     <item>Parameter is the new collection, or null on deletions.</item>
    ///     <item>Parameter is the display name for Individual collections or an empty string otherwise.</item>
    /// </list> </summary>
    public readonly EventWrapper<CollectionType, ModCollection?, ModCollection?, string> CollectionChange = new(nameof(CollectionChange));

    /// <summary> <list type="number">
    ///     <item>Parameter added, deleted or edited temporary mod.</item>
    ///     <item>Parameter is whether the mod was newly created.</item>
    ///     <item>Parameter is whether the mod was deleted.</item>
    /// </list> </summary>
    public readonly EventWrapper<TemporaryMod, bool, bool> TemporaryGlobalModChange = new(nameof(TemporaryGlobalModChange));

    /// <summary> <list type="number">
    ///     <item>Parameter is the type of change. </item>
    ///     <item>Parameter is the affected mod. </item>
    ///     <item>Parameter is either null or the old name of the mod. </item>
    /// </list> </summary>
    public readonly EventWrapper<ModDataChangeType, Mod, string?> ModMetaChange = new(nameof(ModMetaChange));

    /// <summary> <list type="number">
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

    /// <summary> <list type="number">
    ///     <item>Parameter is the type of data change for the mod, which can be multiple flags. </item>
    ///     <item>Parameter is the changed mod. </item>
    ///     <item>Parameter is the old name of the mod in case of a name change, and null otherwise. </item>
    /// </list> </summary>
    public readonly EventWrapper<ModDataChangeType, Mod, string?> ModDataChanged = new(nameof(ModDataChanged));

    /// <summary><list type="number">
    ///     <item>Parameter is the type option change. </item>
    ///     <item>Parameter is the changed mod. </item>
    ///     <item>Parameter is the index of the changed group inside the mod. </item>
    ///     <item>Parameter is the index of the changed option inside the group or -1 if it does not concern a specific option. </item>
    ///     <item>Parameter is the index of the group an option was moved to. </item>
    /// </list> </summary>
    public readonly EventWrapper<ModOptionChangeType, Mod, int, int, int> ModOptionChanged = new(nameof(ModOptionChanged));

    public void Dispose()
    {
        CollectionChange.Dispose();
        TemporaryGlobalModChange.Dispose();
        ModMetaChange.Dispose();
        CreatingCharacterBase.Dispose();
        CreatedCharacterBase.Dispose();
        ModDataChanged.Dispose();
        ModOptionChanged.Dispose();
    }
}
