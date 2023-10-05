using Dalamud.Interface.Internal.Notifications;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Filesystem;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;

namespace Penumbra.Collections.Manager;

public class CollectionStorage : IReadOnlyList<ModCollection>, IDisposable
{
    private readonly CommunicatorService _communicator;
    private readonly SaveService         _saveService;
    private readonly ModStorage          _modStorage;

    /// <remarks> The empty collection is always available at Index 0. </remarks>
    private readonly List<ModCollection> _collections = new()
    {
        ModCollection.Empty,
    };

    public readonly ModCollection DefaultNamed;

    /// <summary> Default enumeration skips the empty collection. </summary>
    public IEnumerator<ModCollection> GetEnumerator()
        => _collections.Skip(1).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _collections.Count;

    public ModCollection this[int index]
        => _collections[index];

    /// <summary> Find a collection by its name. If the name is empty or None, the empty collection is returned. </summary>
    public bool ByName(string name, [NotNullWhen(true)] out ModCollection? collection)
    {
        if (name.Length != 0)
            return _collections.FindFirst(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase), out collection);

        collection = ModCollection.Empty;
        return true;
    }

    public CollectionStorage(CommunicatorService communicator, SaveService saveService, ModStorage modStorage)
    {
        _communicator = communicator;
        _saveService  = saveService;
        _modStorage   = modStorage;
        _communicator.ModDiscoveryStarted.Subscribe(OnModDiscoveryStarted, ModDiscoveryStarted.Priority.CollectionStorage);
        _communicator.ModDiscoveryFinished.Subscribe(OnModDiscoveryFinished, ModDiscoveryFinished.Priority.CollectionStorage);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.CollectionStorage);
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.CollectionStorage);
        ReadCollections(out DefaultNamed);
    }

    public void Dispose()
    {
        _communicator.ModDiscoveryStarted.Unsubscribe(OnModDiscoveryStarted);
        _communicator.ModDiscoveryFinished.Unsubscribe(OnModDiscoveryFinished);
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
    }

    /// <summary>
    /// Returns true if the name is not empty, it is not the name of the empty collection
    /// and no existing collection results in the same filename as name. Also returns the fixed name.
    /// </summary>
    public bool CanAddCollection(string name, out string fixedName)
    {
        if (!IsValidName(name))
        {
            fixedName = string.Empty;
            return false;
        }

        name = name.ToLowerInvariant();
        if (name.Length == 0
         || name == ModCollection.Empty.Name.ToLowerInvariant()
         || _collections.Any(c => c.Name.ToLowerInvariant() == name))
        {
            fixedName = string.Empty;
            return false;
        }

        fixedName = name;
        return true;
    }

    /// <summary>
    /// Add a new collection of the given name.
    /// If duplicate is not-null, the new collection will be a duplicate of it.
    /// If the name of the collection would result in an already existing filename, skip it.
    /// Returns true if the collection was successfully created and fires a Inactive event. 
    /// Also sets the current collection to the new collection afterwards. 
    /// </summary>
    public bool AddCollection(string name, ModCollection? duplicate)
    {
        if (!CanAddCollection(name, out var fixedName))
        {
            Penumbra.Messager.NotificationMessage(
                $"The new collection {name} would lead to the same path {fixedName} as one that already exists.", NotificationType.Warning, false);
            return false;
        }

        var newCollection = duplicate?.Duplicate(name, _collections.Count)
         ?? ModCollection.CreateEmpty(name, _collections.Count, _modStorage.Count);
        _collections.Add(newCollection);

        _saveService.ImmediateSave(new ModCollectionSave(_modStorage, newCollection));
        Penumbra.Messager.NotificationMessage($"Created new collection {newCollection.AnonymizedName}.", NotificationType.Success, false);
        _communicator.CollectionChange.Invoke(CollectionType.Inactive, null, newCollection, string.Empty);
        return true;
    }

    /// <summary>
    /// Remove the given collection if it exists and is neither the empty nor the default-named collection.
    /// </summary>
    public bool RemoveCollection(ModCollection collection)
    {
        if (collection.Index <= ModCollection.Empty.Index || collection.Index >= _collections.Count)
        {
            Penumbra.Messager.NotificationMessage("Can not remove the empty collection.", NotificationType.Error, false);
            return false;
        }

        if (collection.Index == DefaultNamed.Index)
        {
            Penumbra.Messager.NotificationMessage("Can not remove the default collection.", NotificationType.Error, false);
            return false;
        }

        _saveService.ImmediateDelete(new ModCollectionSave(_modStorage, collection));
        _collections.RemoveAt(collection.Index);
        // Update indices.
        for (var i = collection.Index; i < Count; ++i)
            _collections[i].Index = i;

        Penumbra.Messager.NotificationMessage($"Deleted collection {collection.AnonymizedName}.", NotificationType.Success, false);
        _communicator.CollectionChange.Invoke(CollectionType.Inactive, collection, null, string.Empty);
        return true;
    }

    /// <summary> Remove all settings for not currently-installed mods from the given collection. </summary>
    public void CleanUnavailableSettings(ModCollection collection)
    {
        var any = collection.UnusedSettings.Count > 0;
        ((Dictionary<string, ModSettings.SavedSettings>)collection.UnusedSettings).Clear();
        if (any)
            _saveService.QueueSave(new ModCollectionSave(_modStorage, collection));
    }

    /// <summary> Remove a specific setting for not currently-installed mods from the given collection. </summary>
    public void CleanUnavailableSetting(ModCollection collection, string? setting)
    {
        if (setting != null && ((Dictionary<string, ModSettings.SavedSettings>)collection.UnusedSettings).Remove(setting))
            _saveService.QueueSave(new ModCollectionSave(_modStorage, collection));
    }

    /// <summary>
    /// Check if a name is valid to use for a collection.
    /// Does not check for uniqueness.
    /// </summary>
    private static bool IsValidName(string name)
        => name.Length is > 0 and < 64 && name.All(c => !c.IsInvalidAscii() && c is not '|' && !c.IsInvalidInPath());

    /// <summary>
    /// Read all collection files in the Collection Directory.
    /// Ensure that the default named collection exists, and apply inheritances afterwards.
    /// Duplicate collection files are not deleted, just not added here.
    /// </summary>
    private void ReadCollections(out ModCollection defaultNamedCollection)
    {
        foreach (var file in _saveService.FileNames.CollectionFiles)
        {
            if (!ModCollectionSave.LoadFromFile(file, out var name, out var version, out var settings, out var inheritance))
                continue;

            if (!IsValidName(name))
            {
                // TODO: handle better.
                Penumbra.Messager.NotificationMessage($"Collection of unsupported name found: {name} is not a valid collection name.", NotificationType.Warning);
                continue;
            }

            if (ByName(name, out _))
            {
                Penumbra.Messager.NotificationMessage($"Duplicate collection found: {name} already exists. Import skipped.", NotificationType.Warning);
                continue;
            }

            var collection  = ModCollection.CreateFromData(_saveService, _modStorage, name, version, Count, settings, inheritance);
            var correctName = _saveService.FileNames.CollectionFile(collection);
            if (file.FullName != correctName)
                Penumbra.Messager.NotificationMessage($"Collection {file.Name} does not correspond to {collection.Name}.", NotificationType.Warning);
            _collections.Add(collection);
        }

        defaultNamedCollection = SetDefaultNamedCollection();
    }

    /// <summary>
    /// Add the collection with the default name if it does not exist.
    /// It should always be ensured that it exists, otherwise it will be created.
    /// This can also not be deleted, so there are always at least the empty and a collection with default name.
    /// </summary>
    private ModCollection SetDefaultNamedCollection()
    {
        if (ByName(ModCollection.DefaultCollectionName, out var collection))
            return collection;

        if (AddCollection(ModCollection.DefaultCollectionName, null))
            return _collections[^1];

        Penumbra.Messager.NotificationMessage(
            $"Unknown problem creating a collection with the name {ModCollection.DefaultCollectionName}, which is required to exist.", NotificationType.Error);
        return Count > 1 ? _collections[1] : _collections[0];
    }

    /// <summary> Move all settings in all collections to unused settings. </summary>
    private void OnModDiscoveryStarted()
    {
        foreach (var collection in this)
            collection.PrepareModDiscovery(_modStorage);
    }

    /// <summary> Restore all settings in all collections to mods. </summary>
    private void OnModDiscoveryFinished()
    {
        // Re-apply all mod settings.
        foreach (var collection in this)
            collection.ApplyModSettings(_saveService, _modStorage);
    }

    /// <summary> Add or remove a mod from all collections, or re-save all collections where the mod has settings. </summary>
    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? oldDirectory,
        DirectoryInfo? newDirectory)
    {
        switch (type)
        {
            case ModPathChangeType.Added:
                foreach (var collection in this)
                    collection.AddMod(mod);
                break;
            case ModPathChangeType.Deleted:
                foreach (var collection in this)
                    collection.RemoveMod(mod);
                break;
            case ModPathChangeType.Moved:
                foreach (var collection in this.Where(collection => collection.Settings[mod.Index] != null))
                    _saveService.QueueSave(new ModCollectionSave(_modStorage, collection));
                break;
        }
    }

    /// <summary> Save all collections where the mod has settings and the change requires saving. </summary>
    private void OnModOptionChange(ModOptionChangeType type, Mod mod, int groupIdx, int optionIdx, int movedToIdx)
    {
        type.HandlingInfo(out var requiresSaving, out _, out _);
        if (!requiresSaving)
            return;

        foreach (var collection in this)
        {
            if (collection.Settings[mod.Index]?.HandleChanges(type, mod, groupIdx, optionIdx, movedToIdx) ?? false)
                _saveService.QueueSave(new ModCollectionSave(_modStorage, collection));
        }
    }
}
