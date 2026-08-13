using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Api.Preset;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;

namespace Penumbra.Api.Api;

public class PresetApi(ApiHelpers helpers, CollectionManager collections, ModManager mods, MainConfig config) : IPenumbraApiPresets, IApiService
{
    public (PenumbraApiEc, SettingPresetData?) GetPreset(Guid collectionId, in (string Identifier, string Name) mod, PresetQueryMode mode,
        int key)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", mod.Identifier, "ModName", mod.Name, "Mode", mode, "Key", key);
        var needsCollection = !mode.CheckAny(PresetQueryMode.GetDefault | PresetQueryMode.IgnoreSettings);
        var collection = ModCollection.Empty;
        if (needsCollection)
            if (!collections.Storage.ById(collectionId, out collection))
                return (ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args), null);

        return GetPresetBase(args, collection, mod.Identifier, mod.Name, mode, key);
    }

    public (PenumbraApiEc, SettingPresetData?) GetPresetPlayer(int objectIndex, in (string Identifier, string Name) mod, PresetQueryMode mode,
        int key)
    {
        var args = ApiHelpers.Args("ObjectIndex", objectIndex, "ModDirectory", mod.Identifier, "ModName", mod.Name, "Mode", mode, "Key", key);
        var needsCollection = mode.HasFlag(PresetQueryMode.GetDefault) || mode.HasFlag(PresetQueryMode.IgnoreSettings);
        var collection = ModCollection.Empty;
        if (needsCollection)
            if (!helpers.AssociatedCollection(objectIndex, out collection))
                return (ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args), null);

        return GetPresetBase(args, collection, mod.Identifier, mod.Name, mode, key);
    }

    public PenumbraApiEc ApplyPreset(Guid collectionId, in (string Identifier, string Name) mod, in SettingPresetData preset,
        PresetApplyMode mode, int key, string source)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", mod.Identifier, "ModName", mod.Name, "Mode", mode, "Key", key,
            "Source", source);
        if (!collections.Storage.ById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        return ApplyPresetBase(args, collection, mod.Identifier, mod.Name, preset, mode, key, source);
    }

    public PenumbraApiEc ApplyPresetPlayer(int objectIndex, in (string Identifier, string Name) mod, in SettingPresetData preset,
        PresetApplyMode mode, int key, string source)
    {
        var args = ApiHelpers.Args("ObjectIndex", objectIndex, "ModDirectory", mod.Identifier, "ModName", mod.Name, "Mode", mode, "Key", key,
            "Source", source);
        if (!helpers.AssociatedCollection(objectIndex, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        return ApplyPresetBase(args, collection, mod.Identifier, mod.Name, preset, mode, key, source);
    }

    private PenumbraApiEc ApplyPresetBase(Lazy<string> args, ModCollection collection, string modDirectory, string modName,
        in SettingPresetData preset, PresetApplyMode mode, int key, string source)
    {
        if (!mods.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.ModMissing, args);

        var temporary = mode is PresetApplyMode.Auto
            ? config.DefaultTemporaryMode || collection.Settings.Settings[mod.Index].TempSettings is not null
            : mode is not PresetApplyMode.Permanent;

        if (collection.Identity.Index is 0)
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        if (collection.Identity.Index < 0 && temporary)
            return ApiHelpers.Return(PenumbraApiEc.TemporarySettingImpossible, args);

        if (temporary && !collections.Editor.CanSetTemporarySettings(collection, mod, key))
            return ApiHelpers.Return(PenumbraApiEc.TemporarySettingDisallowed, args);

        collections.Editor.ApplyPreset(collection, mod, preset, temporary, source, key);
        return ApiHelpers.Return(PenumbraApiEc.Success, args);
    }

    private (PenumbraApiEc, SettingPresetData?) GetPresetBase(Lazy<string> args, ModCollection collection, string modDirectory, string modName,
        PresetQueryMode mode, int key)
    {
        if (!mods.TryGetMod(modDirectory, modName, out var mod))
            return (ApiHelpers.Return(PenumbraApiEc.ModMissing, args), null);

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
                if (mode.HasFlag(PresetQueryMode.IgnoreInheritance))
                    relevantSettings = collection.GetOwnSettings(mod.Index);
                else
                    relevantSettings = collection.GetInheritedSettings(mod.Index).Settings;
            }
            else
            {
                if (mode.HasFlag(PresetQueryMode.IgnoreInheritance))
                    relevantSettings = collection.GetTempSettings(mod.Index) is { } s
                        ? s.ForceInherit ? null : s
                        : collection.GetOwnSettings(mod.Index);
                else
                    relevantSettings = collection.GetActualSettings(mod.Index).Settings;
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

        return (PenumbraApiEc.Success, preset);
    }
}
