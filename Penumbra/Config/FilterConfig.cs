using Luna;
using Luna.Generators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Enums;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab.Selector;
using Penumbra.UI.ResourceWatcher;
using MessageService = Penumbra.Services.MessageService;

namespace Penumbra;

public sealed partial class FilterConfig : ConfigurationFile<FilenameService>
{
    public override int CurrentVersion
        => 1;

    public FilterConfig(SaveService saveService, MessageService messager)
        : base(saveService, messager, TimeSpan.FromMinutes(5))
    {
        Load();
    }

    protected override void AddData(JsonTextWriter j)
    {
        WriteModsTab(j);
        WriteCollectionsTab(j);
        WriteChangedItemsTab(j);
        WriteEffectiveChangesTab(j);
        WriteOnScreenTab(j);
        WriteResourceManagerTab(j);
        WriteResourceWatcherTab(j);
    }

    protected override void LoadData(JObject j)
    {
        LoadModsTab(j);
        LoadCollectionsTab(j);
        LoadChangedItemsTab(j);
        LoadEffectiveChangesTab(j);
        LoadOnScreenTab(j);
        LoadResourceManagerTab(j);
        LoadResourceWatcherTab(j);
    }

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.FilterFile;


    #region Mods Tab

    [ConfigProperty]
    private ModTypeFilter _modTypeFilter = ModTypeFilterExtensions.UnfilteredStateMods;

    [ConfigProperty]
    private string _modFilter = string.Empty;

    [ConfigProperty]
    private ChangedItemIconFlag _modChangedItemTypeFilter = ChangedItemFlagExtensions.DefaultFlags;

    private void WriteModsTab(JsonTextWriter j)
    {
        if (ModTypeFilter is ModTypeFilterExtensions.UnfilteredStateMods
         && ModFilter.Length is 0
         && ModChangedItemTypeFilter is ChangedItemFlagExtensions.DefaultFlags)
            return;

        j.WritePropertyName("Mods");
        j.WriteStartObject();
        if (ModTypeFilter is not ModTypeFilterExtensions.UnfilteredStateMods)
        {
            j.WritePropertyName("TypeFilter");
            j.WriteValue((uint)ModTypeFilter);
        }

        if (ModFilter.Length > 0)
        {
            j.WritePropertyName("ModFilter");
            j.WriteValue(ModFilter);
        }

        if (ModChangedItemTypeFilter is not ChangedItemFlagExtensions.DefaultFlags)
        {
            j.WritePropertyName("ChangedItemTypeFilter");
            j.WriteValue((uint)ModChangedItemTypeFilter);
        }

        j.WriteEndObject();
    }

    private void LoadModsTab(JObject j)
    {
        if (j["Mods"] is not JObject mods)
            return;

        _modTypeFilter = mods["TypeFilter"]?.Value<uint>() is { } modTypeFilter
            ? (ModTypeFilter)modTypeFilter
            : ModTypeFilterExtensions.UnfilteredStateMods;
        _modFilter = mods["ModFilter"]?.Value<string>() ?? string.Empty;
        _modChangedItemTypeFilter = mods["ChangedItemTypeFilter"]?.Value<uint>() is { } changedItemFilter
            ? (ChangedItemIconFlag)changedItemFilter
            : ChangedItemFlagExtensions.DefaultFlags;
    }

    #endregion

    #region Collections Tab

    [ConfigProperty]
    private string _collectionFilter = string.Empty;

    private void WriteCollectionsTab(JsonTextWriter j)
    {
        if (CollectionFilter.Length is 0)
            return;

        j.WritePropertyName("Collections");
        j.WriteStartObject();
        if (CollectionFilter.Length > 0)
        {
            j.WritePropertyName("CollectionFilter");
            j.WriteValue(CollectionFilter);
        }

        j.WriteEndObject();
    }

    private void LoadCollectionsTab(JObject j)
    {
        if (j["Collections"] is JObject collections)
            _collectionFilter = collections["CollectionFilter"]?.Value<string>() ?? string.Empty;
    }

    #endregion

    #region Changed Items Tab

    // Changed Items tab
    [ConfigProperty]
    private string _changedItemItemFilter = string.Empty;

    [ConfigProperty]
    private string _changedItemModFilter = string.Empty;

    [ConfigProperty(EventName = "ChangedItemTypeFilterChanged")]
    private ChangedItemIconFlag _changedItemTypeFilter = ChangedItemFlagExtensions.DefaultFlags;

    private void WriteChangedItemsTab(JsonTextWriter j)
    {
        if (ChangedItemItemFilter.Length is 0
         && ChangedItemModFilter.Length is 0
         && ChangedItemTypeFilter is ChangedItemFlagExtensions.DefaultFlags)
            return;

        j.WritePropertyName("ChangedItems");
        j.WriteStartObject();
        if (ChangedItemItemFilter.Length > 0)
        {
            j.WritePropertyName("ItemFilter");
            j.WriteValue(ChangedItemItemFilter);
        }

        if (ChangedItemModFilter.Length > 0)
        {
            j.WritePropertyName("ModFilter");
            j.WriteValue(ChangedItemModFilter);
        }

        if (ChangedItemTypeFilter is not ChangedItemFlagExtensions.DefaultFlags)
        {
            j.WritePropertyName("TypeFilter");
            j.WriteValue((uint)ChangedItemTypeFilter);
        }

        j.WriteEndObject();
    }

    private void LoadChangedItemsTab(JObject j)
    {
        if (j["ChangedItems"] is not JObject changedItems)
            return;

        _changedItemItemFilter = changedItems["ItemFilter"]?.Value<string>() ?? string.Empty;
        _changedItemModFilter  = changedItems["ModFilter"]?.Value<string>() ?? string.Empty;
        _changedItemTypeFilter = changedItems["TypeFilter"]?.Value<uint>() is { } typeFilter
            ? (ChangedItemIconFlag)typeFilter
            : ChangedItemFlagExtensions.DefaultFlags;
    }

    #endregion

    #region Effective Changes tab

    [ConfigProperty]
    private string _effectiveChangesGamePathFilter = string.Empty;

    [ConfigProperty]
    private string _effectiveChangesFilePathFilter = string.Empty;

    private void WriteEffectiveChangesTab(JsonTextWriter j)
    {
        if (EffectiveChangesGamePathFilter.Length is 0 && EffectiveChangesFilePathFilter.Length is 0)
            return;

        j.WritePropertyName("EffectiveChanges");
        j.WriteStartObject();
        if (EffectiveChangesGamePathFilter.Length > 0)
        {
            j.WritePropertyName("GamePathFilter");
            j.WriteValue(EffectiveChangesGamePathFilter);
        }

        if (EffectiveChangesFilePathFilter.Length > 0)
        {
            j.WritePropertyName("FilePathFilter");
            j.WriteValue(EffectiveChangesFilePathFilter);
        }

        j.WriteEndObject();
    }

    private void LoadEffectiveChangesTab(JObject j)
    {
        if (j["EffectiveChanges"] is not JObject effectiveChanges)
            return;

        _effectiveChangesGamePathFilter = effectiveChanges["GamePathFilter"]?.Value<string>() ?? string.Empty;
        _effectiveChangesFilePathFilter = effectiveChanges["FilePathFilter"]?.Value<string>() ?? string.Empty;
    }

    #endregion

    #region On-Screen tab

    [ConfigProperty]
    private string _onScreenCharacterFilter = string.Empty;

    [ConfigProperty]
    private string _onScreenItemFilter = string.Empty;

    [ConfigProperty]
    private ChangedItemIconFlag _onScreenTypeFilter = ChangedItemFlagExtensions.DefaultFlags;

    public void ClearOnScreenFilters()
    {
        _onScreenCharacterFilter = string.Empty;
        _onScreenItemFilter      = string.Empty;
        _onScreenTypeFilter      = ChangedItemFlagExtensions.DefaultFlags;
    }

    private void WriteOnScreenTab(JsonTextWriter j)
    {
        if (OnScreenCharacterFilter.Length is 0
         && OnScreenItemFilter.Length is 0
         && OnScreenTypeFilter is ChangedItemFlagExtensions.DefaultFlags)
            return;

        j.WritePropertyName("OnScreen");
        j.WriteStartObject();
        if (OnScreenCharacterFilter.Length > 0)
        {
            j.WritePropertyName("CharacterFilter");
            j.WriteValue(OnScreenCharacterFilter);
        }

        if (OnScreenItemFilter.Length > 0)
        {
            j.WritePropertyName("ItemFilter");
            j.WriteValue(OnScreenItemFilter);
        }

        if (OnScreenTypeFilter is not ChangedItemFlagExtensions.DefaultFlags)
        {
            j.WritePropertyName("TypeFilter");
            j.WriteValue((uint)OnScreenTypeFilter);
        }

        j.WriteEndObject();
    }

    private void LoadOnScreenTab(JObject j)
    {
        if (j["OnScreen"] is not JObject onScreen)
            return;

        _onScreenCharacterFilter = onScreen["CharacterFilter"]?.Value<string>() ?? string.Empty;
        _onScreenItemFilter      = onScreen["ItemFilter"]?.Value<string>() ?? string.Empty;
        _onScreenTypeFilter = onScreen["TypeFilter"]?.Value<uint>() is { } typeFilter
            ? (ChangedItemIconFlag)typeFilter
            : ChangedItemFlagExtensions.DefaultFlags;
    }

    #endregion

    #region Resource Manager tab

    [ConfigProperty]
    private string _resourceManagerFilter = string.Empty;

    private void WriteResourceManagerTab(JsonTextWriter j)
    {
        if (ResourceManagerFilter.Length is 0)
            return;

        j.WritePropertyName("ResourceManager");
        j.WriteStartObject();
        if (ResourceManagerFilter.Length > 0)
        {
            j.WritePropertyName("PathFilter");
            j.WriteValue(ResourceManagerFilter);
        }

        j.WriteEndObject();
    }

    private void LoadResourceManagerTab(JObject j)
    {
        if (j["ResourceManager"] is JObject resourceManager)
            _resourceManagerFilter = resourceManager["PathFilter"]?.Value<string>() ?? string.Empty;
    }

    #endregion

    #region

    [ConfigProperty]
    private bool _resourceLoggerEnabled;

    [ConfigProperty]
    private int _resourceLoggerMaxEntries = 500;

    [ConfigProperty]
    private bool _resourceLoggerStoreOnlyMatching = true;

    [ConfigProperty]
    private bool _resourceLoggerWriteToLog;

    [ConfigProperty]
    private string _resourceLoggerLogFilter = string.Empty;

    [ConfigProperty]
    private string _resourceLoggerPathFilter = string.Empty;

    [ConfigProperty]
    private string _resourceLoggerCollectionFilter = string.Empty;

    [ConfigProperty]
    private string _resourceLoggerObjectFilter = string.Empty;

    [ConfigProperty]
    private string _resourceLoggerOriginalPathFilter = string.Empty;

    [ConfigProperty]
    private string _resourceLoggerResourceFilter = string.Empty;

    [ConfigProperty]
    private string _resourceLoggerCrcFilter = string.Empty;

    [ConfigProperty]
    private string _resourceLoggerRefFilter = string.Empty;

    [ConfigProperty]
    private string _resourceLoggerThreadFilter = string.Empty;

    [ConfigProperty]
    private RecordType _resourceLoggerRecordFilter = RecordTypeExtensions.All;

    [ConfigProperty]
    private BoolEnum _resourceLoggerCustomFilter = BoolEnumExtensions.All;

    [ConfigProperty]
    private BoolEnum _resourceLoggerSyncFilter = BoolEnumExtensions.All;

    [ConfigProperty]
    private ResourceCategoryFlag _resourceLoggerCategoryFilter = ResourceExtensions.AllResourceCategories;

    [ConfigProperty]
    private ResourceTypeFlag _resourceLoggerTypeFilter = ResourceExtensions.AllResourceTypes;

    [ConfigProperty]
    private LoadStateFlag _resourceLoggerLoadStateFilter = LoadStateExtensions.All;

    private void WriteResourceWatcherTab(JsonTextWriter j)
    {
        var jObj = new JObject();

        if (ResourceLoggerEnabled)
            jObj["Enabled"] = true;
        if (ResourceLoggerWriteToLog)
            jObj["WriteToLog"] = true;
        if (ResourceLoggerMaxEntries is not 500)
            jObj["MaxEntries"] = ResourceLoggerMaxEntries;
        if (!ResourceLoggerStoreOnlyMatching)
            jObj["StoreOnlyMatching"] = false;
        if (ResourceLoggerLogFilter.Length > 0)
            jObj["LogFilter"] = ResourceLoggerLogFilter;
        if (ResourceLoggerPathFilter.Length > 0)
            jObj["PathFilter"] = ResourceLoggerPathFilter;
        if (ResourceLoggerCollectionFilter.Length > 0)
            jObj["CollectionFilter"] = ResourceLoggerCollectionFilter;
        if (ResourceLoggerObjectFilter.Length > 0)
            jObj["ObjectFilter"] = ResourceLoggerObjectFilter;
        if (ResourceLoggerOriginalPathFilter.Length > 0)
            jObj["OriginalPathFilter"] = ResourceLoggerOriginalPathFilter;
        if (ResourceLoggerResourceFilter.Length > 0)
            jObj["ResourceFilter"] = ResourceLoggerResourceFilter;
        if (ResourceLoggerCrcFilter.Length > 0)
            jObj["CrcFilter"] = ResourceLoggerCrcFilter;
        if (ResourceLoggerRefFilter.Length > 0)
            jObj["RefFilter"] = ResourceLoggerRefFilter;
        if (ResourceLoggerThreadFilter.Length > 0)
            jObj["ThreadFilter"] = ResourceLoggerThreadFilter;

        if (ResourceLoggerRecordFilter is not RecordTypeExtensions.All)
            jObj["RecordFilter"] = (uint)ResourceLoggerRecordFilter;
        if (ResourceLoggerCustomFilter is not BoolEnumExtensions.All)
            jObj["CustomFilter"] = (uint)ResourceLoggerCustomFilter;
        if (ResourceLoggerSyncFilter is not BoolEnumExtensions.All)
            jObj["SyncFilter"] = (uint)ResourceLoggerSyncFilter;
        if (ResourceLoggerCategoryFilter != ResourceExtensions.AllResourceCategories)
            jObj["CategoryFilter"] = (uint)ResourceLoggerCategoryFilter;
        if (ResourceLoggerTypeFilter != ResourceExtensions.AllResourceTypes)
            jObj["TypeFilter"] = (uint)ResourceLoggerTypeFilter;
        if (ResourceLoggerLoadStateFilter is not LoadStateExtensions.All)
            jObj["LoadStateFilter"] = (uint)ResourceLoggerLoadStateFilter;

        if (jObj.Count is not 0)
        {
            j.WritePropertyName("ResourceWatcher");
            jObj.WriteTo(j);
        }
    }

    private void LoadResourceWatcherTab(JObject j)
    {
        if (j["ResourceWatcher"] is not JObject resourceWatcher)
            return;

        _resourceLoggerEnabled           = resourceWatcher["Enabled"]?.Value<bool>() ?? false;
        _resourceLoggerMaxEntries        = resourceWatcher["MaxEntries"]?.Value<int>() ?? 500;
        _resourceLoggerStoreOnlyMatching = resourceWatcher["StoreOnlyMatching"]?.Value<bool>() ?? true;
        _resourceLoggerWriteToLog        = resourceWatcher["WriteToLog"]?.Value<bool>() ?? false;

        _resourceLoggerLogFilter          = resourceWatcher["LogFilter"]?.Value<string>() ?? string.Empty;
        _resourceLoggerPathFilter         = resourceWatcher["PathFilter"]?.Value<string>() ?? string.Empty;
        _resourceLoggerCollectionFilter   = resourceWatcher["CollectionFilter"]?.Value<string>() ?? string.Empty;
        _resourceLoggerObjectFilter       = resourceWatcher["ObjectFilter"]?.Value<string>() ?? string.Empty;
        _resourceLoggerOriginalPathFilter = resourceWatcher["OriginalPathFilter"]?.Value<string>() ?? string.Empty;
        _resourceLoggerResourceFilter     = resourceWatcher["ResourceFilter"]?.Value<string>() ?? string.Empty;
        _resourceLoggerCrcFilter          = resourceWatcher["CrcFilter"]?.Value<string>() ?? string.Empty;
        _resourceLoggerRefFilter          = resourceWatcher["RefFilter"]?.Value<string>() ?? string.Empty;
        _resourceLoggerThreadFilter       = resourceWatcher["ThreadFilter"]?.Value<string>() ?? string.Empty;

        _resourceLoggerRecordFilter = resourceWatcher["RecordFilter"]?.Value<uint>() is { } recordFilter
            ? (RecordType)recordFilter
            : RecordTypeExtensions.All;
        _resourceLoggerCustomFilter = resourceWatcher["CustomFilter"]?.Value<uint>() is { } customFilter
            ? (BoolEnum)customFilter
            : BoolEnumExtensions.All;
        _resourceLoggerSyncFilter = resourceWatcher["SyncFilter"]?.Value<uint>() is { } syncFilter
            ? (BoolEnum)syncFilter
            : BoolEnumExtensions.All;
        _resourceLoggerCategoryFilter = resourceWatcher["CategoryFilter"]?.Value<uint>() is { } categoryFilter
            ? (ResourceCategoryFlag)categoryFilter
            : ResourceExtensions.AllResourceCategories;
        _resourceLoggerTypeFilter = resourceWatcher["TypeFilter"]?.Value<uint>() is { } typeFilter
            ? (ResourceTypeFlag)typeFilter
            : ResourceExtensions.AllResourceTypes;
        _resourceLoggerLoadStateFilter = resourceWatcher["LoadStateFilter"]?.Value<uint>() is { } loadStateFilter
            ? (LoadStateFlag)loadStateFilter
            : LoadStateExtensions.All;
    }

    #endregion
}
