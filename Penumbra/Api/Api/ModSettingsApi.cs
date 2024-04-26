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

    public Dictionary<string, (string[], int)>? GetAvailableModSettingsBase(string modDirectory, string modName)
        => _modManager.TryGetMod(modDirectory, modName, out var mod)
            ? mod.Groups.ToDictionary(g => g.Name, g => (g.Options.Select(o => o.Name).ToArray(), (int)g.Type))
            : null;

    public (PenumbraApiEc, (bool, int, Dictionary<string, List<string>>, bool)?) GetCurrentModSettings(Guid collectionId, string modDirectory,
        string modName, bool ignoreInheritance)
    {
        if (!_modManager.TryGetMod(modDirectory, modName, out var mod))
            return (PenumbraApiEc.ModMissing, null);

        if (!_collectionManager.Storage.ById(collectionId, out var collection))
            return (PenumbraApiEc.CollectionMissing, null);

        var settings = collection.Id == Guid.Empty 
            ? null 
            : ignoreInheritance 
                ? collection.Settings[mod.Index] 
                : collection[mod.Index].Settings;
        if (settings == null)
            return (PenumbraApiEc.Success, null);

        var (enabled, priority, dict) = settings.ConvertToShareable(mod);
        return (PenumbraApiEc.Success,
            (enabled, priority.Value, dict, collection.Settings[mod.Index] == null));
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

        var setting = mod.Groups[groupIdx] switch
        {
            MultiModGroup  => Setting.Multi(optionIdx),
            SingleModGroup => Setting.Single(optionIdx),
            _              => Setting.Zero,
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

        var groupIdx = mod.Groups.IndexOf(g => g.Name == optionGroupName);
        if (groupIdx < 0)
            return ApiHelpers.Return(PenumbraApiEc.OptionGroupMissing, args);

        var setting = Setting.Zero;
        switch (mod.Groups[groupIdx])
        {
            case SingleModGroup single:
            {
                var optionIdx = optionNames.Count == 0 ? -1 : single.OptionData.IndexOf(o => o.Name == optionNames[^1]);
                if (optionIdx < 0)
                    return ApiHelpers.Return(PenumbraApiEc.OptionMissing, args);

                setting = Setting.Single(optionIdx);
                break;
            }
            case MultiModGroup multi:
            {
                foreach (var name in optionNames)
                {
                    var optionIdx = multi.OptionData.IndexOf(o => o.Mod.Name == name);
                    if (optionIdx < 0)
                        return ApiHelpers.Return(PenumbraApiEc.OptionMissing, args);

                    setting |= Setting.Multi(optionIdx);
                }

                break;
            }
        }

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
    private void TriggerSettingEdited(Mod mod)
    {
        var collection = _collectionResolver.PlayerCollection();
        var (settings, parent) = collection[mod.Index];
        if (settings is { Enabled: true })
            ModSettingChanged?.Invoke(ModSettingChange.Edited, collection.Id, mod.Identifier, parent != collection);
    }

    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? _1, DirectoryInfo? _2)
    {
        if (type == ModPathChangeType.Reloaded)
            TriggerSettingEdited(mod);
    }

    private void OnModSettingChange(ModCollection collection, ModSettingChange type, Mod? mod, Setting _1, int _2, bool inherited)
        => ModSettingChanged?.Invoke(type, collection.Id, mod?.ModPath.Name ?? string.Empty, inherited);

    private void OnModOptionEdited(ModOptionChangeType type, Mod mod, IModGroup? group, IModOption? option, IModDataContainer? container, int moveIndex)
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
}
