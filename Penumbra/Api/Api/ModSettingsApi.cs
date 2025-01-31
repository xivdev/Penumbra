using OtterGui;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Interop.PathResolving;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Api.Api;

public class ModSettingsApi : IPenumbraApiModSettings, IApiService, IDisposable
{
    private readonly CollectionResolver  _collectionResolver;
    private readonly ModManager          _modManager;
    private readonly CollectionManager   _collectionManager;
    private readonly CollectionEditor    _collectionEditor;
    private readonly CommunicatorService _communicator;

    public ModSettingsApi(CollectionResolver collectionResolver,
        ModManager modManager,
        CollectionManager collectionManager,
        CollectionEditor collectionEditor,
        CommunicatorService communicator)
    {
        _collectionResolver = collectionResolver;
        _modManager         = modManager;
        _collectionManager  = collectionManager;
        _collectionEditor   = collectionEditor;
        _communicator       = communicator;
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ApiModSettings);
        _communicator.ModSettingChanged.Subscribe(OnModSettingChange, Communication.ModSettingChanged.Priority.Api);
        _communicator.ModOptionChanged.Subscribe(OnModOptionEdited, ModOptionChanged.Priority.Api);
        _communicator.ModFileChanged.Subscribe(OnModFileChanged, ModFileChanged.Priority.Api);
    }

    public void Dispose()
    {
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _communicator.ModSettingChanged.Unsubscribe(OnModSettingChange);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionEdited);
        _communicator.ModFileChanged.Unsubscribe(OnModFileChanged);
    }

    public event ModSettingChangedDelegate? ModSettingChanged;

    public AvailableModSettings? GetAvailableModSettings(string modDirectory, string modName)
    {
        if (!_modManager.TryGetMod(modDirectory, modName, out var mod))
            return null;

        var dict = new Dictionary<string, (string[], int)>(mod.Groups.Count);
        foreach (var g in mod.Groups)
            dict.Add(g.Name, (g.Options.Select(o => o.Name).ToArray(), (int)g.Type));
        return new AvailableModSettings(dict);
    }

    public (PenumbraApiEc, (bool, int, Dictionary<string, List<string>>, bool)?) GetCurrentModSettings(Guid collectionId, string modDirectory,
        string modName, bool ignoreInheritance)
    {
        var ret = GetCurrentModSettingsWithTemp(collectionId, modDirectory, modName, ignoreInheritance, true, 0);
        if (ret.Item2 is null)
            return (ret.Item1, null);

        return (ret.Item1, (ret.Item2.Value.Item1, ret.Item2.Value.Item2, ret.Item2.Value.Item3, ret.Item2.Value.Item4));
    }

    public (PenumbraApiEc, (bool, int, Dictionary<string, List<string>>, bool, bool)?) GetCurrentModSettingsWithTemp(Guid collectionId,
        string modDirectory, string modName, bool ignoreInheritance, bool ignoreTemporary, int key)
    {
        if (!_modManager.TryGetMod(modDirectory, modName, out var mod))
            return (PenumbraApiEc.ModMissing, null);

        if (!_collectionManager.Storage.ById(collectionId, out var collection))
            return (PenumbraApiEc.CollectionMissing, null);

        if (collection.Identity.Id == Guid.Empty)
            return (PenumbraApiEc.Success, null);

        if (GetCurrentSettings(collection, mod, ignoreInheritance, ignoreTemporary, key) is { } settings)
            return (PenumbraApiEc.Success, settings);

        return (PenumbraApiEc.Success, null);
    }

    public (PenumbraApiEc, Dictionary<string, (bool, int, Dictionary<string, List<string>>, bool, bool)>?) GetAllModSettings(Guid collectionId,
        bool ignoreInheritance, bool ignoreTemporary, int key)
    {
        if (!_collectionManager.Storage.ById(collectionId, out var collection))
            return (PenumbraApiEc.CollectionMissing, null);

        if (collection.Identity.Id == Guid.Empty)
            return (PenumbraApiEc.Success, []);

        var ret = new Dictionary<string, (bool, int, Dictionary<string, List<string>>, bool, bool)>(_modManager.Count);
        foreach (var mod in _modManager)
        {
            if (GetCurrentSettings(collection, mod, ignoreInheritance, ignoreTemporary, key) is { } settings)
                ret[mod.Identifier] = settings;
        }

        return (PenumbraApiEc.Success, ret);
    }

    public PenumbraApiEc TryInheritMod(Guid collectionId, string modDirectory, string modName, bool inherit)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", modDirectory, "ModName", modName, "Inherit",
            inherit.ToString());

        if (collectionId == Guid.Empty)
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        if (!_collectionManager.Storage.ById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        if (!_modManager.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.ModMissing, args);

        var ret = _collectionEditor.SetModInheritance(collection, mod, inherit)
            ? PenumbraApiEc.Success
            : PenumbraApiEc.NothingChanged;
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc TrySetMod(Guid collectionId, string modDirectory, string modName, bool enabled)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", modDirectory, "ModName", modName, "Enabled", enabled);
        if (!_collectionManager.Storage.ById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        if (!_modManager.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.ModMissing, args);

        var ret = _collectionEditor.SetModState(collection, mod, enabled)
            ? PenumbraApiEc.Success
            : PenumbraApiEc.NothingChanged;
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc TrySetModPriority(Guid collectionId, string modDirectory, string modName, int priority)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", modDirectory, "ModName", modName, "Priority", priority);

        if (!_collectionManager.Storage.ById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        if (!_modManager.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.ModMissing, args);

        var ret = _collectionEditor.SetModPriority(collection, mod, new ModPriority(priority))
            ? PenumbraApiEc.Success
            : PenumbraApiEc.NothingChanged;
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc TrySetModSetting(Guid collectionId, string modDirectory, string modName, string optionGroupName, string optionName)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", modDirectory, "ModName", modName, "OptionGroupName",
            optionGroupName, "OptionName", optionName);

        if (!_collectionManager.Storage.ById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        if (!_modManager.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.ModMissing, args);

        var groupIdx = mod.Groups.IndexOf(g => g.Name == optionGroupName);
        if (groupIdx < 0)
            return ApiHelpers.Return(PenumbraApiEc.OptionGroupMissing, args);

        var optionIdx = mod.Groups[groupIdx].Options.IndexOf(o => o.Name == optionName);
        if (optionIdx < 0)
            return ApiHelpers.Return(PenumbraApiEc.OptionMissing, args);

        var setting = mod.Groups[groupIdx].Behaviour switch
        {
            GroupDrawBehaviour.MultiSelection  => Setting.Multi(optionIdx),
            GroupDrawBehaviour.SingleSelection => Setting.Single(optionIdx),
            _                                  => Setting.Zero,
        };
        var ret = _collectionEditor.SetModSetting(collection, mod, groupIdx, setting)
            ? PenumbraApiEc.Success
            : PenumbraApiEc.NothingChanged;
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc TrySetModSettings(Guid collectionId, string modDirectory, string modName, string optionGroupName,
        IReadOnlyList<string> optionNames)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", modDirectory, "ModName", modName, "OptionGroupName",
            optionGroupName, "#optionNames", optionNames.Count);

        if (!_collectionManager.Storage.ById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        if (!_modManager.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.ModMissing, args);

        var settingSuccess = ConvertModSetting(mod, optionGroupName, optionNames, out var groupIdx, out var setting);
        if (settingSuccess is not PenumbraApiEc.Success)
            return ApiHelpers.Return(settingSuccess, args);

        var ret = _collectionEditor.SetModSetting(collection, mod, groupIdx, setting)
            ? PenumbraApiEc.Success
            : PenumbraApiEc.NothingChanged;
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc CopyModSettings(Guid? collectionId, string modDirectoryFrom, string modDirectoryTo)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId.HasValue ? collectionId.Value.ToString() : "NULL",
            "From", modDirectoryFrom, "To", modDirectoryTo);
        var sourceMod = _modManager.FirstOrDefault(m => string.Equals(m.ModPath.Name, modDirectoryFrom, StringComparison.OrdinalIgnoreCase));
        var targetMod = _modManager.FirstOrDefault(m => string.Equals(m.ModPath.Name, modDirectoryTo,   StringComparison.OrdinalIgnoreCase));
        if (collectionId == null)
            foreach (var collection in _collectionManager.Storage)
                _collectionEditor.CopyModSettings(collection, sourceMod, modDirectoryFrom, targetMod, modDirectoryTo);
        else if (_collectionManager.Storage.ById(collectionId.Value, out var collection))
            _collectionEditor.CopyModSettings(collection, sourceMod, modDirectoryFrom, targetMod, modDirectoryTo);
        else
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        return ApiHelpers.Return(PenumbraApiEc.Success, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private (bool, int, Dictionary<string, List<string>>, bool, bool)? GetCurrentSettings(ModCollection collection, Mod mod,
        bool ignoreInheritance, bool ignoreTemporary, int key)
    {
        var settings = collection.Settings.Settings[mod.Index];
        if (!ignoreTemporary && settings.TempSettings is { } tempSettings && (tempSettings.Lock <= 0 || tempSettings.Lock == key))
        {
            if (!tempSettings.ForceInherit)
                return (tempSettings.Enabled, tempSettings.Priority.Value, tempSettings.ConvertToShareable(mod).Settings,
                    false, true);
            if (!ignoreInheritance && collection.GetActualSettings(mod.Index).Settings is { } actualSettingsTemp)
                return (actualSettingsTemp.Enabled, actualSettingsTemp.Priority.Value,
                    actualSettingsTemp.ConvertToShareable(mod).Settings, true, true);
        }

        if (settings.Settings is { } ownSettings)
            return (ownSettings.Enabled, ownSettings.Priority.Value, ownSettings.ConvertToShareable(mod).Settings, false,
                false);
        if (!ignoreInheritance && collection.GetInheritedSettings(mod.Index).Settings is { } actualSettings)
            return (actualSettings.Enabled, actualSettings.Priority.Value,
                actualSettings.ConvertToShareable(mod).Settings, true, false);

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void TriggerSettingEdited(Mod mod)
    {
        var collection = _collectionResolver.PlayerCollection();
        var (settings, parent) = collection.GetActualSettings(mod.Index);
        if (settings is { Enabled: true })
            ModSettingChanged?.Invoke(ModSettingChange.Edited, collection.Identity.Id, mod.Identifier, parent != collection);
    }

    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? _1, DirectoryInfo? _2)
    {
        if (type == ModPathChangeType.Reloaded)
            TriggerSettingEdited(mod);
    }

    private void OnModSettingChange(ModCollection collection, ModSettingChange type, Mod? mod, Setting _1, int _2, bool inherited)
        => ModSettingChanged?.Invoke(type, collection.Identity.Id, mod?.ModPath.Name ?? string.Empty, inherited);

    private void OnModOptionEdited(ModOptionChangeType type, Mod mod, IModGroup? group, IModOption? option, IModDataContainer? container,
        int moveIndex)
    {
        switch (type)
        {
            case ModOptionChangeType.GroupDeleted:
            case ModOptionChangeType.GroupMoved:
            case ModOptionChangeType.GroupTypeChanged:
            case ModOptionChangeType.PriorityChanged:
            case ModOptionChangeType.OptionDeleted:
            case ModOptionChangeType.OptionMoved:
            case ModOptionChangeType.OptionFilesChanged:
            case ModOptionChangeType.OptionFilesAdded:
            case ModOptionChangeType.OptionSwapsChanged:
            case ModOptionChangeType.OptionMetaChanged:
                TriggerSettingEdited(mod);
                break;
        }
    }

    private void OnModFileChanged(Mod mod, FileRegistry file)
    {
        if (file.CurrentUsage == 0)
            return;

        TriggerSettingEdited(mod);
    }

    public static PenumbraApiEc ConvertModSetting(Mod mod, string groupName, IReadOnlyList<string> optionNames, out int groupIndex,
        out Setting setting)
    {
        groupIndex = mod.Groups.IndexOf(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        setting    = Setting.Zero;
        if (groupIndex < 0)
            return PenumbraApiEc.OptionGroupMissing;

        switch (mod.Groups[groupIndex])
        {
            case { Behaviour: GroupDrawBehaviour.SingleSelection } single:
            {
                var optionIdx = optionNames.Count == 0 ? -1 : single.Options.IndexOf(o => o.Name == optionNames[^1]);
                if (optionIdx < 0)
                    return PenumbraApiEc.OptionMissing;

                setting = Setting.Single(optionIdx);
                break;
            }
            case { Behaviour: GroupDrawBehaviour.MultiSelection } multi:
            {
                foreach (var name in optionNames)
                {
                    var optionIdx = multi.Options.IndexOf(o => o.Name == name);
                    if (optionIdx < 0)
                        return PenumbraApiEc.OptionMissing;

                    setting |= Setting.Multi(optionIdx);
                }

                break;
            }
        }

        return PenumbraApiEc.Success;
    }
}
