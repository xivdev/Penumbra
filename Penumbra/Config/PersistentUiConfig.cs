using Luna;
using Luna.Generators;
using Newtonsoft.Json;
using Penumbra.Files;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ModsTab.Selector;

namespace Penumbra;

public sealed partial class PersistentUiConfig : ConfigurationFile<FilenameService>
{
    [ConfigProperty]
    private bool _openWindowAtStart = false;

    [ConfigProperty]
    private bool _hideUiInGPose = false;

    [ConfigProperty]
    private bool _hideUiInCutscenes = true;

    [ConfigProperty]
    private bool _hideUiWhenUiHidden = false;

    [ConfigProperty]
    private bool _hideChangedItemFilters = false;

    [ConfigProperty]
    private bool _hidePrioritiesInSelector = false;

    [ConfigProperty]
    private bool _hideRedrawBar = false;

    [ConfigProperty]
    private bool _hideMachinistOffhandFromChangedItems = true;

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

    [ConfigProperty]
    private bool _printSuccessfulCommandsToChat = true;

    [ConfigProperty]
    private ChangedItemMode _changedItemDisplay = ChangedItemMode.GroupedCollapsed;

    [ConfigProperty]
    private ISortMode _sortMode = ISortMode.FoldersFirst;

    [ConfigProperty]
    private bool _openFoldersByDefault = false;

    [ConfigProperty]
    private int _singleGroupRadioMax = 2;

    [ConfigProperty]
    private bool _displayPages = true;

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

    [ConfigProperty]
    private string _quickMoveFolder1 = string.Empty;

    [ConfigProperty]
    private string _quickMoveFolder2 = string.Empty;

    [ConfigProperty]
    private string _quickMoveFolder3 = string.Empty;
}
