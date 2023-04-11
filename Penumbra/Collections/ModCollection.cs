using OtterGui.Filesystem;
using Penumbra.Mods;
using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui;

namespace Penumbra.Collections;

// A ModCollection is a named set of ModSettings to all of the users' installed mods.
// Settings to mods that are not installed anymore are kept as long as no call to CleanUnavailableSettings is made.
// Invariants:
//    - Index is the collections index in the ModCollection.Manager
//    - Settings has the same size as ModManager.Mods.
//    - any change in settings or inheritance of the collection causes a Save.
public partial class ModCollection
{
    public const int    CurrentVersion        = 1;
    public const string DefaultCollectionName = "Default";
    public const string EmptyCollection       = "None";

    public static readonly ModCollection Empty = CreateEmpty();

    // The collection name can contain invalid path characters,
    // but after removing those and going to lower case it has to be unique.
    public string Name { get; internal init; }

    // Get the first two letters of a collection name and its Index (or None if it is the empty collection).
    public string AnonymizedName
        => this == Empty ? Empty.Name : Name.Length > 2 ? $"{Name[..2]}... ({Index})" : $"{Name} ({Index})";

    public int Version { get; internal set; }
    public int Index   { get; internal set; } = -1;

    // If a ModSetting is null, it can be inherited from other collections.
    // If no collection provides a setting for the mod, it is just disabled.
    internal readonly List<ModSettings?> _settings;

    public IReadOnlyList<ModSettings?> Settings
        => _settings;

    // Returns whether there are settings not in use by any current mod.
    public bool HasUnusedSettings
        => _unusedSettings.Count > 0;

    public int NumUnusedSettings
        => _unusedSettings.Count;

    // Evaluates the settings along the whole inheritance tree.
    public IEnumerable<ModSettings?> ActualSettings
        => Enumerable.Range(0, _settings.Count).Select(i => this[i].Settings);

    // Settings for deleted mods will be kept via directory name.
    internal readonly Dictionary<string, ModSettings.SavedSettings> _unusedSettings;

    // Constructor for duplication.
    private ModCollection(string name, ModCollection duplicate)
    {
        Name                 = name;
        Version              = duplicate.Version;
        _settings            = duplicate._settings.ConvertAll(s => s?.DeepCopy());
        _unusedSettings      = duplicate._unusedSettings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.DeepCopy());
        DirectlyInheritsFrom = duplicate.DirectlyInheritsFrom.ToList();
        foreach (var c in DirectlyInheritsFrom)
            ((List<ModCollection>)c.DirectParentOf).Add(this);
    }

    // Constructor for reading from files.
    private ModCollection(string name, int version, Dictionary<string, ModSettings.SavedSettings> allSettings)
    {
        Name            = name;
        Version         = version;
        _unusedSettings = allSettings;

        _settings = new List<ModSettings?>();
        ApplyModSettings();

        Migration.Migrate(Penumbra.SaveService, this);
    }

    // Create a new, unique empty collection of a given name.
    public static ModCollection CreateNewEmpty(string name)
        => new(name, CurrentVersion, new Dictionary<string, ModSettings.SavedSettings>());

    // Create a new temporary collection that does not save and has a negative index.
    public static ModCollection CreateNewTemporary(string name, int changeCounter)
    {
        var collection = new ModCollection(name, Empty)
        {
            Index         = ~Penumbra.TempCollections.Count,
            ChangeCounter = changeCounter,
        };
        collection.CreateCache(false);
        return collection;
    }

    // Duplicate the calling collection to a new, unique collection of a given name.
    public ModCollection Duplicate(string name)
        => new(name, this);

    // Remove all settings for not currently-installed mods.
    public void CleanUnavailableSettings()
    {
        var any = _unusedSettings.Count > 0;
        _unusedSettings.Clear();
        if (any)
            Penumbra.SaveService.QueueSave(this);
    }

    // Add settings for a new appended mod, by checking if the mod had settings from a previous deletion.
    internal bool AddMod(Mod mod)
    {
        if (_unusedSettings.TryGetValue(mod.ModPath.Name, out var save))
        {
            var ret = save.ToSettings(mod, out var settings);
            _settings.Add(settings);
            _unusedSettings.Remove(mod.ModPath.Name);
            return ret;
        }

        _settings.Add(null);
        return false;
    }

    // Move settings from the current mod list to the unused mod settings.
    internal void RemoveMod(Mod mod, int idx)
    {
        var settings = _settings[idx];
        if (settings != null)
            _unusedSettings[mod.ModPath.Name] = new ModSettings.SavedSettings(settings, mod);

        _settings.RemoveAt(idx);
    }

    // Create the always available Empty Collection that will always sit at index 0,
    // can not be deleted and does never create a cache.
    private static ModCollection CreateEmpty()
    {
        var collection = CreateNewEmpty(EmptyCollection);
        collection.Index = 0;
        collection._settings.Clear();
        return collection;
    }

    // Move all settings to unused settings for rediscovery.
    internal void PrepareModDiscovery()
    {
        foreach (var (mod, setting) in Penumbra.ModManager.Zip(_settings).Where(s => s.Second != null))
            _unusedSettings[mod.ModPath.Name] = new ModSettings.SavedSettings(setting!, mod);

        _settings.Clear();
    }

    // Apply all mod settings from unused settings to the current set of mods.
    // Also fixes invalid settings.
    internal void ApplyModSettings()
    {
        _settings.Capacity = Math.Max(_settings.Capacity, Penumbra.ModManager.Count);
        if (Penumbra.ModManager.Aggregate(false, (current, mod) => current | AddMod(mod)))
            Penumbra.SaveService.ImmediateSave(this);
    }

    public override string ToString()
        => Name;

    /// <summary>
    /// Obtain the actual settings for a given mod via index.
    /// Also returns the collection the settings are taken from.
    /// If no collection provides settings for this mod, this collection is returned together with null.
    /// </summary>
    public (ModSettings? Settings, ModCollection Collection) this[Index idx]
    {
        get
        {
            if (Index <= 0)
                return (ModSettings.Empty, this);

            foreach (var collection in GetFlattenedInheritance())
            {
                var settings = collection._settings[idx];
                if (settings != null)
                    return (settings, collection);
            }

            return (null, this);
        }
    }

    public readonly IReadOnlyList<ModCollection> DirectlyInheritsFrom = new List<ModCollection>();
    public readonly IReadOnlyList<ModCollection> DirectParentOf       = new List<ModCollection>();

    /// <summary> All inherited collections in application order without filtering for duplicates. </summary>
    public static IEnumerable<ModCollection> InheritedCollections(ModCollection collection)
        => collection.DirectlyInheritsFrom.SelectMany(InheritedCollections).Prepend(collection);

    /// <summary>
    /// Iterate over all collections inherited from in depth-first order.
    /// Skip already visited collections to avoid circular dependencies.
    /// </summary>
    public IEnumerable<ModCollection> GetFlattenedInheritance()
        => InheritedCollections(this).Distinct();
}
