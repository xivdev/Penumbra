using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json;
using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.Enums;
using Penumbra.Interop.Services;
using Penumbra.Services;
using Penumbra.UI;
using Penumbra.UI.ResourceWatcher;
using Penumbra.UI.Tabs;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace Penumbra;

public class EphemeralConfig : ISavable
{
    [JsonIgnore]
    private readonly SaveService _saveService;

    public int                               Version                           { get; set; } = Configuration.Constants.CurrentVersion;
    public int                               LastSeenVersion                   { get; set; } = PenumbraChangelog.LastChangelogVersion;
    public bool                              DebugSeparateWindow               { get; set; } = false;
    public int                               TutorialStep                      { get; set; } = 0;
    public bool                              EnableResourceLogging             { get; set; } = false;
    public string                            ResourceLoggingFilter             { get; set; } = string.Empty;
    public bool                              EnableResourceWatcher             { get; set; } = false;
    public bool                              OnlyAddMatchingResources          { get; set; } = true;
    public ResourceTypeFlag                  ResourceWatcherResourceTypes      { get; set; } = ResourceExtensions.AllResourceTypes;
    public ResourceCategoryFlag              ResourceWatcherResourceCategories { get; set; } = ResourceExtensions.AllResourceCategories;
    public RecordType                        ResourceWatcherRecordTypes        { get; set; } = ResourceWatcher.AllRecords;
    public CollectionsTab.PanelMode          CollectionPanel                   { get; set; } = CollectionsTab.PanelMode.SimpleAssignment;
    public TabType                           SelectedTab                       { get; set; } = TabType.Settings;
    public ChangedItemDrawer.ChangedItemIcon ChangedItemFilter                 { get; set; } = ChangedItemDrawer.DefaultFlags;
    public bool                              FixMainWindow                     { get; set; } = false;

    /// <summary>
    /// Load the current configuration.
    /// Includes adding new colors and migrating from old versions.
    /// </summary>
    public EphemeralConfig(SaveService saveService)
    {
        _saveService = saveService;
        Load();
    }

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
        using var jWriter    = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
        var       serializer = new JsonSerializer { Formatting         = Formatting.Indented };
        serializer.Serialize(jWriter, this);
    }
}
