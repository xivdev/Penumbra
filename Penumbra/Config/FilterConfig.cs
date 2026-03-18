using System.Text.Json;
using Luna;
using Luna.Generators;
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

    protected override void AddData(Utf8JsonWriter j)
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

    private void WriteModsTab(Utf8JsonWriter j)
    {
        using var tmp = j.TemporaryObject("Mods"u8);
        tmp.WriteUnsignedIfNot("TypeFilter"u8, ModTypeFilter, ModTypeFilterExtensions.UnfilteredStateMods);
        tmp.WriteNonEmptyString("ModFilter"u8, ModFilter);
        tmp.WriteUnsignedIfNot("ChangedItemTypeFilter"u8, ModChangedItemTypeFilter, ChangedItemFlagExtensions.DefaultFlags);
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

    private void WriteCollectionsTab(Utf8JsonWriter j)
    {
        using var tmp = j.TemporaryObject("Collections"u8);
        tmp.WriteNonEmptyString("CollectionFilter"u8, CollectionFilter);
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

    private void WriteChangedItemsTab(Utf8JsonWriter j)
    {
        using var tmp = j.TemporaryObject("ChangedItems"u8);
        tmp.WriteNonEmptyString("ItemFilter"u8, ChangedItemItemFilter);
        tmp.WriteNonEmptyString("ModFilter"u8,  ChangedItemModFilter);
        tmp.WriteUnsignedIfNot("TypeFilter"u8, ChangedItemTypeFilter, ChangedItemFlagExtensions.DefaultFlags);
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

    private void WriteEffectiveChangesTab(Utf8JsonWriter j)
    {
        using var tmp = j.TemporaryObject("EffectiveChanges"u8);
        tmp.WriteNonEmptyString("GamePathFilter"u8, EffectiveChangesGamePathFilter);
        tmp.WriteNonEmptyString("FilePathFilter"u8, EffectiveChangesFilePathFilter);
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

    private void WriteOnScreenTab(Utf8JsonWriter j)
    {
        using var tmp = j.TemporaryObject("OnScreen"u8);
        tmp.WriteNonEmptyString("CharacterFilter"u8, OnScreenCharacterFilter);
        tmp.WriteNonEmptyString("ItemFilter"u8,      OnScreenItemFilter);
        tmp.WriteUnsignedIfNot("TypeFilter"u8, OnScreenTypeFilter, ChangedItemFlagExtensions.DefaultFlags);
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

    private void WriteResourceManagerTab(Utf8JsonWriter j)
    {
        using var tmp = j.TemporaryObject("ResourceManager"u8);
        tmp.WriteNonEmptyString("PathFilter"u8, ResourceManagerFilter);
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

    private void WriteResourceWatcherTab(Utf8JsonWriter j)
    {
        using var tmp = j.TemporaryObject("ResourceWatcher"u8);
        tmp.WriteBoolIf("Enabled"u8,    ResourceLoggerEnabled,    false);
        tmp.WriteBoolIf("WriteToLog"u8, ResourceLoggerWriteToLog, false);
        tmp.WriteSignedIfNot("MaxEntries"u8, ResourceLoggerMaxEntries, 500);
        tmp.WriteBoolIf("StoreOnlyMatching"u8, ResourceLoggerStoreOnlyMatching, true);
        tmp.WriteNonEmptyString("LogFilter"u8,          ResourceLoggerLogFilter);
        tmp.WriteNonEmptyString("PathFilter"u8,         ResourceLoggerPathFilter);
        tmp.WriteNonEmptyString("CollectionFilter"u8,   ResourceLoggerCollectionFilter);
        tmp.WriteNonEmptyString("ObjectFilter"u8,       ResourceLoggerObjectFilter);
        tmp.WriteNonEmptyString("OriginalPathFilter"u8, ResourceLoggerOriginalPathFilter);
        tmp.WriteNonEmptyString("ResourceFilter"u8,     ResourceLoggerResourceFilter);
        tmp.WriteNonEmptyString("CrcFilter"u8,          ResourceLoggerCrcFilter);
        tmp.WriteNonEmptyString("RefFilter"u8,          ResourceLoggerRefFilter);
        tmp.WriteNonEmptyString("ThreadFilter"u8,       ResourceLoggerThreadFilter);
        tmp.WriteUnsignedIfNot("RecordFilter"u8,    ResourceLoggerRecordFilter,    RecordTypeExtensions.All);
        tmp.WriteUnsignedIfNot("CustomFilter"u8,    ResourceLoggerCustomFilter,    BoolEnumExtensions.All);
        tmp.WriteUnsignedIfNot("SyncFilter"u8,      ResourceLoggerSyncFilter,      BoolEnumExtensions.All);
        tmp.WriteUnsignedIfNot("CategoryFilter"u8,  ResourceLoggerCategoryFilter,  ResourceExtensions.AllResourceCategories);
        tmp.WriteUnsignedIfNot("TypeFilter"u8,      ResourceLoggerTypeFilter,      ResourceExtensions.AllResourceTypes);
        tmp.WriteUnsignedIfNot("LoadStateFilter"u8, ResourceLoggerLoadStateFilter, LoadStateExtensions.All);
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
