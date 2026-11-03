using Dalamud.Plugin.Ipc;
using Luna;
using Luna.Generators;
using Penumbra.Api.Api;
using Penumbra.Api.Enums;
using Penumbra.Api.Preset;
using Penumbra.Api.Wrappers;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;

namespace Penumbra.Api;

public sealed class PresetManagerAdapterFactory(
    IpcObjectManager ipcManager,
    ModManager mods,
    SettingPresetManager presets,
    MainConfig config,
    CollectionManager collections,
    ApiHelpers apiHelpers)
    : IAdapterFactory, IApiService
{
    public readonly ModManager           Mods        = mods;
    public readonly SettingPresetManager Presets     = presets;
    public readonly MainConfig           Config      = config;
    public readonly CollectionManager    Collections = collections;

    public IpcObjectManager IpcManager { get; } = ipcManager;

    public IpcObjectManager.IBasicAdapter CreateAdapter(CallerPlugin owner, object? data)
        => new PresetManagerAdapter(this, owner);

    internal PenumbraApiEc ApplyPreset(Guid collectionId, in ModIdentifier mod, in SettingPresetData preset,
        PresetApplyMode mode, int key, string source)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", mod.Identifier, "ModName", mod.Name, "Mode", mode, "Key", key,
            "Source", source);
        if (!Collections.Storage.ById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        return ApplyPresetBase(args, collection, mod.Identifier, mod.Name, preset, mode, key, source);
    }

    internal PenumbraApiEc ApplyPresetPlayer(int objectIndex, in ModIdentifier mod, in SettingPresetData preset,
        PresetApplyMode mode, int key, string source)
    {
        var args = ApiHelpers.Args("ObjectIndex", objectIndex, "ModDirectory", mod.Identifier, "ModName", mod.Name, "Mode", mode, "Key", key,
            "Source", source);
        if (!apiHelpers.AssociatedCollection(objectIndex, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        return ApplyPresetBase(args, collection, mod.Identifier, mod.Name, preset, mode, key, source);
    }

    internal PenumbraApiEc ApplyPresetBase(Lazy<string> args, ModCollection collection, string modDirectory, string modName,
        in SettingPresetData preset, PresetApplyMode mode, int key, string source)
    {
        if (!Mods.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.ModMissing, args);

        var temporary = mode is PresetApplyMode.Auto
            ? Config.DefaultTemporaryMode || collection.Settings.Settings[mod.Index].TempSettings is not null
            : mode is not PresetApplyMode.Permanent;

        if (collection.Identity.Index is 0)
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        if (collection.Identity.Index < 0 && temporary)
            return ApiHelpers.Return(PenumbraApiEc.TemporarySettingImpossible, args);

        if (temporary && !Collections.Editor.CanSetTemporarySettings(collection, mod, key))
            return ApiHelpers.Return(PenumbraApiEc.TemporarySettingDisallowed, args);

        Collections.Editor.ApplyPreset(collection, mod, preset, temporary, source, key);
        return ApiHelpers.Return(PenumbraApiEc.Success, args);
    }
}

public sealed partial class PresetManagerAdapter(PresetManagerAdapterFactory parent, CallerPlugin owner)
    : IpcObjectManager.BasicAdapter(parent, owner, nameof(PresetManagerAdapter)), IAdapterFactory, IpcObjectManager.IBasicAdapter
{
    internal static readonly Version LocalVersion = new(1, 0);

    public override Version Version
        => LocalVersion;

    public override bool IsDisposed
        => base.Parent is null;

    public event Action? Disposed;

    protected override void InvokeDisposed()
        => Disposed?.Invoke();

    public IpcObjectManager IpcManager
        => Parent.IpcManager;

    public new PresetManagerAdapterFactory Parent
        => (PresetManagerAdapterFactory)base.Parent!;

    [AdapterMethod(PresetManagerWrapper.Method.GetPresetIds)]
    public IEnumerable<Guid> GetPresetIds(ModIdentifier mod)
        => Parent.Mods.TryGetMod(mod.Identifier, mod.Name, out var m) ? m.Presets.Select(p => p.Identifier) : [];

    [AdapterMethod(PresetManagerWrapper.Method.GetAllPresetIds)]
    public IEnumerable<(ModIdentifier AssociatedMod, Guid PresetId)> GetAllPresetIds()
        => Parent.Presets.Values.Select(p
            => (p.Item1 is null ? ModIdentifier.Empty : new ModIdentifier(p.Item1.Identifier, p.Item1.Name), p.Item2.Identifier));

    [AdapterMethod(PresetManagerWrapper.Method.GetPreset)]
    public IIdDataShareAdapter? GetPreset(Guid id)
        => Parent.Presets.TryGetValue(id, out var preset) ? CreateAdapter(Owner, preset) : null;

    [AdapterMethod(PresetManagerWrapper.Method.GetPresetByName)]
    public IIdDataShareAdapter? GetPresetByName(string name, ModIdentifier? mod = null)
    {
        if (mod is { } modId)
        {
            if (modId.IsEmpty)
            {
                foreach (var preset in Parent.Presets.GenericPresets)
                {
                    if (preset.Name == name)
                        return CreateAdapter(Owner, ((Mod?)null, preset));
                }
            }
            else if (Parent.Mods.TryGetMod(modId.Identifier, modId.Name, out var m))
            {
                foreach (var preset in m.Presets)
                {
                    if (preset.Name == name)
                        return CreateAdapter(Owner, (m, preset));
                }
            }
        }
        else
        {
            foreach (var preset in Parent.Presets.Values)
            {
                if (preset.Item2.Name == name)
                    return CreateAdapter(Owner, preset);
            }
        }

        return null;
    }

    [AdapterMethod(PresetManagerWrapper.Method.GetPresets)]
    public IEnumerable<IIdDataShareAdapter> GetPresets(ModIdentifier mod)
        => (mod.IsEmpty ? Parent.Presets.GenericPresets : Parent.Mods.TryGetMod(mod.Identifier, mod.Name, out var m) ? m.Presets : []).Select(p
            => CreatePreset(p, null));

    [AdapterMethod(PresetManagerWrapper.Method.GetAllPresets)]
    public IEnumerable<(ModIdentifier AssociatedMod, IIdDataShareAdapter Preset)> GetAllPresets()
        => Parent.Presets.Select(p => (p.Value.Item1 is { } m ? new ModIdentifier(m.Identifier, m.Name) : ModIdentifier.Empty,
            CreatePreset(p.Value.Item2, p.Value.Item1)));

    [AdapterMethod(PresetManagerWrapper.Method.Count)]
    public int Count(ModIdentifier mod)
        => mod.IsEmpty ? Parent.Presets.GenericPresets.Count : Parent.Mods.TryGetMod(mod.Identifier, mod.Name, out var m) ? m.Presets.Count : 0;

    [AdapterMethod(PresetManagerWrapper.Method.TotalCount)]
    public int TotalCount
        => Parent.Presets.Count;

    [AdapterMethod(PresetManagerWrapper.Method.AddPreset)]
    public IIdDataShareAdapter? AddPreset(string name, SettingPresetData preset, ModIdentifier modIdentifier)
    {
        SettingPreset? newPreset;
        Mod?           mod;
        if (modIdentifier.IsEmpty)
        {
            Parent.Presets.AddGenericPreset(name, preset);
            mod       = null;
            newPreset = Parent.Presets.GenericPresets[^1];
        }
        else if (Parent.Mods.TryGetMod(modIdentifier.Identifier, modIdentifier.Name, out mod))
        {
            Parent.Presets.AddPreset(mod, preset, name);
            newPreset = mod.Presets[^1];
        }
        else
        {
            mod       = null;
            newPreset = null;
        }

        return CreatePreset(newPreset, mod);
    }

    [AdapterMethod(PresetManagerWrapper.Method.DeletePreset)]
    public bool DeletePreset(Guid id)
    {
        if (!Parent.Presets.TryGetValue(id, out var data))
            return false;

        if (data.Item1 is null)
            Parent.Presets.DeleteGeneric(data.Item2);
        else
            Parent.Presets.DeletePreset(data.Item1, data.Item2);
        return true;
    }

    [AdapterMethod(PresetManagerWrapper.Method.GetPresetData)]
    public SettingPresetData? GetPresetData(Guid id)
        => Parent.Presets.TryGetValue(id, out var data) ? data.Item2.Data : null;

    [return: NotNullIfNotNull(nameof(preset))]
    private IIdDataShareAdapter? CreatePreset(SettingPreset? preset, Mod? mod, [CallerMemberName] string? callerName = null)
        => this.Create(Owner, (mod, preset), callerName);

    public IpcObjectManager.IBasicAdapter? CreateAdapter(CallerPlugin owner, object? data)
        => data is ValueTuple<Mod?, SettingPreset> { Item2: not null } d ? new PresetAdapter(this, d.Item1, d.Item2) : null;
}
