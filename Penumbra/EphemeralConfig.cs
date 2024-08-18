using Dalamud.Interface.ImGuiNotification;
using Newtonsoft.Json;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Enums;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI;
using Penumbra.UI.ResourceWatcher;
using Penumbra.UI.Tabs;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace Penumbra;

public class EphemeralConfig : ISavable, IDisposable, IService
{
    [JsonIgnore]
    private readonly SaveService _saveService;

    [JsonIgnore]
    private readonly ModPathChanged _modPathChanged;

    public int                      Version                           { get; set; } = Configuration.Constants.CurrentVersion;
    public int                      LastSeenVersion                   { get; set; } = PenumbraChangelog.LastChangelogVersion;
    public bool                     DebugSeparateWindow               { get; set; } = false;
    public int                      TutorialStep                      { get; set; } = 0;
    public bool                     EnableResourceLogging             { get; set; } = false;
    public string                   ResourceLoggingFilter             { get; set; } = string.Empty;
    public bool                     EnableResourceWatcher             { get; set; } = false;
    public bool                     OnlyAddMatchingResources          { get; set; } = true;
    public ResourceTypeFlag         ResourceWatcherResourceTypes      { get; set; } = ResourceExtensions.AllResourceTypes;
    public ResourceCategoryFlag     ResourceWatcherResourceCategories { get; set; } = ResourceExtensions.AllResourceCategories;
    public RecordType               ResourceWatcherRecordTypes        { get; set; } = ResourceWatcher.AllRecords;
    public CollectionsTab.PanelMode CollectionPanel                   { get; set; } = CollectionsTab.PanelMode.SimpleAssignment;
    public TabType                  SelectedTab                       { get; set; } = TabType.Settings;
    public ChangedItemIconFlag      ChangedItemFilter                 { get; set; } = ChangedItemFlagExtensions.DefaultFlags;
    public bool                     FixMainWindow                     { get; set; } = false;
    public string                   LastModPath                       { get; set; } = string.Empty;
    public bool                     AdvancedEditingOpen               { get; set; } = false;
    public bool                     ForceRedrawOnFileChange           { get; set; } = false;

    /// <summary>
    /// Load the current configuration.
    /// Includes adding new colors and migrating from old versions.
    /// </summary>
    public EphemeralConfig(SaveService saveService, ModPathChanged modPathChanged)
    {
        _saveService    = saveService;
        _modPathChanged = modPathChanged;
        Load();
        _modPathChanged.Subscribe(OnModPathChanged, ModPathChanged.Priority.EphemeralConfig);
    }

    public void Dispose()
        => _modPathChanged.Unsubscribe(OnModPathChanged);

    private void Load()
    {
        static void HandleDeserializationError(object? sender, ErrorEventArgs errorArgs)
        {
            Penumbra.Log.Error(
                $"Error parsing ephemeral Configuration at {errorArgs.ErrorContext.Path}, using default or migrating:\n{errorArgs.ErrorContext.Error}");
            errorArgs.ErrorContext.Handled = true;
        }

        if (!File.Exists(_saveService.FileNames.EphemeralConfigFile))
            return;

        try
        {
            var text = File.ReadAllText(_saveService.FileNames.EphemeralConfigFile);
            JsonConvert.PopulateObject(text, this, new JsonSerializerSettings
            {
                Error = HandleDeserializationError,
            });
        }
        catch (Exception ex)
        {
            Penumbra.Messager.NotificationMessage(ex,
                "Error reading ephemeral Configuration, reverting to default.",
                "Error reading ephemeral Configuration", NotificationType.Error);
        }
    }

    /// <summary> Save the current configuration. </summary>
    public void Save()
        => _saveService.DelaySave(this, TimeSpan.FromSeconds(5));


    public string ToFilename(FilenameService fileNames)
        => fileNames.EphemeralConfigFile;

    public void Save(StreamWriter writer)
    {
        using var jWriter = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        var serializer = new JsonSerializer { Formatting = Formatting.Indented };
        serializer.Serialize(jWriter, this);
    }

    /// <summary> Overwrite the last saved mod path if it changes. </summary>
    private void OnModPathChanged(ModPathChangeType type, Mod mod, DirectoryInfo? old, DirectoryInfo? _)
    {
        if (type is not ModPathChangeType.Moved || !string.Equals(old?.Name, LastModPath, StringComparison.OrdinalIgnoreCase))
            return;

        LastModPath = mod.Identifier;
        Save();
    }
}
