using Dalamud.Configuration;
using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using Luna.Generators;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using Penumbra.Import.Structs;
using Penumbra.Interop.Services;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ModsTab.Selector;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace Penumbra;

[Serializable]
public partial class Configuration : IPluginConfiguration, ISavable, IService
{
    [JsonIgnore]
    private readonly SaveService _saveService;

    [JsonIgnore]
    public readonly EphemeralConfig Ephemeral;

    [JsonIgnore]
    public readonly FilterConfig Filters;

    [JsonIgnore]
    public readonly UiConfig Ui;

    public int Version { get; set; } = Constants.CurrentVersion;

    public ChangeLogDisplayType ChangeLogDisplayType { get; set; } = ChangeLogDisplayType.New;

    public event Action<bool>? ModsEnabled;

    [JsonIgnore]
    private bool _enableMods = true;

    public bool EnableMods
    {
        get => _enableMods;
        set => SetField(ref _enableMods, value, ModsEnabled);
    }

    public string ModDirectory    { get; set; } = string.Empty;
    public string ExportDirectory { get; set; } = string.Empty;
    public string WatchDirectory  { get; set; } = string.Empty;

    public bool? UseCrashHandler                { get; set; } = null;
    public bool  OpenWindowAtStart              { get; set; } = false;
    public bool  HideUiInGPose                  { get; set; } = false;
    public bool  HideUiInCutscenes              { get; set; } = true;
    public bool  HideUiWhenUiHidden             { get; set; } = false;
    public bool  UseDalamudUiTextureRedirection { get; set; } = true;

    public bool AutoSelectCollection { get; set; } = false;

    public bool ShowModsInLobby                      { get; set; } = true;
    public bool UseCharacterCollectionInMainWindow   { get; set; } = true;
    public bool UseCharacterCollectionsInCards       { get; set; } = true;
    public bool UseCharacterCollectionInInspect      { get; set; } = true;
    public bool UseCharacterCollectionInTryOn        { get; set; } = true;
    public bool UseOwnerNameForCharacterCollection   { get; set; } = true;
    public bool UseNoModsInInspect                   { get; set; } = false;
    public bool HideChangedItemFilters               { get; set; } = false;
    public bool ReplaceNonAsciiOnImport              { get; set; } = false;
    public bool HidePrioritiesInSelector             { get; set; } = false;
    public bool HideRedrawBar                        { get; set; } = false;
    public bool HideMachinistOffhandFromChangedItems { get; set; } = true;
    public bool DefaultTemporaryMode                 { get; set; } = false;
    public bool EnableDirectoryWatch                 { get; set; } = false;
    public bool EnableAutomaticModImport             { get; set; } = false;
    public bool AutoDismissModImportSuccessReports   { get; set; } = true;
    public bool AlwaysShowDetailedModImport          { get; set; } = false;
    public bool PreventExportLoopback                { get; set; } = true;
    public bool EnableCustomShapes                   { get; set; } = true;

    [ConfigProperty]
    private bool _includeShpkInSwap = false;

    public PcpSettings PcpSettings = new();

    [ConfigProperty]
    private bool _rememberModFilters = true;

    [ConfigProperty]
    private bool _rememberCollectionFilters = true;

    [ConfigProperty]
    private bool _rememberOnScreenFilters = true;

    [ConfigProperty]
    private bool _rememberChangedItemFilters = true;

    [ConfigProperty]
    private bool _rememberEffectiveChangesFilters = true;

    [ConfigProperty]
    private bool _rememberResourceManagerFilters = true;

    [ConfigProperty(EventName = "ShowRenameChanged")]
    private RenameField _showRename = RenameField.BothDataPrio;

    public ChangedItemMode ChangedItemDisplay        { get; set; } = ChangedItemMode.GroupedCollapsed;
    public int             OptionGroupCollapsibleMin { get; set; } = 5;

    public Vector2 MinimumSize = new(Constants.MinimumSizeX, Constants.MinimumSizeY);

#if DEBUG
    public bool DebugMode { get; set; } = true;
#else
    public bool DebugMode { get; set; } = false;
#endif

    [JsonConverter(typeof(SortModeConverter))]
    [JsonProperty(Order = int.MaxValue)]
    public ISortMode SortMode = ISortMode.FoldersFirst;

    public bool           OpenFoldersByDefault          { get; set; } = false;
    public int            SingleGroupRadioMax           { get; set; } = 2;
    public string         DefaultImportFolder           { get; set; } = string.Empty;
    public string         QuickMoveFolder1              { get; set; } = string.Empty;
    public string         QuickMoveFolder2              { get; set; } = string.Empty;
    public string         QuickMoveFolder3              { get; set; } = string.Empty;
    public DoubleModifier DeleteModModifier             { get; set; } = new(ModifierHotkey.Control, ModifierHotkey.Shift);
    public DoubleModifier IncognitoModifier             { get; set; } = new(ModifierHotkey.Control);
    public bool           PrintSuccessfulCommandsToChat { get; set; } = true;
    public bool           AutoDeduplicateOnImport       { get; set; } = true;
    public bool           AutoReduplicateUiOnImport     { get; set; } = true;
    public bool           UseFileSystemCompression      { get; set; } = true;
    public bool           EnableHttpApi                 { get; set; } = true;

    public bool MigrateImportedModelsToV6        { get; set; } = true;
    public bool MigrateImportedMaterialsToLegacy { get; set; } = true;

    public string DefaultModImportPath    { get; set; } = string.Empty;
    public bool   AlwaysOpenDefaultImport { get; set; } = false;
    public bool   KeepDefaultMetaChanges  { get; set; } = false;
    public string DefaultModAuthor        { get; set; } = DefaultTexToolsData.Author;

    public bool DefaultEditWindowModPinned        { get; set; } = true;
    public bool EditRawTileTransforms             { get; set; } = false;
    public bool WholePairSelectorAlwaysHighlights { get; set; } = false;

    public bool HdrRenderTargets { get; set; } = true;

    public Dictionary<ColorId, uint> Colors { get; set; }
        = ColorId.Values.ToDictionary(c => c, c => c.Data().DefaultColor);

    public Dictionary<ResourceType, string> PreferredEditorFactories { get; set; } = [];

    /// <summary>
    /// Load the current configuration.
    /// Includes adding new colors and migrating from old versions.
    /// </summary>
    public Configuration(CharacterUtility utility, ConfigMigrationService migrator, SaveService saveService, EphemeralConfig ephemeral,
        UiConfig ui, FilterConfig filters)
    {
        _saveService = saveService;
        Ephemeral    = ephemeral;
        Ui           = ui;
        Filters      = filters;
        Load(utility, migrator);
    }

    public void Load(CharacterUtility utility, ConfigMigrationService migrator)
    {
        static void HandleDeserializationError(object? sender, ErrorEventArgs errorArgs)
        {
            Penumbra.Log.Error(
                $"Error parsing Configuration at {errorArgs.ErrorContext.Path}, using default or migrating:\n{errorArgs.ErrorContext.Error}");
            errorArgs.ErrorContext.Handled = true;
        }

        if (File.Exists(_saveService.FileNames.ConfigurationFile))
            try
            {
                var text = File.ReadAllText(_saveService.FileNames.ConfigurationFile);
                JsonConvert.PopulateObject(text, this, new JsonSerializerSettings
                {
                    Error = HandleDeserializationError,
                });
            }
            catch (Exception ex)
            {
                Penumbra.Messager.NotificationMessage(ex,
                    "Error reading Configuration, reverting to default.\nYou may be able to restore your configuration using the rolling backups in the XIVLauncher/backups/Penumbra directory.",
                    "Error reading Configuration", NotificationType.Error);
            }

        migrator.Migrate(utility, this);
    }

    /// <summary> Save the current configuration. </summary>
    public void Save()
        => _saveService.QueueSave(this);

    /// <summary> Contains some default values or boundaries for config values. </summary>
    public static class Constants
    {
        public const int   CurrentVersion      = 13;
        public const int   MinimumSizeX        = 900;
        public const int   MinimumSizeY        = 675;
    }

    /// <summary> Convert SortMode Types to their name. </summary>
    private class SortModeConverter : JsonConverter<ISortMode>
    {
        public override void WriteJson(JsonWriter writer, ISortMode? value, JsonSerializer serializer)
        {
            value ??= ISortMode.FoldersFirst;
            serializer.Serialize(writer, value.GetType().Name);
        }

        public override ISortMode ReadJson(JsonReader reader, Type objectType, ISortMode? existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (serializer.Deserialize<string>(reader) is { } name)
                return ISortMode.Valid.GetValueOrDefault(name, existingValue ?? ISortMode.FoldersFirst);

            return existingValue ?? ISortMode.FoldersFirst;
        }
    }

    public string ToFilePath(FilenameService fileNames)
        => fileNames.ConfigurationFile;

    public void Save(Stream stream)
    {
        using var writer  = new StreamWriter(stream);
        using var jWriter = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        var serializer = new JsonSerializer { Formatting = Formatting.Indented };
        serializer.Serialize(jWriter, this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool SetField<T>(ref T field, T value, Action<T, T>? @event, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(value))
            return false;

        var oldValue = field;
        field = value;
        try
        {
            @event?.Invoke(oldValue, field);
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Error in subscribers updating configuration field {propertyName} from {oldValue} to {field}:\n{ex}");
            throw;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool SetField<T>(ref T field, T value, Action<T>? @event, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(value))
            return false;

        field = value;
        try
        {
            @event?.Invoke(field);
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Error in subscribers updating configuration field {propertyName} to {field}:\n{ex}");
            throw;
        }

        return true;
    }
}
