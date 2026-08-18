using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods.Manager;

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

        return (PenumbraApiEc.Success, collection.GetPreset(mod, mode, key));
    }
}
