using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Mods;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Penumbra.Api;
using Penumbra.GameData.Actors;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.Util;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;

namespace Penumbra.Collections;

public sealed partial class CollectionManager : IDisposable, IEnumerable<ModCollection>
{
    private readonly ModManager              _modManager;
    private readonly CommunicatorService     _communicator;
    private readonly SaveService             _saveService;
    private readonly CharacterUtility        _characterUtility;
    private readonly ResidentResourceManager _residentResources;
    private readonly Configuration           _config;


    // The empty collection is always available and always has index 0.
    // It can not be deleted or moved.
    private readonly List<ModCollection> _collections = new()
    {
        ModCollection.Empty,
    };

    public ModCollection this[Index idx]
        => _collections[idx];

    public ModCollection? this[string name]
        => ByName(name, out var c) ? c : null;

    public int Count
        => _collections.Count;

    // Obtain a collection case-independently by name. 
    public bool ByName(string name, [NotNullWhen(true)] out ModCollection? collection)
        => _collections.FindFirst(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase), out collection);

    // Default enumeration skips the empty collection.
    public IEnumerator<ModCollection> GetEnumerator()
        => _collections.Skip(1).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public IEnumerable<ModCollection> GetEnumeratorWithEmpty()
        => _collections;

    public CollectionManager(StartTracker timer, CommunicatorService communicator, FilenameService files, CharacterUtility characterUtility,
        ResidentResourceManager residentResources, Configuration config, ModManager modManager, IndividualCollections individuals,
        SaveService saveService)
    {
        using var time = timer.Measure(StartTimeType.Collections);
        _communicator      = communicator;
        _characterUtility  = characterUtility;
        _residentResources = residentResources;
        _config            = config;
        _modManager        = modManager;
        _saveService       = saveService;
        Individuals        = individuals;

        // The collection manager reacts to changes in mods by itself.
        _communicator.ModDiscoveryStarted.Event      += OnModDiscoveryStarted;
        _communicator.ModDiscoveryFinished.Event     += OnModDiscoveryFinished;
        _communicator.ModOptionChanged.Event         += OnModOptionsChanged;
        _communicator.ModPathChanged.Event           += OnModPathChange;
        _communicator.CollectionChange.Event         += SaveOnChange;
        _communicator.TemporaryGlobalModChange.Event += OnGlobalModChange;
        ReadCollections(files);
        LoadCollections(files);
        UpdateCurrentCollectionInUse();
        CreateNecessaryCaches();
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Event         -= SaveOnChange;
        _communicator.TemporaryGlobalModChange.Event -= OnGlobalModChange;
        _communicator.ModDiscoveryStarted.Event      -= OnModDiscoveryStarted;
        _communicator.ModDiscoveryFinished.Event     -= OnModDiscoveryFinished;
        _communicator.ModOptionChanged.Event         -= OnModOptionsChanged;
        _communicator.ModPathChanged.Event           -= OnModPathChange;
    }

    private void OnGlobalModChange(TemporaryMod mod, bool created, bool removed)
        => TempModManager.OnGlobalModChange(_collections, mod, created, removed);

    // Returns true if the name is not empty, it is not the name of the empty collection
    // and no existing collection results in the same filename as name.
    public bool CanAddCollection(string name, out string fixedName)
    {
        if (!ModCollection.IsValidName(name))
        {
            fixedName = string.Empty;
            return false;
        }

        name = name.RemoveInvalidPathSymbols().ToLowerInvariant();
        if (name.Length == 0
         || name == ModCollection.Empty.Name.ToLowerInvariant()
         || _collections.Any(c => c.Name.RemoveInvalidPathSymbols().ToLowerInvariant() == name))
        {
            fixedName = string.Empty;
            return false;
        }

        fixedName = name;
        return true;
    }

    // Add a new collection of the given name.
    // If duplicate is not-null, the new collection will be a duplicate of it.
    // If the name of the collection would result in an already existing filename, skip it.
    // Returns true if the collection was successfully created and fires a Inactive event.
    // Also sets the current collection to the new collection afterwards.
    public bool AddCollection(string name, ModCollection? duplicate)
    {
        if (!CanAddCollection(name, out var fixedName))
        {
            Penumbra.Log.Warning($"The new collection {name} would lead to the same path {fixedName} as one that already exists.");
            return false;
        }

        var newCollection = duplicate?.Duplicate(name) ?? ModCollection.CreateNewEmpty(name);
        newCollection.Index = _collections.Count;
        _collections.Add(newCollection);

        Penumbra.SaveService.ImmediateSave(newCollection);
        Penumbra.Log.Debug($"Added collection {newCollection.AnonymizedName}.");
        _communicator.CollectionChange.Invoke(CollectionType.Inactive, null, newCollection, string.Empty);
        SetCollection(newCollection.Index, CollectionType.Current);
        return true;
    }

    // Remove the given collection if it exists and is neither the empty nor the default-named collection.
    // If the removed collection was active, it also sets the corresponding collection to the appropriate default.
    // Also removes the collection from inheritances of all other collections.
    public bool RemoveCollection(int idx)
    {
        if (idx <= ModCollection.Empty.Index || idx >= _collections.Count)
        {
            Penumbra.Log.Error("Can not remove the empty collection.");
            return false;
        }

        if (idx == DefaultName.Index)
        {
            Penumbra.Log.Error("Can not remove the default collection.");
            return false;
        }

        if (idx == Current.Index)
            SetCollection(DefaultName.Index, CollectionType.Current);

        if (idx == Default.Index)
            SetCollection(ModCollection.Empty.Index, CollectionType.Default);

        for (var i = 0; i < _specialCollections.Length; ++i)
        {
            if (idx == _specialCollections[i]?.Index)
                SetCollection(ModCollection.Empty, (CollectionType)i);
        }

        for (var i = 0; i < Individuals.Count; ++i)
        {
            if (Individuals[i].Collection.Index == idx)
                SetCollection(ModCollection.Empty, CollectionType.Individual, i);
        }

        var collection = _collections[idx];

        // Clear own inheritances.
        foreach (var inheritance in collection.Inheritance)
            collection.ClearSubscriptions(inheritance);

        Penumbra.SaveService.ImmediateDelete(collection);
        _collections.RemoveAt(idx);

        // Clear external inheritances.
        foreach (var c in _collections)
        {
            var inheritedIdx = c._inheritance.IndexOf(collection);
            if (inheritedIdx >= 0)
                c.RemoveInheritance(inheritedIdx);

            if (c.Index > idx)
                --c.Index;
        }

        Penumbra.Log.Debug($"Removed collection {collection.AnonymizedName}.");
        _communicator.CollectionChange.Invoke(CollectionType.Inactive, collection, null, string.Empty);
        return true;
    }

    public bool RemoveCollection(ModCollection collection)
        => RemoveCollection(collection.Index);

    private void OnModDiscoveryStarted()
    {
        foreach (var collection in this)
            collection.PrepareModDiscovery();
    }

    private void OnModDiscoveryFinished()
    {
        // First, re-apply all mod settings.
        foreach (var collection in this)
            collection.ApplyModSettings();

        // Afterwards, we update the caches. This can not happen in the same loop due to inheritance.
        foreach (var collection in this.Where(c => c.HasCache))
            collection.ForceCacheUpdate();
    }


    // A changed mod path forces changes for all collections, active and inactive.
    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? oldDirectory,
        DirectoryInfo? newDirectory)
    {
        switch (type)
        {
            case ModPathChangeType.Added:
                foreach (var collection in this)
                    collection.AddMod(mod);

                OnModAddedActive(mod);
                break;
            case ModPathChangeType.Deleted:
                OnModRemovedActive(mod);
                foreach (var collection in this)
                    collection.RemoveMod(mod, mod.Index);

                break;
            case ModPathChangeType.Moved:
                OnModMovedActive(mod);
                foreach (var collection in this.Where(collection => collection.Settings[mod.Index] != null))
                    Penumbra.SaveService.QueueSave(collection);

                break;
            case ModPathChangeType.StartingReload:
                OnModRemovedActive(mod);
                break;
            case ModPathChangeType.Reloaded:
                OnModAddedActive(mod);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    // Automatically update all relevant collections when a mod is changed.
    // This means saving if options change in a way where the settings may change and the collection has settings for this mod.
    // And also updating effective file and meta manipulation lists if necessary.
    private void OnModOptionsChanged(ModOptionChangeType type, Mod mod, int groupIdx, int optionIdx, int movedToIdx)
    {
        // Handle changes that break revertability.
        if (type == ModOptionChangeType.PrepareChange)
        {
            foreach (var collection in this.Where(c => c.HasCache))
            {
                if (collection[mod.Index].Settings is { Enabled: true })
                    collection._cache!.RemoveMod(mod, false);
            }

            return;
        }

        type.HandlingInfo(out var requiresSaving, out var recomputeList, out var reload);

        // Handle changes that require overwriting the collection.
        if (requiresSaving)
            foreach (var collection in this)
            {
                if (collection._settings[mod.Index]?.HandleChanges(type, mod, groupIdx, optionIdx, movedToIdx) ?? false)
                    _saveService.QueueSave(collection);
            }

        // Handle changes that reload the mod if the changes did not need to be prepared,
        // or re-add the mod if they were prepared.
        if (recomputeList)
            foreach (var collection in this.Where(c => c.HasCache))
            {
                if (collection[mod.Index].Settings is { Enabled: true })
                {
                    if (reload)
                        collection._cache!.ReloadMod(mod, true);
                    else
                        collection._cache!.AddMod(mod, true);
                }
            }
    }

    // Add the collection with the default name if it does not exist.
    // It should always be ensured that it exists, otherwise it will be created.
    // This can also not be deleted, so there are always at least the empty and a collection with default name.
    private void AddDefaultCollection()
    {
        var idx = GetIndexForCollectionName(ModCollection.DefaultCollection);
        if (idx >= 0)
        {
            DefaultName = this[idx];
            return;
        }

        var defaultCollection = ModCollection.CreateNewEmpty((string)ModCollection.DefaultCollection);
        _saveService.ImmediateSave(defaultCollection);
        defaultCollection.Index = _collections.Count;
        _collections.Add(defaultCollection);
    }

    // Inheritances can not be setup before all collections are read,
    // so this happens after reading the collections.
    private void ApplyInheritances(IEnumerable<IReadOnlyList<string>> inheritances)
    {
        foreach (var (collection, inheritance) in this.Zip(inheritances))
        {
            var changes = false;
            foreach (var subCollectionName in inheritance)
            {
                if (!ByName(subCollectionName, out var subCollection))
                {
                    changes = true;
                    Penumbra.Log.Warning($"Inherited collection {subCollectionName} for {collection.Name} does not exist, removed.");
                }
                else if (!collection.AddInheritance(subCollection, false))
                {
                    changes = true;
                    Penumbra.Log.Warning($"{collection.Name} can not inherit from {subCollectionName}, removed.");
                }
            }

            if (changes)
                _saveService.ImmediateSave(collection);
        }
    }

    // Read all collection files in the Collection Directory.
    // Ensure that the default named collection exists, and apply inheritances afterwards.
    // Duplicate collection files are not deleted, just not added here.
    private void ReadCollections(FilenameService files)
    {
        var inheritances = new List<IReadOnlyList<string>>();
        foreach (var file in files.CollectionFiles)
        {
            var collection = ModCollection.LoadFromFile(file, out var inheritance);
            if (collection == null || collection.Name.Length == 0)
                continue;

            if (file.Name != $"{collection.Name.RemoveInvalidPathSymbols()}.json")
                Penumbra.Log.Warning($"Collection {file.Name} does not correspond to {collection.Name}.");

            if (this[collection.Name] != null)
            {
                Penumbra.Log.Warning($"Duplicate collection found: {collection.Name} already exists.");
            }
            else
            {
                inheritances.Add(inheritance);
                collection.Index = _collections.Count;
                _collections.Add(collection);
            }
        }

        AddDefaultCollection();
        ApplyInheritances(inheritances);
    }

    public string RedundancyCheck(CollectionType type, ActorIdentifier id)
    {
        var checkAssignment = ByType(type, id);
        if (checkAssignment == null)
            return string.Empty;

        switch (type)
        {
            // Check individual assignments. We can only be sure of redundancy for world-overlap or ownership overlap.
            case CollectionType.Individual:
                switch (id.Type)
                {
                    case IdentifierType.Player when id.HomeWorld != ushort.MaxValue:
                    {
                        var global = ByType(CollectionType.Individual, Penumbra.Actors.CreatePlayer(id.PlayerName, ushort.MaxValue));
                        return global?.Index == checkAssignment.Index
                            ? "Assignment is redundant due to an identical Any-World assignment existing.\nYou can remove it."
                            : string.Empty;
                    }
                    case IdentifierType.Owned:
                        if (id.HomeWorld != ushort.MaxValue)
                        {
                            var global = ByType(CollectionType.Individual,
                                Penumbra.Actors.CreateOwned(id.PlayerName, ushort.MaxValue, id.Kind, id.DataId));
                            if (global?.Index == checkAssignment.Index)
                                return "Assignment is redundant due to an identical Any-World assignment existing.\nYou can remove it.";
                        }

                        var unowned = ByType(CollectionType.Individual, Penumbra.Actors.CreateNpc(id.Kind, id.DataId));
                        return unowned?.Index == checkAssignment.Index
                            ? "Assignment is redundant due to an identical unowned NPC assignment existing.\nYou can remove it."
                            : string.Empty;
                }

                break;
            // The group of all Characters is redundant if they are all equal to Default or unassigned.
            case CollectionType.MalePlayerCharacter:
            case CollectionType.MaleNonPlayerCharacter:
            case CollectionType.FemalePlayerCharacter:
            case CollectionType.FemaleNonPlayerCharacter:
                var first  = ByType(CollectionType.MalePlayerCharacter) ?? Default;
                var second = ByType(CollectionType.MaleNonPlayerCharacter) ?? Default;
                var third  = ByType(CollectionType.FemalePlayerCharacter) ?? Default;
                var fourth = ByType(CollectionType.FemaleNonPlayerCharacter) ?? Default;
                if (first.Index == second.Index
                 && first.Index == third.Index
                 && first.Index == fourth.Index
                 && first.Index == Default.Index)
                    return
                        "Assignment is currently redundant due to the group [Male, Female, Player, NPC] Characters being unassigned or identical to each other and Default.\n"
                      + "You can keep just the Default Assignment.";

                break;
            // Children and Elderly are redundant if they are identical to both Male NPCs and Female NPCs, or if they are unassigned to Default.
            case CollectionType.NonPlayerChild:
            case CollectionType.NonPlayerElderly:
                var maleNpc     = ByType(CollectionType.MaleNonPlayerCharacter);
                var femaleNpc   = ByType(CollectionType.FemaleNonPlayerCharacter);
                var collection1 = CollectionType.MaleNonPlayerCharacter;
                var collection2 = CollectionType.FemaleNonPlayerCharacter;
                if (maleNpc == null)
                {
                    maleNpc = Default;
                    if (maleNpc.Index != checkAssignment.Index)
                        return string.Empty;

                    collection1 = CollectionType.Default;
                }

                if (femaleNpc == null)
                {
                    femaleNpc = Default;
                    if (femaleNpc.Index != checkAssignment.Index)
                        return string.Empty;

                    collection2 = CollectionType.Default;
                }

                return collection1 == collection2
                    ? $"Assignment is currently redundant due to overwriting {collection1.ToName()} with an identical collection.\nYou can remove them."
                    : $"Assignment is currently redundant due to overwriting {collection1.ToName()} and {collection2.ToName()} with an identical collection.\nYou can remove them.";

            // For other assignments, check the inheritance order, unassigned means fall-through,
            // assigned needs identical assignments to be redundant.
            default:
                var group = type.InheritanceOrder();
                foreach (var parentType in group)
                {
                    var assignment = ByType(parentType);
                    if (assignment == null)
                        continue;

                    if (assignment.Index == checkAssignment.Index)
                        return
                            $"Assignment is currently redundant due to overwriting {parentType.ToName()} with an identical collection.\nYou can remove it.";
                }

                break;
        }

        return string.Empty;
    }
}
