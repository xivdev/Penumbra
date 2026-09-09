using ImSharp;
using Penumbra.Api.Enums;
using Penumbra.Api.Preset;
using Penumbra.Collections.Manager;
using Penumbra.Files;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;

namespace Penumbra.Collections;

/// <summary>
/// A ModCollection is a named set of ModSettings to all the users' installed mods.
/// Settings to mods that are not installed anymore are kept as long as no call to CleanUnavailableSettings is made.
/// Invariants:
///    - Index is the collections index in the ModCollection.Manager
///    - Settings has the same size as ModManager.Mods.
///    - any change in settings or inheritance of the collection causes a Save.
/// </summary>
public partial class ModCollection
{
    public const int CurrentVersion = 2;

    /// <summary>
    /// Create the always available Empty Collection that will always sit at index 0,
    /// can not be deleted and does never create a cache.
    /// </summary>
    public static readonly ModCollection Empty = new(ModCollectionIdentity.Empty, 0, CurrentVersion, new ModSettingProvider(),
        new ModCollectionInheritance());

    public ModCollectionIdentity Identity;

    public override string ToString()
        => Identity.ToString();

    public readonly ModSettingProvider       Settings;
    public          ModCollectionInheritance Inheritance;
    public          CollectionCounters       Counters;


    public ModSettings? GetOwnSettings(Index idx)
    {
        if (Identity.Index <= 0)
            return ModSettings.Empty;

        return Settings.Settings[idx].Settings;
    }

    public TemporaryModSettings? GetTempSettings(Index idx)
    {
        if (Identity.Index <= 0)
            return null;

        return Settings.Settings[idx].TempSettings;
    }

    public (ModSettings? Settings, ModCollection Collection) GetInheritedSettings(Index idx)
    {
        if (Identity.Index <= 0)
            return (ModSettings.Empty, this);

        foreach (var collection in Inheritance.FlatHierarchy)
        {
            var settings = collection.Settings.Settings[idx].Settings;
            if (settings != null)
                return (settings, collection);
        }

        return (null, this);
    }

    public (ModSettings? Settings, ModCollection Collection) GetActualSettings(Index idx)
    {
        if (Identity.Index <= 0)
            return (ModSettings.Empty, this);

        // Check temp settings.
        var ownTempSettings = Settings.Settings[idx].Resolve();
        if (ownTempSettings != null)
            return (ownTempSettings, this);

        // Ignore temp settings for inherited collections.
        foreach (var collection in Inheritance.FlatHierarchy.Skip(1))
        {
            var settings = collection.Settings.Settings[idx].Settings;
            if (settings != null)
                return (settings, collection);
        }

        return (null, this);
    }

    /// <summary> Evaluates all settings along the whole inheritance tree. </summary>
    public IEnumerable<ModSettings?> ActualSettings
        => Enumerable.Range(0, Settings.Count).Select(i => GetActualSettings(i).Settings);

    /// <summary>
    /// Constructor for duplication. Deep copies all settings and parent collections and adds the new collection to their children lists.
    /// </summary>
    public ModCollection Duplicate(string name, LocalCollectionId localId, int index)
    {
        Debug.Assert(index > 0, "Collection duplicated with non-positive index.");
        return new ModCollection(ModCollectionIdentity.New(name, localId, index), 0, CurrentVersion, Settings.Clone(), Inheritance.Clone());
    }

    /// <summary> Constructor for reading from files. </summary>
    public static ModCollection CreateFromData(SaveService saver, ModStorage mods, ModCollectionIdentity identity, int version,
        Dictionary<string, ModSettings.SavedSettings> allSettings, IReadOnlyList<string> inheritances)
    {
        Debug.Assert(identity.Index > 0, "Collection read with non-positive index.");
        var ret = new ModCollection(identity, 0, version, new ModSettingProvider(allSettings), new ModCollectionInheritance(inheritances));
        ret.Settings.ApplyModSettings(ret, saver, mods);
        ModCollectionMigration.Migrate(saver, mods, version, ret);
        return ret;
    }

    /// <summary> Constructor for temporary collections. </summary>
    public static ModCollection CreateTemporary(string name, LocalCollectionId localId, int index, int changeCounter)
    {
        Debug.Assert(index < 0, "Temporary collection created with non-negative index.");
        var ret = new ModCollection(ModCollectionIdentity.New(name, localId, index), changeCounter, CurrentVersion, new ModSettingProvider(),
            new ModCollectionInheritance());
        return ret;
    }

    /// <summary> Constructor for empty collections. </summary>
    public static ModCollection CreateEmpty(string name, LocalCollectionId localId, int index, int modCount)
    {
        Debug.Assert(index >= 0, "Empty collection created with negative index.");
        return new ModCollection(ModCollectionIdentity.New(name, localId, index), 0, CurrentVersion, ModSettingProvider.Empty(modCount),
            new ModCollectionInheritance());
    }

    public SettingPresetData? GetPreset(Mod mod, PresetQueryMode mode, int _)
    {
        // In order:
        // - use null settings if we do not care about actual settings (IgnoreSettings, GetDefault)
        // - use the own setting configuration of the collection, or null settings if not configured, if we ignore temporary and inheritance
        // - use the own or inherited setting configuration of the collection or null settings if not configured nor inherited, if we ignore temporary.
        // - use the actual temporary settings if those are not set to inherited, or null if they are, or the own settings, or null if they are not configured, if we ignore inheritance
        // - use the actual settings (which may be null) if we ignore nothing.
        var relevantSettings = ModSettings.Empty;
        if (!mode.CheckAny(PresetQueryMode.GetDefault | PresetQueryMode.IgnoreSettings))
        {
            if (mode.HasFlag(PresetQueryMode.IgnoreTemporary))
            {
                relevantSettings = mode.HasFlag(PresetQueryMode.IgnoreInheritance)
                    ? GetOwnSettings(mod.Index)
                    : GetInheritedSettings(mod.Index).Settings;
            }
            else
            {
                if (mode.HasFlag(PresetQueryMode.IgnoreInheritance))
                    relevantSettings = GetTempSettings(mod.Index) is { } s
                        ? s.ForceInherit ? null : s
                        : GetOwnSettings(mod.Index);
                else
                    relevantSettings = GetActualSettings(mod.Index).Settings;
            }
        }

        var preset = SettingPresetData.FromMod(mod, relevantSettings);
        if (mode.HasFlag(PresetQueryMode.IgnoreDisabled))
            foreach (var group in preset.Settings.Values)
            {
                var tmp = group.Options.Where(g => g.Value is not (byte)OptionState.Disabled).ToList();
                group.Options.Clear();
                foreach (var (option, value) in tmp)
                    group.Options.Add(option, value);
            }

        if (mode.HasFlag(PresetQueryMode.IgnoreSettings))
        {
            foreach (var group in preset.Settings.Values)
            {
                foreach (var option in group.Options.Keys)
                    group.Options[option] = (byte)OptionState.Ignored;
            }

            preset._hasPriority = false;
            preset._state       = (byte)ModState.Ignored;
        }

        return preset;
    }

    private ModCollection(ModCollectionIdentity identity, int changeCounter, int version, ModSettingProvider settings,
        ModCollectionInheritance inheritance)
    {
        Identity    = identity;
        Counters    = new CollectionCounters(changeCounter);
        Settings    = settings;
        Inheritance = inheritance;
        ModCollectionInheritance.UpdateChildren(this);
        ModCollectionInheritance.UpdateFlattenedInheritance(this);
    }
}
