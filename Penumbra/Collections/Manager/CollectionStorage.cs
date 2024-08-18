using Dalamud.Interface.ImGuiNotification;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Collections.Manager;

/// <summary> A contiguously incrementing ID managed by the CollectionCreator. </summary>
public readonly record struct LocalCollectionId(int Id) : IAdditionOperators<LocalCollectionId, int, LocalCollectionId>
{
    public static readonly LocalCollectionId Zero = new(0);

    public static LocalCollectionId operator +(LocalCollectionId left, int right)
        => new(left.Id + right);
}

public class CollectionStorage : IReadOnlyList<ModCollection>, IDisposable, IService
{
    private readonly CommunicatorService _communicator;
    private readonly SaveService         _saveService;
    private readonly ModStorage          _modStorage;

    public ModCollection Create(string name, int index, ModCollection? duplicate)
    {
        var newCollection = duplicate?.Duplicate(name, CurrentCollectionId, index)
         ?? ModCollection.CreateEmpty(name, CurrentCollectionId, index, _modStorage.Count);
        _collectionsByLocal[CurrentCollectionId] =  newCollection;
        CurrentCollectionId                      += 1;
        return newCollection;
    }

    public ModCollection CreateFromData(Guid id, string name, int version, Dictionary<string, ModSettings.SavedSettings> allSettings,
        IReadOnlyList<string> inheritances)
    {
        var newCollection = ModCollection.CreateFromData(_saveService, _modStorage, id, name, CurrentCollectionId, version, Count, allSettings,
            inheritances);
        _collectionsByLocal[CurrentCollectionId] =  newCollection;
        CurrentCollectionId                      += 1;
        return newCollection;
    }

    public ModCollection CreateTemporary(string name, int index, int globalChangeCounter)
    {
        var newCollection = ModCollection.CreateTemporary(name, CurrentCollectionId, index, globalChangeCounter);
        _collectionsByLocal[CurrentCollectionId] =  newCollection;
        CurrentCollectionId                      += 1;
        return newCollection;
    }

    public void Delete(ModCollection collection)
        => _collectionsByLocal.Remove(collection.LocalId);

    /// <remarks> The empty collection is always available at Index 0. </remarks>
    private readonly List<ModCollection> _collections =
    [
        ModCollection.Empty,
    ];

    /// <remarks> A list of all collections ever created still existing by their local id. </remarks>
    private readonly Dictionary<LocalCollectionId, ModCollection>
        _collectionsByLocal = new() { [LocalCollectionId.Zero] = ModCollection.Empty };


    public readonly ModCollection DefaultNamed;

    /// <remarks> Incremented by 1 because the empty collection gets Zero. </remarks>
    public LocalCollectionId CurrentCollectionId { get; private set; } = LocalCollectionId.Zero + 1;

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

    /// <summary> Find a collection by its id. If the GUID is empty, the empty collection is returned. </summary>
    public bool ById(Guid id, [NotNullWhen(true)] out ModCollection? collection)
    {
        if (id != Guid.Empty)
            return _collections.FindFirst(c => c.Id == id, out collection);

        collection = ModCollection.Empty;
        return true;
    }

    /// <summary> Find a collection by an identifier, which is interpreted as a GUID first and if it does not correspond to one, as a name. </summary>
    public bool ByIdentifier(string identifier, [NotNullWhen(true)] out ModCollection? collection)
    {
        if (Guid.TryParse(identifier, out var guid))
            return ById(guid, out collection);

        return ByName(identifier, out collection);
    }

    /// <summary> Find a collection by its local ID if it still exists, otherwise returns the empty collection. </summary>
    public ModCollection ByLocalId(LocalCollectionId localId)
        => _collectionsByLocal.TryGetValue(localId, out var coll) ? coll : ModCollection.Empty;

    public CollectionStorage(CommunicatorService communicator, SaveService saveService, ModStorage modStorage)
    {
        _communicator = communicator;
        _saveService  = saveService;
        _modStorage   = modStorage;
        _communicator.ModDiscoveryStarted.Subscribe(OnModDiscoveryStarted, ModDiscoveryStarted.Priority.CollectionStorage);
        _communicator.ModDiscoveryFinished.Subscribe(OnModDiscoveryFinished, ModDiscoveryFinished.Priority.CollectionStorage);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.CollectionStorage);
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.CollectionStorage);
        _communicator.ModFileChanged.Subscribe(OnModFileChanged, ModFileChanged.Priority.CollectionStorage);
        ReadCollections(out DefaultNamed);
    }

    public void Dispose()
    {
        _communicator.ModDiscoveryStarted.Unsubscribe(OnModDiscoveryStarted);
        _communicator.ModDiscoveryFinished.Unsubscribe(OnModDiscoveryFinished);
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
        _communicator.ModFileChanged.Unsubscribe(OnModFileChanged);
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
        if (name.Length == 0)
            return false;

        var newCollection = Create(name, _collections.Count, duplicate);
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

        Delete(collection);
        _saveService.ImmediateDelete(new ModCollectionSave(_modStorage, collection));
        _collections.RemoveAt(collection.Index);
        // Update indices.
        for (var i = collection.Index; i < Count; ++i)
            _collections[i].Index = i;
        _collectionsByLocal.Remove(collection.LocalId);

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
    /// Read all collection files in the Collection Directory.
    /// Ensure that the default named collection exists, and apply inheritances afterward.
    /// Duplicate collection files are not deleted, just not added here.
    /// </summary>
    private void ReadCollections(out ModCollection defaultNamedCollection)
    {
        Penumbra.Log.Debug("[Collections] Reading saved collections...");
        foreach (var file in _saveService.FileNames.CollectionFiles)
        {
            if (!ModCollectionSave.LoadFromFile(file, out var id, out var name, out var version, out var settings, out var inheritance))
                continue;

            if (id == Guid.Empty)
            {
                Penumbra.Messager.NotificationMessage("Collection without ID found.", NotificationType.Warning);
                continue;
            }

            if (ById(id, out _))
            {
                Penumbra.Messager.NotificationMessage($"Duplicate collection found: {id} already exists. Import skipped.",
                    NotificationType.Warning);
                continue;
            }

            var collection  = CreateFromData(id, name, version, settings, inheritance);
            var correctName = _saveService.FileNames.CollectionFile(collection);
            if (file.FullName != correctName)
                try
                {
                    if (version >= 2)
                    {
                        try
                        {
                            File.Move(file.FullName, correctName, false);
                            Penumbra.Messager.NotificationMessage(
                                $"Collection {file.Name} does not correspond to {collection.Identifier}, renamed.",
                                NotificationType.Warning);
                        }
                        catch (Exception ex)
                        {
                            Penumbra.Messager.NotificationMessage(
                                $"Collection {file.Name} does not correspond to {collection.Identifier}, rename failed:\n{ex}",
                                NotificationType.Warning);
                        }
                    }
                    else
                    {
                        _saveService.ImmediateSaveSync(new ModCollectionSave(_modStorage, collection));
                        try
                        {
                            File.Move(file.FullName, file.FullName + ".bak", true);
                            Penumbra.Log.Information($"Migrated collection {name} to Guid {id} with backup of old file.");
                        }
                        catch (Exception ex)
                        {
                            Penumbra.Log.Information($"Migrated collection {name} to Guid {id}, rename of old file failed:\n{ex}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Penumbra.Messager.NotificationMessage(e,
                        $"Collection {file.Name} does not correspond to {collection.Identifier}, but could not rename.",
                        NotificationType.Error);
                }

            _collections.Add(collection);
        }

        defaultNamedCollection = SetDefaultNamedCollection();
        Penumbra.Log.Debug($"[Collections] Found {Count} saved collections.");
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
            $"Unknown problem creating a collection with the name {ModCollection.DefaultCollectionName}, which is required to exist.",
            NotificationType.Error);
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
            case ModPathChangeType.Reloaded:
                foreach (var collection in this)
                {
                    if (collection.Settings[mod.Index]?.Settings.FixAll(mod) ?? false)
                        _saveService.QueueSave(new ModCollectionSave(_modStorage, collection));
                }

                break;
        }
    }

    /// <summary> Save all collections where the mod has settings and the change requires saving. </summary>
    private void OnModOptionChange(ModOptionChangeType type, Mod mod, IModGroup? group, IModOption? option, IModDataContainer? container,
        int movedToIdx)
    {
        type.HandlingInfo(out var requiresSaving, out _, out _);
        if (!requiresSaving)
            return;

        foreach (var collection in this)
        {
            if (collection.Settings[mod.Index]?.HandleChanges(type, mod, group, option, movedToIdx) ?? false)
                _saveService.QueueSave(new ModCollectionSave(_modStorage, collection));
        }
    }

    /// <summary> Update change counters when changing files. </summary>
    private void OnModFileChanged(Mod mod, FileRegistry file)
    {
        if (file.CurrentUsage == 0)
            return;

        foreach (var collection in this)
        {
            var (settings, _) = collection[mod.Index];
            if (settings is { Enabled: true })
                collection.IncrementCounter();
        }
    }
}
