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
    public const int    CurrentVersion    = 1;
    public const string DefaultCollection = "Default";
    public const string EmptyCollection   = "None";

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
    private readonly Dictionary<string, ModSettings.SavedSettings> _unusedSettings;

    // Constructor for duplication.
    private ModCollection(string name, ModCollection duplicate)
    {
        Name               =  name;
        Version            =  duplicate.Version;
        _settings          =  duplicate._settings.ConvertAll(s => s?.DeepCopy());
        _unusedSettings    =  duplicate._unusedSettings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.DeepCopy());
        _inheritance       =  duplicate._inheritance.ToList();
        ModSettingChanged  += SaveOnChange;
        InheritanceChanged += SaveOnChange;
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
        ModSettingChanged  += SaveOnChange;
        InheritanceChanged += SaveOnChange;
    }

    // Create a new, unique empty collection of a given name.
    public static ModCollection CreateNewEmpty(string name)
        => new(name, CurrentVersion, new Dictionary<string, ModSettings.SavedSettings>());

    // Create a new temporary collection that does not save and has a negative index.
    public static ModCollection CreateNewTemporary(string name, int changeCounter)
    {
        var collection = new ModCollection(name, Empty);
        collection.ModSettingChanged  -= collection.SaveOnChange;
        collection.InheritanceChanged -= collection.SaveOnChange;
        collection.Index              =  ~Penumbra.TempCollections.Count;
        collection.ChangeCounter      =  changeCounter;
        collection.CreateCache(false);
        return collection;
    }

    // Duplicate the calling collection to a new, unique collection of a given name.
    public ModCollection Duplicate(string name)
        => new(name, this);

    // Check if a name is valid to use for a collection.
    // Does not check for uniqueness.
    public static bool IsValidName(string name)
        => name.Length > 0 && name.All(c => !c.IsInvalidAscii() && c is not '|' && !c.IsInvalidInPath());

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

    public bool CopyModSettings(int modIdx, string modName, int targetIdx, string targetName)
    {
        if (targetName.Length == 0 && targetIdx < 0 || modName.Length == 0 && modIdx < 0)
            return false;

        // If the source mod exists, convert its settings to saved settings or null if its inheriting.
        // If it does not exist, check unused settings.
        // If it does not exist and has no unused settings, also use null.
        ModSettings.SavedSettings? savedSettings = modIdx >= 0
            ? _settings[modIdx] != null
                ? new ModSettings.SavedSettings(_settings[modIdx]!, Penumbra.ModManager[modIdx])
                : null
            : _unusedSettings.TryGetValue(modName, out var s)
                ? s
                : null;

        if (targetIdx >= 0)
        {
            if (savedSettings != null)
            {
                // The target mod exists and the source settings are not inheriting, convert and fix the settings and copy them.
                // This triggers multiple events.
                savedSettings.Value.ToSettings(Penumbra.ModManager[targetIdx], out var settings);
                SetModState(targetIdx, settings.Enabled);
                SetModPriority(targetIdx, settings.Priority);
                foreach (var (value, index) in settings.Settings.WithIndex())
                    SetModSetting(targetIdx, index, value);
            }
            else
            {
                // The target mod exists, but the source is inheriting, set the target to inheriting.
                // This triggers events.
                SetModInheritance(targetIdx, true);
            }
        }
        else
        {
            // The target mod does not exist.
            // Either copy the unused source settings directly if they are not inheriting,
            // or remove any unused settings for the target if they are inheriting.
            if (savedSettings != null)
                _unusedSettings[targetName] = savedSettings.Value;
            else
                _unusedSettings.Remove(targetName);
        }

        return true;
    }

    public override string ToString()
        => Name;
}
