using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Collections.Manager;
using Penumbra.Services;

namespace Penumbra.Collections;

/// <summary>
/// A ModCollection is a named set of ModSettings to all of the users' installed mods.
/// Settings to mods that are not installed anymore are kept as long as no call to CleanUnavailableSettings is made.
/// Invariants:
///    - Index is the collections index in the ModCollection.Manager
///    - Settings has the same size as ModManager.Mods.
///    - any change in settings or inheritance of the collection causes a Save.
///    - the name can not contain invalid path characters and has to be unique when lower-cased.
/// </summary>
public partial class ModCollection
{
    public const int    CurrentVersion        = 1;
    public const string DefaultCollectionName = "Default";
    public const string EmptyCollectionName   = "None";

    /// <summary>
    /// Create the always available Empty Collection that will always sit at index 0,
    /// can not be deleted and does never create a cache.
    /// </summary>
    public static readonly ModCollection Empty = CreateEmpty(EmptyCollectionName, 0, 0);

    /// <summary> The name of a collection can not contain characters invalid in a path. </summary>
    public string Name { get; internal init; }

    public override string ToString()
        => Name;

    /// <summary> Get the first two letters of a collection name and its Index (or None if it is the empty collection). </summary>
    public string AnonymizedName
        => this == Empty ? Empty.Name : Name.Length > 2 ? $"{Name[..2]}... ({Index})" : $"{Name} ({Index})";

    /// <summary> The index of the collection is set and kept up-to-date by the CollectionManager. </summary>
    public int Index { get; internal set; }

    /// <summary>
    /// Count the number of changes of the effective file list.
    /// This is used for material and imc changes.
    /// </summary>
    public int ChangeCounter { get; private set; }

    /// <summary> Increment the number of changes in the effective file list. </summary>
    public int IncrementCounter()
        => ++ChangeCounter;

    /// <summary>
    /// If a ModSetting is null, it can be inherited from other collections.
    /// If no collection provides a setting for the mod, it is just disabled.
    /// </summary>
    public readonly IReadOnlyList<ModSettings?> Settings;

    /// <summary> Settings for deleted mods will be kept via the mods identifier (directory name). </summary>
    public readonly IReadOnlyDictionary<string, ModSettings.SavedSettings> UnusedSettings;

    /// <summary> Inheritances stored before they can be applied. </summary>
    public IReadOnlyList<string>? InheritanceByName;

    /// <summary> Contains all direct parent collections this collection inherits settings from. </summary>
    public readonly IReadOnlyList<ModCollection> DirectlyInheritsFrom;

    /// <summary> Contains all direct child collections that inherit from this collection. </summary>
    public readonly IReadOnlyList<ModCollection> DirectParentOf = new List<ModCollection>();

    /// <summary> All inherited collections in application order without filtering for duplicates. </summary>
    public static IEnumerable<ModCollection> InheritedCollections(ModCollection collection)
        => collection.DirectlyInheritsFrom.SelectMany(InheritedCollections).Prepend(collection);

    /// <summary>
    /// Iterate over all collections inherited from in depth-first order.
    /// Skip already visited collections to avoid circular dependencies.
    /// </summary>
    public IEnumerable<ModCollection> GetFlattenedInheritance()
        => InheritedCollections(this).Distinct();

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
                var settings = collection.Settings[idx];
                if (settings != null)
                    return (settings, collection);
            }

            return (null, this);
        }
    }

    /// <summary> Evaluates all settings along the whole inheritance tree. </summary>
    public IEnumerable<ModSettings?> ActualSettings
        => Enumerable.Range(0, Settings.Count).Select(i => this[i].Settings);

    /// <summary>
    /// Constructor for duplication. Deep copies all settings and parent collections and adds the new collection to their children lists.
    /// </summary>
    public ModCollection Duplicate(string name, int index)
    {
        Debug.Assert(index > 0, "Collection duplicated with non-positive index.");
        return new ModCollection(name, index, 0, CurrentVersion, Settings.Select(s => s?.DeepCopy()).ToList(),
            DirectlyInheritsFrom.ToList(), UnusedSettings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.DeepCopy()));
    }

    /// <summary> Constructor for reading from files. </summary>
    public static ModCollection CreateFromData(SaveService saver, ModStorage mods, string name, int version, int index,
        Dictionary<string, ModSettings.SavedSettings> allSettings, IReadOnlyList<string> inheritances)
    {
        Debug.Assert(index > 0, "Collection read with non-positive index.");
        var ret = new ModCollection(name, index, 0, version, new List<ModSettings?>(), new List<ModCollection>(), allSettings)
        {
            InheritanceByName = inheritances,
        };
        ret.ApplyModSettings(saver, mods);
        ModCollectionMigration.Migrate(saver, mods, version, ret);
        return ret;
    }

    /// <summary> Constructor for temporary collections. </summary>
    public static ModCollection CreateTemporary(string name, int index, int changeCounter) 
    {
        Debug.Assert(index < 0, "Temporary collection created with non-negative index.");
        var ret = new ModCollection(name, index, changeCounter, CurrentVersion, new List<ModSettings?>(), new List<ModCollection>(),
            new Dictionary<string, ModSettings.SavedSettings>());
        return ret;
    }

    /// <summary> Constructor for empty collections. </summary>
    public static ModCollection CreateEmpty(string name, int index, int modCount)
    {
        Debug.Assert(index >= 0, "Empty collection created with negative index.");
        return new ModCollection(name, index, 0, CurrentVersion, Enumerable.Repeat((ModSettings?) null, modCount).ToList(), new List<ModCollection>(),
            new Dictionary<string, ModSettings.SavedSettings>());
    }

    /// <summary> Add settings for a new appended mod, by checking if the mod had settings from a previous deletion. </summary>
    internal bool AddMod(Mod mod)
    {
        if (UnusedSettings.TryGetValue(mod.ModPath.Name, out var save))
        {
            var ret = save.ToSettings(mod, out var settings);
            ((List<ModSettings?>)Settings).Add(settings);
            ((Dictionary<string, ModSettings.SavedSettings>)UnusedSettings).Remove(mod.ModPath.Name);
            return ret;
        }

        ((List<ModSettings?>)Settings).Add(null);
        return false;
    }

    /// <summary> Move settings from the current mod list to the unused mod settings. </summary>
    internal void RemoveMod(Mod mod)
    {
        var settings = Settings[mod.Index];
        if (settings != null)
            ((Dictionary<string, ModSettings.SavedSettings>)UnusedSettings)[mod.ModPath.Name] = new ModSettings.SavedSettings(settings, mod);

        ((List<ModSettings?>)Settings).RemoveAt(mod.Index);
    }

    /// <summary> Move all settings to unused settings for rediscovery. </summary>
    internal void PrepareModDiscovery(ModStorage mods)
    {
        foreach (var (mod, setting) in mods.Zip(Settings).Where(s => s.Second != null))
            ((Dictionary<string, ModSettings.SavedSettings>)UnusedSettings)[mod.ModPath.Name] = new ModSettings.SavedSettings(setting!, mod);

        ((List<ModSettings?>)Settings).Clear();
    }

    /// <summary>
    /// Apply all mod settings from unused settings to the current set of mods.
    /// Also fixes invalid settings.
    /// </summary>
    internal void ApplyModSettings(SaveService saver, ModStorage mods)
    {
        ((List<ModSettings?>)Settings).Capacity = Math.Max(((List<ModSettings?>)Settings).Capacity, mods.Count);
        if (mods.Aggregate(false, (current, mod) => current | AddMod(mod)))
            saver.ImmediateSave(new ModCollectionSave(mods, this));
    }

    private ModCollection(string name, int index, int changeCounter, int version, List<ModSettings?> appliedSettings,
        List<ModCollection> inheritsFrom, Dictionary<string, ModSettings.SavedSettings> settings)
    {
        Name                 = name;
        Index                = index;
        ChangeCounter        = changeCounter;
        Settings             = appliedSettings;
        UnusedSettings       = settings;
        DirectlyInheritsFrom = inheritsFrom;
        foreach (var c in DirectlyInheritsFrom)
            ((List<ModCollection>)c.DirectParentOf).Add(this);
    }
}
