using Dalamud.Configuration;
using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Enums;
using Penumbra.Import.Structs;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI;
using Penumbra.UI.Classes;
using Penumbra.UI.ResourceWatcher;
using Penumbra.UI.Tabs;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace Penumbra;

[Serializable]
public class Configuration : IPluginConfiguration, ISavable
{
    [JsonIgnore]
    private readonly SaveService _saveService;

    public int Version { get; set; } = Constants.CurrentVersion;

    public int                  LastSeenVersion      { get; set; } = PenumbraChangelog.LastChangelogVersion;
    public ChangeLogDisplayType ChangeLogDisplayType { get; set; } = ChangeLogDisplayType.New;

    public bool   EnableMods      { get; set; } = true;
    public string ModDirectory    { get; set; } = string.Empty;
    public string ExportDirectory { get; set; } = string.Empty;

    public bool HideUiInGPose                  { get; set; } = false;
    public bool HideUiInCutscenes              { get; set; } = true;
    public bool HideUiWhenUiHidden             { get; set; } = false;
    public bool UseDalamudUiTextureRedirection { get; set; } = true;

    public bool UseCharacterCollectionInMainWindow { get; set; } = true;
    public bool UseCharacterCollectionsInCards     { get; set; } = true;
    public bool UseCharacterCollectionInInspect    { get; set; } = true;
    public bool UseCharacterCollectionInTryOn      { get; set; } = true;
    public bool UseOwnerNameForCharacterCollection { get; set; } = true;
    public bool UseNoModsInInspect                 { get; set; } = false;
    public bool HideChangedItemFilters             { get; set; } = false;

    public bool HidePrioritiesInSelector  { get; set; } = false;
    public bool HideRedrawBar             { get; set; } = false;
    public int  OptionGroupCollapsibleMin { get; set; } = 5;

    public bool    DebugSeparateWindow = false;
    public Vector2 MinimumSize         = new(Constants.MinimumSizeX, Constants.MinimumSizeY);

#if DEBUG
    public bool DebugMode { get; set; } = true;
#else
    public bool DebugMode { get; set; } = false;
#endif

    public int TutorialStep { get; set; } = 0;

    public bool   EnableResourceLogging     { get; set; } = false;
    public string ResourceLoggingFilter     { get; set; } = string.Empty;
    public bool   EnableResourceWatcher     { get; set; } = false;
    public bool   OnlyAddMatchingResources  { get; set; } = true;
    public int    MaxResourceWatcherRecords { get; set; } = ResourceWatcher.DefaultMaxEntries;

    public ResourceTypeFlag     ResourceWatcherResourceTypes      { get; set; } = ResourceExtensions.AllResourceTypes;
    public ResourceCategoryFlag ResourceWatcherResourceCategories { get; set; } = ResourceExtensions.AllResourceCategories;
    public RecordType           ResourceWatcherRecordTypes        { get; set; } = ResourceWatcher.AllRecords;


    [JsonConverter(typeof(SortModeConverter))]
    [JsonProperty(Order = int.MaxValue)]
    public ISortMode<Mod> SortMode = ISortMode<Mod>.FoldersFirst;

    public bool                     ScaleModSelector        { get; set; } = false;
    public float                    ModSelectorAbsoluteSize { get; set; } = Constants.DefaultAbsoluteSize;
    public int                      ModSelectorScaledSize   { get; set; } = Constants.DefaultScaledSize;
    public bool                     OpenFoldersByDefault    { get; set; } = false;
    public int                      SingleGroupRadioMax     { get; set; } = 2;
    public string                   DefaultImportFolder     { get; set; } = string.Empty;
    public string                   QuickMoveFolder1        { get; set; } = string.Empty;
    public string                   QuickMoveFolder2        { get; set; } = string.Empty;
    public string                   QuickMoveFolder3        { get; set; } = string.Empty;
    public DoubleModifier           DeleteModModifier       { get; set; } = new(ModifierHotkey.Control, ModifierHotkey.Shift);
    public CollectionsTab.PanelMode CollectionPanel         { get; set; } = CollectionsTab.PanelMode.SimpleAssignment;
    public TabType                  SelectedTab             { get; set; } = TabType.Settings;

    public ChangedItemDrawer.ChangedItemIcon ChangedItemFilter { get; set; } = ChangedItemDrawer.DefaultFlags;

    public bool PrintSuccessfulCommandsToChat { get; set; } = true;
    public bool FixMainWindow                 { get; set; } = false;
    public bool AutoDeduplicateOnImport       { get; set; } = true;
    public bool UseFileSystemCompression      { get; set; } = true;
    public bool EnableHttpApi                 { get; set; } = true;

    public string DefaultModImportPath    { get; set; } = string.Empty;
    public bool   AlwaysOpenDefaultImport { get; set; } = false;
    public bool   KeepDefaultMetaChanges  { get; set; } = false;
    public string DefaultModAuthor        { get; set; } = DefaultTexToolsData.Author;

    public Dictionary<ColorId, uint> Colors { get; set; }
        = Enum.GetValues<ColorId>().ToDictionary(c => c, c => c.Data().DefaultColor);

    /// <summary>
    /// Load the current configuration.
    /// Includes adding new colors and migrating from old versions.
    /// </summary>
    public Configuration(CharacterUtility utility, ConfigMigrationService migrator, SaveService saveService)
    {
        _saveService = saveService;
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

        if (File.Exists(_saveService.FileNames.ConfigFile))
            try
            {
                var text = File.ReadAllText(_saveService.FileNames.ConfigFile);
                JsonConvert.PopulateObject(text, this, new JsonSerializerSettings
                {
                    Error = HandleDeserializationError,
                });
            }
            catch (Exception ex)
            {
                Penumbra.Chat.NotificationMessage(ex,
                    "Error reading Configuration, reverting to default.\nYou may be able to restore your configuration using the rolling backups in the XIVLauncher/backups/Penumbra directory.",
                    "Error reading Configuration", "Error", NotificationType.Error);
            }

        migrator.Migrate(utility, this);
    }

    /// <summary> Save the current configuration. </summary>
    public void Save()
        => _saveService.DelaySave(this);

    /// <summary> Contains some default values or boundaries for config values. </summary>
    public static class Constants
    {
        public const int   CurrentVersion      = 7;
        public const float MaxAbsoluteSize     = 600;
        public const int   DefaultAbsoluteSize = 250;
        public const float MinAbsoluteSize     = 50;
        public const int   MaxScaledSize       = 80;
        public const int   DefaultScaledSize   = 20;
        public const int   MinScaledSize       = 5;
        public const int   MinimumSizeX        = 900;
        public const int   MinimumSizeY        = 675;

        public static readonly ISortMode<Mod>[] ValidSortModes =
        {
            ISortMode<Mod>.FoldersFirst,
            ISortMode<Mod>.Lexicographical,
            new ModFileSystem.ImportDate(),
            new ModFileSystem.InverseImportDate(),
            ISortMode<Mod>.InverseFoldersFirst,
            ISortMode<Mod>.InverseLexicographical,
            ISortMode<Mod>.FoldersLast,
            ISortMode<Mod>.InverseFoldersLast,
            ISortMode<Mod>.InternalOrder,
            ISortMode<Mod>.InverseInternalOrder,
        };
    }

    /// <summary> Convert SortMode Types to their name. </summary>
    private class SortModeConverter : JsonConverter<ISortMode<Mod>>
    {
        public override void WriteJson(JsonWriter writer, ISortMode<Mod>? value, JsonSerializer serializer)
        {
            value ??= ISortMode<Mod>.FoldersFirst;
            serializer.Serialize(writer, value.GetType().Name);
        }

        public override ISortMode<Mod> ReadJson(JsonReader reader, Type objectType, ISortMode<Mod>? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var name = serializer.Deserialize<string>(reader);
            if (name == null || !Constants.ValidSortModes.FindFirst(s => s.GetType().Name == name, out var mode))
                return existingValue ?? ISortMode<Mod>.FoldersFirst;

            return mode;
        }
    }

    public string ToFilename(FilenameService fileNames)
        => fileNames.ConfigFile;

    public void Save(StreamWriter writer)
    {
        using var jWriter    = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
        var       serializer = new JsonSerializer { Formatting         = Formatting.Indented };
        serializer.Serialize(jWriter, this);
    }
}
