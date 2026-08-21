using Dalamud.Plugin.Ipc;
using Luna;
using Luna.Generators;
using Penumbra.Api.Enums;
using Penumbra.Api.Wrappers;
using Penumbra.Collections;
using Penumbra.Mods.Settings;

namespace Penumbra.Api;

public sealed partial class CollectionAdapter(CollectionManagerAdapter parent, ModCollection collection)
    : IpcObjectManager.BasicAdapter(parent.Parent, parent.Owner, nameof(CollectionAdapter)), IIdDataShareAdapter
{
    private ModCollection _collection = collection;

    private new CollectionManagerAdapterFactory Parent
        => (CollectionManagerAdapterFactory)base.Parent!;

    [AdapterMethod(CollectionWrapper.Method.GetIndex)]
    private int Index
        => _collection.Identity.Index;

    [AdapterMethod(CollectionWrapper.Method.GetId)]
    private Guid Id
        => _collection.Identity.Id;

    [AdapterMethod(CollectionWrapper.Method.GetName)]
    private string Name
        => _collection.Identity.Name;

    [AdapterMethod(CollectionWrapper.Method.GetAnonymousName)]
    private string AnonymousName
        => _collection.Identity.AnonymizedName;

    [AdapterMethod(CollectionWrapper.Method.GetChangedItems)]
    private Dictionary<string, object?> GetChangedItems()
        => _collection.Cache?.ChangedItems.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Item2.ToInternalObject()) ?? [];

    [AdapterMethod(CollectionWrapper.Method.HasCache)]
    private bool HasCache
        => _collection.HasCache;

    [AdapterMethod(CollectionWrapper.Method.GetActualSettingsByIndex)]
    private SettingPresetData? GetActualSettings(int modIndex)
        => SettingPresetData.FromMod(Parent.Mods[modIndex], _collection.GetActualSettings(modIndex).Settings);

    [AdapterMethod(CollectionWrapper.Method.GetActualSettingsByName)]
    private SettingPresetData? GetActualSettings(ModIdentifier modIdentifier)
    {
        if (!Parent.Mods.TryGetMod(modIdentifier.Identifier, modIdentifier.Name, out var mod))
            return null;

        return SettingPresetData.FromMod(mod, _collection.GetActualSettings(mod.Index).Settings);
    }

    [AdapterMethod(CollectionWrapper.Method.GetTemporarySettingsByIndex)]
    private SettingPresetData? GetTemporarySettings(int modIndex)
        => SettingPresetData.FromMod(Parent.Mods[modIndex], _collection.GetTempSettings(modIndex));

    [AdapterMethod(CollectionWrapper.Method.GetTemporarySettingsByName)]
    private SettingPresetData? GetTemporarySettings(ModIdentifier modIdentifier)
    {
        if (!Parent.Mods.TryGetMod(modIdentifier.Identifier, modIdentifier.Name, out var mod))
            return null;

        return SettingPresetData.FromMod(mod, _collection.GetOwnSettings(mod.Index));
    }

    [AdapterMethod(CollectionWrapper.Method.GetOwnSettingsByIndex)]
    private SettingPresetData? GetOwnSettings(int modIndex)
        => SettingPresetData.FromMod(Parent.Mods[modIndex], _collection.GetTempSettings(modIndex));

    [AdapterMethod(CollectionWrapper.Method.GetOwnSettingsByName)]
    private SettingPresetData? GetOwnSettings(ModIdentifier modIdentifier)
    {
        if (!Parent.Mods.TryGetMod(modIdentifier.Identifier, modIdentifier.Name, out var mod))
            return null;

        return SettingPresetData.FromMod(mod, _collection.GetOwnSettings(mod.Index));
    }

    [AdapterMethod(CollectionWrapper.Method.ModState)]
    private (int Actual, int Own, bool Temporary) ModState(int modIndex)
    {
        var settings = _collection.GetActualSettings(modIndex);
        var actualState = settings.Settings is null ? Preset.ModState.Ignored :
            settings.Settings.Enabled               ? Preset.ModState.Enabled : Preset.ModState.Disabled;
        var ownState = settings.Collection == _collection ? actualState : Preset.ModState.Ignored;
        return ((int)actualState, (int)ownState, _collection.GetTempSettings(modIndex) is not null);
    }

    [AdapterMethod(CollectionWrapper.Method.ModPriority)]
    private int ModPriority(int modIndex)
        => (_collection.GetActualSettings(modIndex).Settings?.Priority ?? Mods.Settings.ModPriority.Default).Value;

    [AdapterMethod(CollectionWrapper.Method.EnumerateGroups)]
    private IEnumerable<(ModObjectIdentifier Identifier, IEnumerable<(ModObjectIdentifier, bool)>)> EnumerateGroups(int modIndex)
    {
        var mod            = Parent.Mods[modIndex];
        var actualSettings = _collection.GetActualSettings(modIndex).Settings ?? ModSettings.Empty;
        foreach (var group in mod.Groups)
        {
            var id         = ModObjectIdentifier.From(group);
            var enumerable = group.Options.Select(o => (ModObjectIdentifier.From(o), o.IsEnabled(actualSettings)));
            yield return (id, enumerable);
        }
    }

    [AdapterMethod(CollectionWrapper.Method.CanUnlock)]
    private bool CanUnlock(int modIndex, int key)
    {
        if (_collection.GetTempSettings(modIndex) is not { } settings)
            return true;

        return settings.Lock <= 0 || settings.Lock == key;
    }

    [AdapterMethod(CollectionWrapper.Method.GetTemporaryOwner)]
    private string? GetTemporaryOwner(int modIndex)
        => _collection.GetTempSettings(modIndex)?.Source;

    [AdapterMethod(CollectionWrapper.Method.GetPreset)]
    private SettingPresetData? GetPreset(int modIndex, uint mode, int key)
        => _collection.GetPreset(Parent.Mods[modIndex], (PresetQueryMode)mode, key);

    [AdapterMethod(CollectionWrapper.Method.ApplyPreset)]
    private void ApplyPreset(int modIndex, SettingPresetData preset, int mode, string source, int key)
    {
        if (Parent.Mods.Count <= modIndex || modIndex < 0)
            return;

        Parent.Log.Debug($"[{Owner}] Applying preset to {Parent.Mods[modIndex].Identifier}...");
        Parent.Collections.Editor.ApplyPreset(_collection, Parent.Mods[modIndex], preset, (PresetApplyMode)mode, source, key);
    }

    protected override void DisposeInternal()
        => _collection = null!;
}
