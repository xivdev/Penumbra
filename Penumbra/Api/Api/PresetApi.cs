using Dalamud.Plugin.Ipc;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods.Manager;

namespace Penumbra.Api.Api;

public class PresetApi(ApiHelpers helpers, CollectionManager collections, ModManager mods, PresetManagerAdapterFactory adapterFactory)
    : IPenumbraApiPresets, IApiService
{
    public IIdDataShareAdapter GetPresetManagerAdapter(CallerPlugin owner)
        => adapterFactory.Create(owner)!;

    public (PenumbraApiEc, SettingPresetData?) GetPreset(Guid collectionId, in ModIdentifier mod, PresetQueryMode mode,
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

    public (PenumbraApiEc, SettingPresetData?) GetPresetPlayer(int objectIndex, in ModIdentifier mod, PresetQueryMode mode,
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

    public PenumbraApiEc ApplyPreset(Guid collectionId, in ModIdentifier mod, in SettingPresetData preset,
        PresetApplyMode mode, int key, string source)
        => adapterFactory.ApplyPreset(collectionId, in mod, in preset, mode, key, source);

    public PenumbraApiEc ApplyPresetPlayer(int objectIndex, in ModIdentifier mod, in SettingPresetData preset,
        PresetApplyMode mode, int key, string source)
        => adapterFactory.ApplyPresetPlayer(objectIndex, in mod, in preset, mode, key, source);

    private (PenumbraApiEc, SettingPresetData?) GetPresetBase(Lazy<string> args, ModCollection collection, string modDirectory, string modName,
        PresetQueryMode mode, int key)
    {
        if (!mods.TryGetMod(modDirectory, modName, out var mod))
            return (ApiHelpers.Return(PenumbraApiEc.ModMissing, args), null);

        return (PenumbraApiEc.Success, collection.GetPreset(mod, mode, key));
    }
}
