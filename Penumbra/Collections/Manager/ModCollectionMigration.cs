using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;
using Penumbra.Util;

namespace Penumbra.Collections.Manager;

/// <summary> Migration to convert ModCollections from older versions to newer. </summary>
internal static class ModCollectionMigration
{
    /// <summary> Migrate a mod collection to the current version. </summary>
    public static void Migrate(SaveService saver, ModStorage mods, int version, ModCollection collection)
    {
        var changes = MigrateV0ToV1(collection, ref version);
        if (changes)
            saver.ImmediateSave(new ModCollectionSave(mods, collection));
    }

    /// <summary> Migrate a mod collection from Version 0 to Version 1, which introduced support for inheritance. </summary>
    private static bool MigrateV0ToV1(ModCollection collection, ref int version)
    {
        if (version > 0)
            return false;

        version = 1;

        // Remove all completely defaulted settings from active and inactive mods.
        for (var i = 0; i < collection.Settings.Count; ++i)
        {
            if (SettingIsDefaultV0(collection.Settings[i]))
                ((List<ModSettings?>)collection.Settings)[i] = null;
        }

        foreach (var (key, _) in collection.UnusedSettings.Where(kvp => SettingIsDefaultV0(kvp.Value)).ToList())
            ((Dictionary<string, ModSettings.SavedSettings>)collection.UnusedSettings).Remove(key);

        return true;
    }

    /// <summary> We treat every completely defaulted setting as inheritance-ready. </summary>
    private static bool SettingIsDefaultV0(ModSettings.SavedSettings setting)
        => setting is { Enabled: false, Priority: 0 } && setting.Settings.Values.All(s => s == 0);

    /// <inheritdoc cref="SettingIsDefaultV0(ModSettings.SavedSettings)"/>
    private static bool SettingIsDefaultV0(ModSettings? setting)
        => setting is { Enabled: false, Priority: 0 } && setting.Settings.All(s => s == 0);
}
