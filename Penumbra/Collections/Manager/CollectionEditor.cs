using OtterGui;
using Penumbra.Api.Enums;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Collections.Manager;

public class CollectionEditor
{
    private readonly CommunicatorService _communicator;
    private readonly SaveService         _saveService;
    private readonly ModStorage          _modStorage;

    public CollectionEditor(SaveService saveService, CommunicatorService communicator, ModStorage modStorage)
    {
        _saveService  = saveService;
        _communicator = communicator;
        _modStorage   = modStorage;
    }

    /// <summary> Enable or disable the mod inheritance of mod idx. </summary>
    public bool SetModInheritance(ModCollection collection, Mod mod, bool inherit)
    {
        if (!FixInheritance(collection, mod, inherit))
            return false;

        InvokeChange(collection, ModSettingChange.Inheritance, mod, inherit ? 0 : 1, 0);
        return true;
    }

    /// <summary>
    /// Set the enabled state mod idx to newValue if it differs from the current enabled state.
    /// If the mod is currently inherited, stop the inheritance.
    /// </summary>
    public bool SetModState(ModCollection collection, Mod mod, bool newValue)
    {
        var oldValue = collection.Settings[mod.Index]?.Enabled ?? collection[mod.Index].Settings?.Enabled ?? false;
        if (newValue == oldValue)
            return false;

        var inheritance = FixInheritance(collection, mod, false);
        ((List<ModSettings?>)collection.Settings)[mod.Index]!.Enabled = newValue;
        InvokeChange(collection, ModSettingChange.EnableState, mod, inheritance ? -1 : newValue ? 0 : 1, 0);
        return true;
    }

    /// <summary> Enable or disable the mod inheritance of every mod in mods. </summary>
    public void SetMultipleModInheritances(ModCollection collection, IEnumerable<Mod> mods, bool inherit)
    {
        if (!mods.Aggregate(false, (current, mod) => current | FixInheritance(collection, mod, inherit)))
            return;

        InvokeChange(collection, ModSettingChange.MultiInheritance, null, -1, 0);
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
            var oldValue = collection.Settings[mod.Index]?.Enabled;
            if (newValue == oldValue)
                continue;

            FixInheritance(collection, mod, false);
            ((List<ModSettings?>)collection.Settings)[mod.Index]!.Enabled = newValue;
            changes                                                       = true;
        }

        if (!changes)
            return;

        InvokeChange(collection, ModSettingChange.MultiEnableState, null, -1, 0);
    }

    /// <summary>
    /// Set the priority of mod idx to newValue if it differs from the current priority.
    /// If the mod is currently inherited, stop the inheritance.
    /// </summary>
    public bool SetModPriority(ModCollection collection, Mod mod, int newValue)
    {
        var oldValue = collection.Settings[mod.Index]?.Priority ?? collection[mod.Index].Settings?.Priority ?? 0;
        if (newValue == oldValue)
            return false;

        var inheritance = FixInheritance(collection, mod, false);
        ((List<ModSettings?>)collection.Settings)[mod.Index]!.Priority = newValue;
        InvokeChange(collection, ModSettingChange.Priority, mod, inheritance ? -1 : oldValue, 0);
        return true;
    }

    /// <summary>
    /// Set a given setting group settingName of mod idx to newValue if it differs from the current value and fix it if necessary.
    /// /// If the mod is currently inherited, stop the inheritance.
    /// </summary>
    public bool SetModSetting(ModCollection collection, Mod mod, int groupIdx, uint newValue)
    {
        var settings = collection.Settings[mod.Index] != null
            ? collection.Settings[mod.Index]!.Settings
            : collection[mod.Index].Settings?.Settings;
        var oldValue = settings?[groupIdx] ?? mod.Groups[groupIdx].DefaultSettings;
        if (oldValue == newValue)
            return false;

        var inheritance = FixInheritance(collection, mod, false);
        ((List<ModSettings?>)collection.Settings)[mod.Index]!.SetValue(mod, groupIdx, newValue);
        InvokeChange(collection, ModSettingChange.Setting, mod, inheritance ? -1 : (int)oldValue, groupIdx);
        return true;
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
            ? collection.Settings[sourceMod.Index] != null
                ? new ModSettings.SavedSettings(collection.Settings[sourceMod.Index]!, sourceMod)
                : null
            : collection.UnusedSettings.TryGetValue(sourceName, out var s)
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
                ((Dictionary<string, ModSettings.SavedSettings>)collection.UnusedSettings)[targetName] = savedSettings.Value;
            else
                ((Dictionary<string, ModSettings.SavedSettings>)collection.UnusedSettings).Remove(targetName);
        }

        return true;
    }

    /// <summary>
    /// Change one of the available mod settings for mod idx discerned by type.
    /// If type == Setting, settingName should be a valid setting for that mod, otherwise it will be ignored.
    /// The setting will also be automatically fixed if it is invalid for that setting group.
    /// For boolean parameters, newValue == 0 will be treated as false and != 0 as true.
    /// </summary>
    public bool ChangeModSetting(ModCollection collection, ModSettingChange type, Mod mod, int newValue, int groupIdx)
    {
        return type switch
        {
            ModSettingChange.Inheritance => SetModInheritance(collection, mod, newValue != 0),
            ModSettingChange.EnableState => SetModState(collection, mod, newValue != 0),
            ModSettingChange.Priority    => SetModPriority(collection, mod, newValue),
            ModSettingChange.Setting     => SetModSetting(collection, mod, groupIdx, (uint)newValue),
            _                            => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    /// <summary>
    /// Set inheritance of a mod without saving,
    /// to be used as an intermediary.
    /// </summary>
    private static bool FixInheritance(ModCollection collection, Mod mod, bool inherit)
    {
        var settings = collection.Settings[mod.Index];
        if (inherit == (settings == null))
            return false;

        ((List<ModSettings?>)collection.Settings)[mod.Index] =
            inherit ? null : collection[mod.Index].Settings?.DeepCopy() ?? ModSettings.DefaultSettings(mod);
        return true;
    }

    /// <summary> Queue saves and trigger changes for any non-inherited change in a collection, then trigger changes for all inheritors. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void InvokeChange(ModCollection changedCollection, ModSettingChange type, Mod? mod, int oldValue, int groupIdx)
    {
        _saveService.QueueSave(new ModCollectionSave(_modStorage, changedCollection));
        _communicator.ModSettingChanged.Invoke(changedCollection, type, mod, oldValue, groupIdx, false);
        RecurseInheritors(changedCollection, type, mod, oldValue, groupIdx);
    }

    /// <summary> Trigger changes in all inherited collections. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void RecurseInheritors(ModCollection directParent, ModSettingChange type, Mod? mod, int oldValue, int groupIdx)
    {
        foreach (var directInheritor in directParent.DirectParentOf)
        {
            switch (type)
            {
                case ModSettingChange.MultiInheritance:
                case ModSettingChange.MultiEnableState:
                    _communicator.ModSettingChanged.Invoke(directInheritor, type, null, oldValue, groupIdx, true);
                    break;
                default:
                    if (directInheritor.Settings[mod!.Index] == null)
                        _communicator.ModSettingChanged.Invoke(directInheritor, type, mod, oldValue, groupIdx, true);
                    break;
            }

            RecurseInheritors(directInheritor, type, mod, oldValue, groupIdx);
        }
    }
}
