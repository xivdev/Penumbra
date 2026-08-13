global using GroupSettingData =
    (System.Collections.Generic.Dictionary<(System.Guid Identifier, string? Name), byte> Options, bool DisableAllUnknown);
global using ModObjectIdentifier = (System.Guid Identifier, string? Name);
global using SettingPresetData = (System.Collections.Generic.Dictionary<(System.Guid Identifier, string? Name),
        (System.Collections.Generic.Dictionary<(System.Guid Identifier, string? Name), byte> Options, bool DisableAllUnknown)> Settings, int
    _priority, short Version, bool _hasPriority, byte _state);
global using SettingsDictionary =
    System.Collections.Generic.Dictionary<(System.Guid Identifier, string? Name), (
        System.Collections.Generic.Dictionary<(System.Guid Identifier, string? Name), byte> Options, bool DisableAllUnknown)>;
using Luna;
using Penumbra.Api.Preset;
using Penumbra.Communication;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Services;

namespace Penumbra.Mods.Settings;

public sealed class SettingPresetManager : IDisposable, IService
{
    private readonly LocalModDatabase    _database;
    private readonly CommunicatorService _communicator;
    public readonly  List<SettingPreset> GenericPresets = [];

    public SettingPresetManager(ModManager mods, CommunicatorService communicator, LocalModDatabase database)
    {
        _communicator = communicator;
        _database     = database;
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.SettingPresetManager);
        GenericPresets = _database.GetGenericPresets();
    }

    public event Action<SettingPreset>? Deleted;

    public void AddPreset(Mod mod, ModSettings? settings, string name)
    {
        var preset = new SettingPreset(Guid.NewGuid(), SettingPresetData.FromMod(mod, settings)) { Name = name };
        mod.Presets.Add(preset);
        _database.UpsertFullPreset(mod.Identifier, preset);
    }

    public void AddGenericPreset(SettingPreset preset)
    {
        GenericPresets.Add(preset);
        _database.UpsertFullPreset(string.Empty, preset);
    }

    public void DeletePreset(Mod mod, SettingPreset preset)
    {
        if (!mod.Presets.Remove(preset))
            return;

        Deleted?.Invoke(preset);
        _database.DeletePreset(preset.Identifier);
    }

    public void DeleteGeneric(SettingPreset preset)
    {
        if (!GenericPresets.Remove(preset))
            return;

        Deleted?.Invoke(preset);
        _database.DeletePreset(preset.Identifier);
    }

    public void Update(string modIdentifier, SettingPreset preset, in SettingPresetData newData)
    {
        if (preset.Update(newData, true))
            _database.UpsertFullPreset(modIdentifier, preset);
    }

    public void Update(string modIdentifier, SettingPreset preset, Mod mod, ModSettings? settings)
    {
        if (!preset.Data.Update(mod, settings, true))
            return;

        preset.LastEdit = DateTime.UtcNow;
        _database.UpsertFullPreset(modIdentifier, preset);
    }

    public void ChangeLastEdit(string modIdentifier, SettingPreset preset, DateTimeOffset lastEdit)
    {
        preset.LastEdit = lastEdit;
        _database.UpsertPresetProperty(modIdentifier, preset, p => new LocalModDatabase.PresetData
        {
            LastEdit = lastEdit.ToUnixTimeMilliseconds(),
        });
    }

    public void ChangeLastApply(string modIdentifier, SettingPreset preset, DateTimeOffset lastApply)
    {
        preset.LastApplication = lastApply;
        _database.UpsertPresetProperty(modIdentifier, preset, p => new LocalModDatabase.PresetData
        {
            LastEdit = preset.LastApplication.ToUnixTimeMilliseconds(),
        });
    }

    public void ChangeLastEdit(string modIdentifier, SettingPreset preset, string newName)
    {
        if (preset.UpdateName(newName))
            _database.UpsertPresetProperty(modIdentifier, preset, p => new LocalModDatabase.PresetData
            {
                LastEdit = preset.LastEdit.ToUnixTimeMilliseconds(),
            });
    }

    public void ChangeName(string modIdentifier, SettingPreset preset, string newName)
    {
        if (preset.UpdateName(newName))
            _database.UpsertPresetProperty(modIdentifier, preset, p => new LocalModDatabase.PresetData
            {
                Name     = preset.Name,
                LastEdit = preset.LastEdit.ToUnixTimeMilliseconds(),
            });
    }

    public void ChangePriority(string modIdentifier, SettingPreset preset, int? priority)
    {
        if (preset.SetPriority(priority, true))
            _database.UpsertPresetProperty(modIdentifier, preset, p => new LocalModDatabase.PresetData
            {
                Priority = priority,
                LastEdit = preset.LastEdit.ToUnixTimeMilliseconds(),
            });
    }

    public void ChangeState(string modIdentifier, SettingPreset preset, ModState state)
    {
        if (preset.SetState(state, true))
            _database.UpsertPresetProperty(modIdentifier, preset, p => new LocalModDatabase.PresetData
            {
                State    = state,
                LastEdit = preset.LastEdit.ToUnixTimeMilliseconds(),
            });
    }

    public void ChangeDisableUnknownOptions(string modIdentifier, SettingPreset preset, ModObjectIdentifier group, bool disableUnknownOptions)
    {
        if (preset.SetDisableUnknownOptions(group, disableUnknownOptions))
            _database.UpsertFullPreset(modIdentifier, preset);
    }

    public void AddGroupReference(string modIdentifier, SettingPreset preset, ModObjectIdentifier group)
    {
        if (preset.AddGroup(group))
            _database.UpsertFullPreset(modIdentifier, preset);
    }

    public void DeleteGroupReference(string modIdentifier, SettingPreset preset, ModObjectIdentifier group)
    {
        if (!preset.Data.Settings.Remove(group))
            return;

        preset.LastEdit = DateTimeOffset.UtcNow;
        _database.UpsertFullPreset(modIdentifier, preset);
    }

    public void ChangeGroupReference(string modIdentifier, SettingPreset preset, ModObjectIdentifier group, ModObjectIdentifier newGroup)
    {
        if (!preset.Data.Settings.ReplaceGroupIdentifier(group, newGroup))
            return;

        preset.LastEdit = DateTimeOffset.UtcNow;
        _database.UpsertFullPreset(modIdentifier, preset);
    }

    public void DeleteOptionReference(string modIdentifier, SettingPreset preset, ModObjectIdentifier group, ModObjectIdentifier option)
    {
        if (!preset.Data.Settings.TryGetValue(group, out var groupData))
            return;

        if (!groupData.Options.Remove(option))
            return;

        preset.LastEdit = DateTimeOffset.UtcNow;
        _database.UpsertFullPreset(modIdentifier, preset);
    }

    public void ChangeOptionReference(string modIdentifier, SettingPreset preset, ModObjectIdentifier group, ModObjectIdentifier option,
        ModObjectIdentifier newOption)
    {
        if (!preset.Data.Settings.ReplaceOptionIdentifiers(option, newOption, group))
            return;

        preset.LastEdit = DateTimeOffset.UtcNow;
        _database.UpsertFullPreset(modIdentifier, preset);
    }

    public void ChangeOption(string modIdentifier, SettingPreset preset, ModObjectIdentifier group, ModObjectIdentifier option,
        OptionState state)
    {
        if (preset.SetOption(group, option, state, true))
            _database.UpsertFullPreset(modIdentifier, preset);
    }

    public void SetMod(SettingPreset preset, Mod? oldMod, Mod? newMod)
    {
        if (oldMod == newMod)
            return;

        var oldPresetList = oldMod?.Presets ?? GenericPresets;
        oldPresetList.Remove(preset);
        var newIdentifier = newMod?.Identifier ?? string.Empty;
        _database.UpsertPresetProperty(newIdentifier, preset, p => new LocalModDatabase.PresetData { Mod = newIdentifier });
        var newPresetList = newMod?.Presets ?? GenericPresets;
        if (newPresetList.Contains(preset))
            return;

        newPresetList.Add(preset);
    }

    public void MakeGeneric(SettingPreset preset, Mod? oldMod)
    {
        oldMod?.Presets.Remove(preset);
        if (!GenericPresets.Contains(preset))
            GenericPresets.Add(preset);
        if (preset.MakeGeneric() || oldMod is not null)
            _database.UpsertFullPreset(string.Empty, preset);
    }

    private void OnModOptionChange(in ModOptionChanged.Arguments arguments)
    {
        // We only react to changes in identifiers. We do not care about
        //   - additions (those are ignored until a manual update)
        //   - deletions (those are just kept and ignored on application until a manual update)
        switch (arguments.Type)
        {
            case ModOptionChangeType.GroupIdentifierChanged:
            {
                // Replace all occurrences in this mod's associated presets.
                var oldId = new ModObjectIdentifier(arguments.Id, null);
                var newId = ModObjectIdentifier.From(arguments.Group!);
                foreach (var preset in arguments.Mod.Presets)
                {
                    if (preset.ReplaceGroupIdentifier(oldId, newId))
                        _database.UpsertFullPreset(arguments.Mod.Identifier, preset);
                }

                break;
            }
            case ModOptionChangeType.OptionIdentifierChanged:
            {
                // Replace all occurrences in this mod's associated presets in the correct group.
                var groupId = ModObjectIdentifier.From(arguments.Group!);
                var oldId   = new ModObjectIdentifier(arguments.Id, null);
                var newId   = ModObjectIdentifier.From(arguments.Option!);
                foreach (var preset in arguments.Mod.Presets)
                {
                    if (preset.ReplaceOptionIdentifiers(oldId, newId, groupId))
                        _database.UpsertFullPreset(arguments.Mod.Identifier, preset);
                }

                break;
            }
            case ModOptionChangeType.GroupRenamed:
            {
                // Replace all occurrences of the associated ID, and of ID-less identifiers of the same name.
                var oldId1 = new ModObjectIdentifier(arguments.Id, null);
                var oldId2 = new ModObjectIdentifier(Guid.Empty,   arguments.OldName!);
                var newId1 = new ModObjectIdentifier(arguments.Id, arguments.Group!.Name);
                var newId2 = new ModObjectIdentifier(Guid.Empty,   arguments.Group!.Name);
                foreach (var preset in arguments.Mod.Presets)
                {
                    if (preset.ReplaceGroupIdentifier(oldId1, newId1) | preset.ReplaceGroupIdentifier(oldId2, newId2))
                        _database.UpsertFullPreset(arguments.Mod.Identifier, preset);
                }

                break;
            }
            case ModOptionChangeType.OptionRenamed:
            {
                // Replace all occurrences of the associated ID, and of ID-less identifiers of the same name in the correct group.
                var groupId = ModObjectIdentifier.From(arguments.Group!);
                var oldId1  = new ModObjectIdentifier(arguments.Id, null);
                var oldId2  = new ModObjectIdentifier(Guid.Empty,   arguments.OldName!);
                var newId1  = new ModObjectIdentifier(arguments.Id, arguments.Option!.Name);
                var newId2  = new ModObjectIdentifier(Guid.Empty,   arguments.Option!.Name);
                foreach (var preset in arguments.Mod.Presets)
                {
                    if (preset.ReplaceOptionIdentifiers(oldId1, newId1, groupId) | preset.ReplaceOptionIdentifiers(oldId2, newId2, groupId))
                        _database.UpsertFullPreset(arguments.Mod.Identifier, preset);
                }

                break;
            }
        }
    }

    public void Dispose()
        => _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
}
