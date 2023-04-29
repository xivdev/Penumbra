using System;
using System.IO;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Util;

namespace Penumbra.Services;

/// <summary>
/// Triggered whenever collection setup is changed.
/// <list type="number">
///     <item>Parameter is the type of the changed collection. (Inactive or Temporary for additions or deletions)</item>
///     <item>Parameter is the old collection, or null on additions.</item>
///     <item>Parameter is the new collection, or null on deletions.</item>
///     <item>Parameter is the display name for Individual collections or an empty string otherwise.</item>
/// </list> </summary>
public sealed class CollectionChange : EventWrapper<Action<CollectionType, ModCollection?, ModCollection?, string>>
{
    public CollectionChange()
        : base(nameof(CollectionChange))
    { }

    public void Invoke(CollectionType collectionType, ModCollection? oldCollection, ModCollection? newCollection, string displayName)
        => Invoke(this, collectionType, oldCollection, newCollection, displayName);
}

/// <summary>
/// Triggered whenever a temporary mod for all collections is changed.
/// <list type="number">
///     <item>Parameter added, deleted or edited temporary mod.</item>
///     <item>Parameter is whether the mod was newly created.</item>
///     <item>Parameter is whether the mod was deleted.</item>
/// </list> </summary>
public sealed class TemporaryGlobalModChange : EventWrapper<Action<TemporaryMod, bool, bool>>
{
    public TemporaryGlobalModChange()
        : base(nameof(TemporaryGlobalModChange))
    { }

    public void Invoke(TemporaryMod temporaryMod, bool newlyCreated, bool deleted)
        => Invoke(this, temporaryMod, newlyCreated, deleted);
}

/// <summary>
/// Triggered whenever a character base draw object is being created by the game.
/// <list type="number">
///     <item>Parameter is the game object for which a draw object is created. </item>
///     <item>Parameter is the name of the applied collection. </item>
///     <item>Parameter is a pointer to the model id (an uint). </item>
///     <item>Parameter is a pointer to the customize array. </item>
///     <item>Parameter is a pointer to the equip data array. </item>
/// </list> </summary>
public sealed class CreatingCharacterBase : EventWrapper<Action<nint, string, nint, nint, nint>>
{
    public CreatingCharacterBase()
        : base(nameof(CreatingCharacterBase))
    { }

    public void Invoke(nint gameObject, string appliedCollectionName, nint modelIdAddress, nint customizeArrayAddress, nint equipDataAddress)
        => Invoke(this, gameObject, appliedCollectionName, modelIdAddress, customizeArrayAddress, equipDataAddress);
}

/// <summary> <list type="number">
///     <item>Parameter is the game object for which a draw object is created. </item>
///     <item>Parameter is the name of the applied collection. </item>
///     <item>Parameter is the created draw object. </item>
/// </list> </summary>
public sealed class CreatedCharacterBase : EventWrapper<Action<nint, string, nint>>
{
    public CreatedCharacterBase()
        : base(nameof(CreatedCharacterBase))
    { }

    public void Invoke(nint gameObject, string appliedCollectionName, nint drawObject)
        => Invoke(this, gameObject, appliedCollectionName, drawObject);
}

/// <summary>
/// Triggered whenever mod meta data or local data is changed.
/// <list type="number">
///     <item>Parameter is the type of data change for the mod, which can be multiple flags. </item>
///     <item>Parameter is the changed mod. </item>
///     <item>Parameter is the old name of the mod in case of a name change, and null otherwise. </item>
/// </list> </summary>
public sealed class ModDataChanged : EventWrapper<Action<ModDataChangeType, Mod, string?>>
{
    public ModDataChanged()
        : base(nameof(ModDataChanged))
    { }

    public void Invoke(ModDataChangeType changeType, Mod mod, string? oldName)
        => Invoke(this, changeType, mod, oldName);
}

/// <summary>
/// Triggered whenever an option of a mod is changed inside the mod.
/// <list type="number">
///     <item>Parameter is the type option change. </item>
///     <item>Parameter is the changed mod. </item>
///     <item>Parameter is the index of the changed group inside the mod. </item>
///     <item>Parameter is the index of the changed option inside the group or -1 if it does not concern a specific option. </item>
///     <item>Parameter is the index of the group an option was moved to. </item>
/// </list> </summary>
public sealed class ModOptionChanged : EventWrapper<Action<ModOptionChangeType, Mod, int, int, int>>
{
    public ModOptionChanged()
        : base(nameof(ModOptionChanged))
    { }

    public void Invoke(ModOptionChangeType changeType, Mod mod, int groupIndex, int optionIndex, int moveToIndex)
        => Invoke(this, changeType, mod, groupIndex, optionIndex, moveToIndex);
}

/// <summary> Triggered whenever mods are prepared to be rediscovered. </summary>
public sealed class ModDiscoveryStarted : EventWrapper<Action>
{
    public ModDiscoveryStarted()
        : base(nameof(ModDiscoveryStarted))
    { }

    public void Invoke()
        => EventWrapper<Action>.Invoke(this);
}

/// <summary> Triggered whenever a new mod discovery has finished. </summary>
public sealed class ModDiscoveryFinished : EventWrapper<Action>
{
    public ModDiscoveryFinished()
        : base(nameof(ModDiscoveryFinished))
    { }

    public void Invoke()
        => Invoke(this);
}

/// <summary>
/// Triggered whenever the mod root directory changes.
/// <list type="number">
///     <item>Parameter is the full path of the new directory. </item>
///     <item>Parameter is whether the new directory is valid. </item>
/// </list>
/// </summary>
public sealed class ModDirectoryChanged : EventWrapper<Action<string, bool>>
{
    public ModDirectoryChanged()
        : base(nameof(ModDirectoryChanged))
    { }

    public void Invoke(string newModDirectory, bool newDirectoryValid)
        => Invoke(this, newModDirectory, newDirectoryValid);
}

/// <summary>
/// Triggered whenever a mod is added, deleted, moved or reloaded.
/// <list type="number">
///     <item>Parameter is the type of change. </item>
///     <item>Parameter is the changed mod. </item>
///     <item>Parameter is the old directory on deletion, move or reload and null on addition. </item>
///     <item>Parameter is the new directory on addition, move or reload and null on deletion. </item>
/// </list>
/// </summary>
public sealed class ModPathChanged : EventWrapper<Action<ModPathChangeType, Mod, DirectoryInfo?, DirectoryInfo?>>
{
    public ModPathChanged()
        : base(nameof(ModPathChanged))
    { }

    public void Invoke(ModPathChangeType changeType, Mod mod, DirectoryInfo? oldModDirectory, DirectoryInfo? newModDirectory)
        => Invoke(this, changeType, mod, oldModDirectory, newModDirectory);
}

/// <summary>
/// Triggered whenever a mod setting is changed.
/// <list type="number">
///     <item>Parameter is the collection in which the setting was changed. </item>
///     <item>Parameter is the type of change. </item>
///     <item>Parameter is the mod the setting was changed for, unless it was a multi-change. </item>
///     <item>Parameter is the old value of the setting before the change as int. </item>
///     <item>Parameter is the index of the changed group if the change type is Setting. </item>
///     <item>Parameter is whether the change was inherited from another collection. </item>
/// </list>
/// </summary>
public sealed class ModSettingChanged : EventWrapper<Action<ModCollection, ModSettingChange, Mod?, int, int, bool>>
{
    public ModSettingChanged()
        : base(nameof(ModSettingChanged))
    { }

    public void Invoke(ModCollection collection, ModSettingChange type, Mod? mod, int oldValue, int groupIdx, bool inherited)
        => Invoke(this, collection, type, mod, oldValue, groupIdx, inherited);
}

/// <summary>
/// Triggered whenever a collections inheritances change.
/// <list type="number">
///     <item>Parameter is the collection whose ancestors were changed. </item>
///     <item>Parameter is whether the change was itself inherited, i.e. if it happened in a direct parent (false) or a more removed ancestor (true). </item>
/// </list>
/// </summary>
public sealed class CollectionInheritanceChanged : EventWrapper<Action<ModCollection, bool>>
{
    public CollectionInheritanceChanged()
        : base(nameof(CollectionInheritanceChanged))
    { }

    public void Invoke(ModCollection collection, bool inherited)
        => Invoke(this, collection, inherited);
}

/// <summary>
/// Triggered when the general Enabled state of Penumbra is changed.
/// <list type="number">
///     <item>Parameter is whether Penumbra is now Enabled (true) or Disabled (false). </item>
/// </list>
/// </summary>
public sealed class EnabledChanged : EventWrapper<Action<bool>>
{
    public EnabledChanged()
        : base(nameof(EnabledChanged))
    { }

    public void Invoke(bool enabled)
        => Invoke(this, enabled);
}

/// <summary>
/// Triggered before the settings panel is drawn.
/// <list type="number">
///     <item>Parameter is the identifier (directory name) of the currently selected mod. </item>
/// </list>
/// </summary>
public sealed class PreSettingsPanelDraw : EventWrapper<Action<string>>
{
    public PreSettingsPanelDraw()
        : base(nameof(PreSettingsPanelDraw))
    { }

    public void Invoke(string modDirectory)
        => Invoke(this, modDirectory);
}

/// <summary>
/// Triggered after the settings panel is drawn.
/// <list type="number">
///     <item>Parameter is the identifier (directory name) of the currently selected mod. </item>
/// </list>
/// </summary>
public sealed class PostSettingsPanelDraw : EventWrapper<Action<string>>
{
    public PostSettingsPanelDraw()
        : base(nameof(PostSettingsPanelDraw))
    { }

    public void Invoke(string modDirectory)
        => Invoke(this, modDirectory);
}

/// <summary>
/// Triggered when a Changed Item in Penumbra is hovered.
/// <list type="number">
///     <item>Parameter is the hovered object data if any. </item>
/// </list>
/// </summary>
public sealed class ChangedItemHover : EventWrapper<Action<object?>>
{
    public ChangedItemHover()
        : base(nameof(ChangedItemHover))
    { }

    public void Invoke(object? data)
        => Invoke(this, data);

    public bool HasTooltip
        => HasSubscribers;
}

/// <summary>
/// Triggered when a Changed Item in Penumbra is clicked.
/// <list type="number">
///     <item>Parameter is the clicked mouse button. </item>
///     <item>Parameter is the clicked object data if any.. </item>
/// </list>
/// </summary>
public sealed class ChangedItemClick : EventWrapper<Action<MouseButton, object?>>
{
    public ChangedItemClick()
        : base(nameof(ChangedItemClick))
    { }

    public void Invoke(MouseButton button, object? data)
        => Invoke(this, button, data);
}

public class CommunicatorService : IDisposable
{
    /// <inheritdoc cref="Services.CollectionChange"/>
    public readonly CollectionChange CollectionChange = new();

    /// <inheritdoc cref="Services.TemporaryGlobalModChange"/>
    public readonly TemporaryGlobalModChange TemporaryGlobalModChange = new();

    /// <inheritdoc cref="Services.CreatingCharacterBase"/>
    public readonly CreatingCharacterBase CreatingCharacterBase = new();

    /// <inheritdoc cref="Services.CreatedCharacterBase"/>
    public readonly CreatedCharacterBase CreatedCharacterBase = new();

    /// <inheritdoc cref="Services.ModDataChanged"/>
    public readonly ModDataChanged ModDataChanged = new();

    /// <inheritdoc cref="Services.ModOptionChanged"/>
    public readonly ModOptionChanged ModOptionChanged = new();

    /// <inheritdoc cref="Services.ModDiscoveryStarted"/>
    public readonly ModDiscoveryStarted ModDiscoveryStarted = new();

    /// <inheritdoc cref="Services.ModDiscoveryFinished"/>
    public readonly ModDiscoveryFinished ModDiscoveryFinished = new();

    /// <inheritdoc cref="Services.ModDirectoryChanged"/>
    public readonly ModDirectoryChanged ModDirectoryChanged = new();

    /// <inheritdoc cref="Services.ModPathChanged"/>
    public readonly ModPathChanged ModPathChanged = new();

    /// <inheritdoc cref="Services.ModSettingChanged"/>
    public readonly ModSettingChanged ModSettingChanged = new();

    /// <inheritdoc cref="Services.CollectionInheritanceChanged"/>
    public readonly CollectionInheritanceChanged CollectionInheritanceChanged = new();

    /// <inheritdoc cref="Services.EnabledChanged"/>
    public readonly EnabledChanged EnabledChanged = new();

    /// <inheritdoc cref="Services.PreSettingsPanelDraw"/>
    public readonly PreSettingsPanelDraw PreSettingsPanelDraw = new();

    /// <inheritdoc cref="Services.PostSettingsPanelDraw"/>
    public readonly PostSettingsPanelDraw PostSettingsPanelDraw = new();

    /// <inheritdoc cref="Services.ChangedItemHover"/>
    public readonly ChangedItemHover ChangedItemHover = new();

    /// <inheritdoc cref="Services.ChangedItemClick"/>
    public readonly ChangedItemClick ChangedItemClick = new();

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
        ModSettingChanged.Dispose();
        CollectionInheritanceChanged.Dispose();
        EnabledChanged.Dispose();
        PreSettingsPanelDraw.Dispose();
        PostSettingsPanelDraw.Dispose();
        ChangedItemHover.Dispose();
        ChangedItemClick.Dispose();
    }
}
