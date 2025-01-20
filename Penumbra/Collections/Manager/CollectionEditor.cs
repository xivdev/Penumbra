using OtterGui;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Services;

namespace Penumbra.Collections.Manager;

public class CollectionEditor(SaveService saveService, CommunicatorService communicator, ModStorage modStorage) : IService
{
    /// <summary> Enable or disable the mod inheritance of mod idx. </summary>
    public bool SetModInheritance(ModCollection collection, Mod mod, bool inherit)
    {
        if (!FixInheritance(collection, mod, inherit))
            return false;

        InvokeChange(collection, ModSettingChange.Inheritance, mod, inherit ? Setting.False : Setting.True, 0);
        return true;
    }

    /// <summary>
    /// Set the enabled state mod idx to newValue if it differs from the current enabled state.
    /// If the mod is currently inherited, stop the inheritance.
    /// </summary>
    public bool SetModState(ModCollection collection, Mod mod, bool newValue)
    {
        var oldValue = collection.GetInheritedSettings(mod.Index).Settings?.Enabled ?? false;
        if (newValue == oldValue)
            return false;

        var inheritance = FixInheritance(collection, mod, false);
        collection.GetOwnSettings(mod.Index)!.Enabled = newValue;
        InvokeChange(collection, ModSettingChange.EnableState, mod, inheritance ? Setting.Indefinite : newValue ? Setting.False : Setting.True,
            0);
        return true;
    }

    /// <summary> Enable or disable the mod inheritance of every mod in mods. </summary>
    public void SetMultipleModInheritances(ModCollection collection, IEnumerable<Mod> mods, bool inherit)
    {
        if (!mods.Aggregate(false, (current, mod) => current | FixInheritance(collection, mod, inherit)))
            return;

        InvokeChange(collection, ModSettingChange.MultiInheritance, null, Setting.Indefinite, 0);
    }

    /// <summary>
    /// Set the enabled state of every mod in mods to the new value.
    /// If the mod is currently inherited, stop the inheritance.
    /// </summary>
    public void SetMultipleModStates(ModCollection collection, IEnumerable<Mod> mods, bool newValue)
    {
        var changes = false;
        foreach (var mod in mods)
        {
            var oldValue = collection.GetOwnSettings(mod.Index)?.Enabled;
            if (newValue == oldValue)
                continue;

            FixInheritance(collection, mod, false);
            collection.GetOwnSettings(mod.Index)!.Enabled = newValue;
            changes                                       = true;
        }

        if (!changes)
            return;

        InvokeChange(collection, ModSettingChange.MultiEnableState, null, Setting.Indefinite, 0);
    }

    /// <summary>
    /// Set the priority of mod idx to newValue if it differs from the current priority.
    /// If the mod is currently inherited, stop the inheritance.
    /// </summary>
    public bool SetModPriority(ModCollection collection, Mod mod, ModPriority newValue)
    {
        var oldValue = collection.GetInheritedSettings(mod.Index).Settings?.Priority ?? ModPriority.Default;
        if (newValue == oldValue)
            return false;

        var inheritance = FixInheritance(collection, mod, false);
        collection.GetOwnSettings(mod.Index)!.Priority = newValue;
        InvokeChange(collection, ModSettingChange.Priority, mod, inheritance ? Setting.Indefinite : oldValue.AsSetting, 0);
        return true;
    }

    /// <summary>
    /// Set a given setting group settingName of mod idx to newValue if it differs from the current value and fix it if necessary.
    /// If the mod is currently inherited, stop the inheritance.
    /// </summary>
    public bool SetModSetting(ModCollection collection, Mod mod, int groupIdx, Setting newValue)
    {
        var settings = collection.GetInheritedSettings(mod.Index).Settings?.Settings;
        var oldValue = settings?[groupIdx] ?? mod.Groups[groupIdx].DefaultSettings;
        if (oldValue == newValue)
            return false;

        var inheritance = FixInheritance(collection, mod, false);
        collection.GetOwnSettings(mod.Index)!.SetValue(mod, groupIdx, newValue);
        InvokeChange(collection, ModSettingChange.Setting, mod, inheritance ? Setting.Indefinite : oldValue, groupIdx);
        return true;
    }

    public bool SetTemporarySettings(ModCollection collection, Mod mod, TemporaryModSettings? settings, int key = 0)
    {
        key = settings?.Lock ?? key;
        if (!CanSetTemporarySettings(collection, mod, key))
            return false;

        collection.Settings.SetTemporary(mod.Index, settings);
        InvokeChange(collection, ModSettingChange.TemporarySetting, mod, Setting.Indefinite, 0);
        return true;
    }

    public bool CanSetTemporarySettings(ModCollection collection, Mod mod, int key)
    {
        var old = collection.GetTempSettings(mod.Index);
        return old is not { Lock: > 0 } || old.Lock == key;
    }

    /// <summary> Copy the settings of an existing (sourceMod != null) or stored (sourceName) mod to another mod, if they exist. </summary>
    public bool CopyModSettings(ModCollection collection, Mod? sourceMod, string sourceName, Mod? targetMod, string targetName)
    {
        if (targetName.Length == 0 && targetMod == null || sourceName.Length == 0)
            return false;

        // If the source mod exists, convert its settings to saved settings or null if its inheriting.
        // If it does not exist, check unused settings.
        // If it does not exist and has no unused settings, also use null.
        ModSettings.SavedSettings? savedSettings = sourceMod != null
            ? collection.GetOwnSettings(sourceMod.Index) is { } ownSettings
                ? new ModSettings.SavedSettings(ownSettings, sourceMod)
                : null
            : collection.Settings.Unused.TryGetValue(sourceName, out var s)
                ? s
                : null;

        if (targetMod != null)
        {
            if (savedSettings != null)
            {
                // The target mod exists and the source settings are not inheriting, convert and fix the settings and copy them.
                // This triggers multiple events.
                savedSettings.Value.ToSettings(targetMod, out var settings);
                SetModState(collection, targetMod, settings.Enabled);
                SetModPriority(collection, targetMod, settings.Priority);
                foreach (var (value, index) in settings.Settings.WithIndex())
                    SetModSetting(collection, targetMod, index, value);
            }
            else
            {
                // The target mod exists, but the source is inheriting, set the target to inheriting.
                // This triggers events.
                SetModInheritance(collection, targetMod, true);
            }
        }
        else
        {
            // The target mod does not exist.
            // Either copy the unused source settings directly if they are not inheriting,
            // or remove any unused settings for the target if they are inheriting.
            if (savedSettings != null)
            {
                ((Dictionary<string, ModSettings.SavedSettings>)collection.Settings.Unused)[targetName] = savedSettings.Value;
                saveService.QueueSave(new ModCollectionSave(modStorage, collection));
            }
            else if (((Dictionary<string, ModSettings.SavedSettings>)collection.Settings.Unused).Remove(targetName))
            {
                saveService.QueueSave(new ModCollectionSave(modStorage, collection));
            }
        }

        return true;
    }

    /// <summary>
    /// Set inheritance of a mod without saving,
    /// to be used as an intermediary.
    /// </summary>
    private static bool FixInheritance(ModCollection collection, Mod mod, bool inherit)
    {
        var settings = collection.GetOwnSettings(mod.Index);
        if (inherit == (settings == null))
            return false;

        var settings1 = inherit ? null : collection.GetInheritedSettings(mod.Index).Settings?.DeepCopy() ?? ModSettings.DefaultSettings(mod);
        collection.Settings.Set(mod.Index, settings1);
        return true;
    }

    /// <summary> Queue saves and trigger changes for any non-inherited change in a collection, then trigger changes for all inheritors. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void InvokeChange(ModCollection changedCollection, ModSettingChange type, Mod? mod, Setting oldValue, int groupIdx)
    {
        if (type is not ModSettingChange.TemporarySetting)
            saveService.QueueSave(new ModCollectionSave(modStorage, changedCollection));
        communicator.ModSettingChanged.Invoke(changedCollection, type, mod, oldValue, groupIdx, false);
        if (type is not ModSettingChange.TemporarySetting)
            RecurseInheritors(changedCollection, type, mod, oldValue, groupIdx);
    }

    /// <summary> Trigger changes in all inherited collections. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void RecurseInheritors(ModCollection directParent, ModSettingChange type, Mod? mod, Setting oldValue, int groupIdx)
    {
        foreach (var directInheritor in directParent.Inheritance.DirectlyInheritedBy)
        {
            switch (type)
            {
                case ModSettingChange.MultiInheritance:
                case ModSettingChange.MultiEnableState:
                    communicator.ModSettingChanged.Invoke(directInheritor, type, null, oldValue, groupIdx, true);
                    break;
                default:
                    if (directInheritor.GetOwnSettings(mod!.Index) == null)
                        communicator.ModSettingChanged.Invoke(directInheritor, type, mod, oldValue, groupIdx, true);
                    break;
            }

            RecurseInheritors(directInheritor, type, mod, oldValue, groupIdx);
        }
    }
}
