using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Services;

namespace Penumbra.Collections.Manager;

/// <summary> Migration to convert ModCollections from older versions to newer. </summary>
internal static class ModCollectionMigration
{
    /// <summary> Migrate a mod collection to the current version. </summary>
    public static void Migrate(SaveService saver, ModStorage mods, int version, ModCollection collection)
    {
        var changes = MigrateV0ToV1(collection, ref version);
        if (changes)
            saver.ImmediateSaveSync(new ModCollectionSave(mods, collection));
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
            if (SettingIsDefaultV0(collection.GetOwnSettings(i)))
                collection.Settings.SetAll(i, FullModSettings.Empty);
        }

        foreach (var (key, _) in collection.Settings.Unused.Where(kvp => SettingIsDefaultV0(kvp.Value)).ToList())
            collection.Settings.RemoveUnused(key);

        return true;
    }

    /// <summary> We treat every completely defaulted setting as inheritance-ready. </summary>
    private static bool SettingIsDefaultV0(ModSettings.SavedSettings setting)
        => setting is { Enabled: true, Priority.IsDefault: true } && setting.Settings.Values.All(s => s == Setting.Zero);

    /// <inheritdoc cref="SettingIsDefaultV0(ModSettings.SavedSettings)"/>
    private static bool SettingIsDefaultV0(ModSettings? setting)
        => setting is { Enabled: true, Priority.IsDefault: true } && setting.Settings.All(s => s == Setting.Zero);
}
