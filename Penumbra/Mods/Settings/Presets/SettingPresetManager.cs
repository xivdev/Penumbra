using System.Text.Json;
using Dalamud.Interface.ImGuiNotification;
using Luna;
using Penumbra.Communication;
using Penumbra.Files;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Services;

namespace Penumbra.Mods.Settings;

public sealed class SettingPresetManager : JsonPopulatable<SettingPresetManager>, ISavable, IDisposable, IService
{
    public const int PresetVersion = 1;

    private readonly CommunicatorService                   _communicator;
    private readonly SaveService                           _saveService;
    public readonly  ListDictionary<string, SettingPreset> Presets = [];

    public SettingPresetManager(CommunicatorService communicator, SaveService saveService)
    {
        _communicator = communicator;
        _saveService  = saveService;
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.SettingPresetManager);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.SettingPresetManager);
    }

    private void OnModPathChange(in ModPathChanged.Arguments arguments)
    {
        if (arguments.Type is not ModPathChangeType.Moved)
            return;

        if (!Presets.TryGetValue(arguments.OldDirectory!.Name, out var list))
            return;

        if (Presets.TryGetValue(arguments.NewDirectory!.Name, out var newList))
            foreach (var value in list.Where(v => newList.All(v2 => v2.Identifier != v.Identifier)))
                Presets.TryAdd(arguments.NewDirectory!.Name, value);
        else
            foreach (var value in list)
                Presets.TryAdd(arguments.NewDirectory!.Name, value);
        _saveService.QueueSave(this);
    }

    private void OnModOptionChange(in ModOptionChanged.Arguments arguments)
    {
        if (!Presets.TryGetValue(arguments.Mod.Identifier, out var presets))
            return;

        // We only react to changes in identifiers. We do not care about
        //   - additions (those are ignored until a manual update)
        //   - deletions (those are just kept and ignored on application until a manual update)
        var changes = false;
        switch (arguments.Type)
        {
            case ModOptionChangeType.GroupIdentifierChanged:
            {
                // Replace all occurrences in this mod's associated presets.
                foreach (var preset in presets)
                {
                    changes |= preset.Settings.ReplaceGroupIdentifier(arguments.Id, null, arguments.Group!.Id,
                        arguments.Group!.Name);
                }

                break;
            }
            case ModOptionChangeType.OptionIdentifierChanged:
            {
                // Replace all occurrences in this mod's associated presets in the correct group.
                var groupId = new ModObjectIdentifier(arguments.Group!);
                foreach (var preset in presets)
                {
                    changes |= preset.Settings.ReplaceOptionIdentifiers(arguments.Id, null, arguments.Group!.Id, arguments.Group!.Name,
                        groupId);
                }

                break;
            }
            case ModOptionChangeType.GroupRenamed:
            {
                // Replace all occurrences of the associated ID, and of ID-less identifiers of the same name.
                foreach (var preset in presets)
                {
                    changes |= preset.Settings.ReplaceGroupIdentifier(arguments.Id, null,               arguments.Id, arguments.Group!.Name);
                    changes |= preset.Settings.ReplaceGroupIdentifier(Guid.Empty,   arguments.OldName!, Guid.Empty,   arguments.Group!.Name);
                }

                break;
            }
            case ModOptionChangeType.OptionRenamed:
            {
                // Replace all occurrences of the associated ID, and of ID-less identifiers of the same name in the correct group.
                var groupId = new ModObjectIdentifier(arguments.Group!);
                foreach (var preset in presets)
                {
                    changes |= preset.Settings.ReplaceOptionIdentifiers(arguments.Id, null, arguments.Id, arguments.Group!.Name, groupId);
                    changes |= preset.Settings.ReplaceOptionIdentifiers(Guid.Empty, arguments.OldName!, Guid.Empty, arguments.Group!.Name,
                        groupId);
                }

                break;
            }
        }

        if (changes)
            _saveService.QueueSave(this);
    }

    public void Dispose()
    {
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
    }

    public string ToFilePath(FilenameService fileNames)
        => fileNames.SettingPresetFile;

    public void Save(Stream stream)
    {
        using var writer = new Utf8JsonWriter(stream, JsonFunctions.WriterOptions);
        writer.WriteNumber("Version"u8,   PresetVersion);
        writer.WriteNumber("Timestamp"u8, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (Presets.Count is 0)
            return;

        writer.WriteStartArray("Presets"u8);
        foreach (var (mod, preset) in Presets)
            preset.WriteJson(writer, mod, null);
        writer.WriteEndArray();
    }

    private void Load()
    {
        try
        {
            Reset();
            Populate(_saveService, ToFilePath(_saveService.FileNames));
        }
        catch (Exception ex)
        {
            Penumbra.Messager.NotificationMessage(ex, "Could not load Setting Presets", NotificationType.Error);
        }
    }

    public override void Reset()
        => Presets.Clear();

    protected override void PopulateData(scoped ref Utf8JsonReader reader, string filePath, object? userInput = null)
    {
        if (!reader.Read() || reader.TokenType is not JsonTokenType.StartObject)
            throw new JsonException("Setting Preset file does not start with an object.", filePath, null, null);

        var limit = reader.CreateObjectLimit();
        while (limit.Read(ref reader))
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
                throw new JsonException("Invalid JSON.");

            if (reader.NumberProperty("Version"u8, out int version))
            {
                if (version is not PresetVersion)
                    throw new Exception($"Unknown version {version} for setting preset manager.");
            }
            else if (reader.NumberProperty("Timestamp"u8, out long timestamp))
            {
                // ignored.
            }
            else if (reader.ArrayProperty("Presets"u8, out var presets, true))
            {
                while (presets.Read(ref reader))
                {
                    if (reader.TokenType is not JsonTokenType.StartObject)
                        throw new JsonException("Invalid JSON.");

                    var preset = SettingPreset.ParseJson(ref reader, out var mod);
                    Presets.TryAdd(mod, preset);
                }
            }
            else
            {
                reader.Skip();
            }
        }
    }
}
