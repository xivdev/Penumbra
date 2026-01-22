using Dalamud.Interface.ImGuiNotification;
using Luna;
using Luna.Generators;
using Newtonsoft.Json;
using Penumbra.Services;
using Penumbra.UI;
using Penumbra.UI.ManagementTab;
using Penumbra.UI.ModsTab;
using Penumbra.UI.Tabs;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;
using TabType = Penumbra.Api.Enums.TabType;

namespace Penumbra;

public sealed partial class EphemeralConfig : ISavable, IService
{
    [JsonIgnore]
    private readonly SaveService _saveService;

    public int                 Version             { get; set; } = Configuration.Constants.CurrentVersion;
    public int                 LastSeenVersion     { get; set; } = PenumbraChangelog.LastChangelogVersion;
    public bool                DebugSeparateWindow { get; set; } = false;
    public int                 TutorialStep        { get; set; } = 0;
    public CollectionPanelMode CollectionPanel     { get; set; } = CollectionPanelMode.SimpleAssignment;
    public TabType             SelectedTab         { get; set; } = TabType.Settings;

    [ConfigProperty]
    private ManagementTabType _selectedManagementTab = ManagementTabType.UnusedMods;

    [ConfigProperty]
    private ModPanelTab _selectedModPanelTab = ModPanelTab.Settings;

    public bool            FixMainWindow                  { get; set; } = false;
    public HashSet<string> AdvancedEditingOpenForModPaths { get; set; } = [];
    public bool            ForceRedrawOnFileChange        { get; set; } = false;
    public bool            IncognitoMode                  { get; set; } = false;

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


    public string ToFilePath(FilenameService fileNames)
        => fileNames.EphemeralConfigFile;

    public void Save(StreamWriter writer)
    {
        using var jWriter = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        var serializer = new JsonSerializer { Formatting = Formatting.Indented };
        serializer.Serialize(jWriter, this);
    }
}
