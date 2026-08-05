using System.Text.Json;
using Luna;
using Luna.Generators;
using Penumbra.Enums;
using Penumbra.Files;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab.Selector;
using Penumbra.UI.ResourceWatcher;

namespace Penumbra;

public sealed partial class FilterConfig : ConfigurationFile<FilenameService>, IDisposable
{
    private readonly UiConfig _uiConfig;

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

    private void LoadModsTab(in JsonElement j)
    {
        if (!j.TryReadObject("Mods"u8, out var mods))
            return;

        _modTypeFilter            = mods.EnumOrDefault("TypeFilter"u8, ModTypeFilterExtensions.UnfilteredStateMods);
        _modFilter                = mods.PropertyOrDefault("ModFilter"u8, string.Empty);
        _modChangedItemTypeFilter = mods.EnumOrDefault("ChangedItemTypeFilter"u8, ChangedItemFlagExtensions.DefaultFlags);
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

    private void LoadCollectionsTab(in JsonElement j)
    {
        if (!j.TryReadObject("Collections"u8, out var collections))
            return;

        _collectionFilter = collections.PropertyOrDefault("CollectionFilter"u8, string.Empty);
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

    private void LoadChangedItemsTab(in JsonElement j)
    {
        if (!j.TryReadObject("ChangedItems"u8, out var changedItems))
            return;

        _changedItemItemFilter = changedItems.PropertyOrDefault("ItemFilter"u8, string.Empty);
        _changedItemModFilter  = changedItems.PropertyOrDefault("ModFilter"u8,  string.Empty);
        _changedItemTypeFilter =
            (ChangedItemIconFlag)changedItems.PropertyOrDefault("TypeFilter"u8, (uint)ChangedItemFlagExtensions.DefaultFlags);
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

    private void LoadEffectiveChangesTab(in JsonElement j)
    {
        if (!j.TryReadObject("EffectiveChanges"u8, out var effectiveChanges))
            return;

        _effectiveChangesGamePathFilter = effectiveChanges.PropertyOrDefault("GamePathFilter"u8, string.Empty);
        _effectiveChangesFilePathFilter = effectiveChanges.PropertyOrDefault("FilePathFilter"u8, string.Empty);
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

    private void LoadOnScreenTab(in JsonElement j)
    {
        if (!j.TryReadObject("OnScreen"u8, out var onScreen))
            return;

        _onScreenCharacterFilter = onScreen.PropertyOrDefault("CharacterFilter"u8, string.Empty);
        _onScreenItemFilter      = onScreen.PropertyOrDefault("ItemFilter"u8,      string.Empty);
        _onScreenTypeFilter      = onScreen.EnumOrDefault("TypeFilter"u8, ChangedItemFlagExtensions.DefaultFlags);
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

    private void LoadResourceManagerTab(in JsonElement j)
    {
        if (!j.TryReadObject("ResourceManager"u8, out var resourceManager))
            return;

        _resourceManagerFilter = resourceManager.PropertyOrDefault("PathFilter"u8, string.Empty);
    }

    #endregion

    #region Resource Logger

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
        tmp.WriteIfNot("Enabled"u8,           ResourceLoggerEnabled,           false);
        tmp.WriteIfNot("WriteToLog"u8,        ResourceLoggerWriteToLog,        false);
        tmp.WriteIfNot("MaxEntries"u8,        ResourceLoggerMaxEntries,        500);
        tmp.WriteIfNot("StoreOnlyMatching"u8, ResourceLoggerStoreOnlyMatching, true);
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

    private void LoadResourceWatcherTab(in JsonElement j)
    {
        if (!j.TryReadObject("ResourceWatcher"u8, out var resourceWatcher))
            return;

        _resourceLoggerEnabled           = resourceWatcher.PropertyOrDefault("Enabled"u8,           false);
        _resourceLoggerMaxEntries        = resourceWatcher.PropertyOrDefault("MaxEntries"u8,        500);
        _resourceLoggerStoreOnlyMatching = resourceWatcher.PropertyOrDefault("StoreOnlyMatching"u8, true);
        _resourceLoggerWriteToLog        = resourceWatcher.PropertyOrDefault("WriteToLog"u8,        false);

        _resourceLoggerLogFilter          = resourceWatcher.PropertyOrDefault("LogFilter"u8,          string.Empty);
        _resourceLoggerPathFilter         = resourceWatcher.PropertyOrDefault("PathFilter"u8,         string.Empty);
        _resourceLoggerCollectionFilter   = resourceWatcher.PropertyOrDefault("CollectionFilter"u8,   string.Empty);
        _resourceLoggerObjectFilter       = resourceWatcher.PropertyOrDefault("ObjectFilter"u8,       string.Empty);
        _resourceLoggerOriginalPathFilter = resourceWatcher.PropertyOrDefault("OriginalPathFilter"u8, string.Empty);
        _resourceLoggerResourceFilter     = resourceWatcher.PropertyOrDefault("ResourceFilter"u8,     string.Empty);
        _resourceLoggerCrcFilter          = resourceWatcher.PropertyOrDefault("CrcFilter"u8,          string.Empty);
        _resourceLoggerRefFilter          = resourceWatcher.PropertyOrDefault("RefFilter"u8,          string.Empty);
        _resourceLoggerThreadFilter       = resourceWatcher.PropertyOrDefault("ThreadFilter"u8,       string.Empty);

        _resourceLoggerRecordFilter    = resourceWatcher.EnumOrDefault("RecordFilter"u8,    RecordTypeExtensions.All);
        _resourceLoggerCustomFilter    = resourceWatcher.EnumOrDefault("CustomFilter"u8,    BoolEnumExtensions.All);
        _resourceLoggerSyncFilter      = resourceWatcher.EnumOrDefault("SyncFilter"u8,      BoolEnumExtensions.All);
        _resourceLoggerCategoryFilter  = resourceWatcher.EnumOrDefault("CategoryFilter"u8,  ResourceExtensions.AllResourceCategories);
        _resourceLoggerTypeFilter      = resourceWatcher.EnumOrDefault("TypeFilter"u8,      ResourceExtensions.AllResourceTypes);
        _resourceLoggerLoadStateFilter = resourceWatcher.EnumOrDefault("LoadStateFilter"u8, LoadStateExtensions.All);
    }

    #endregion

    public override int CurrentVersion
        => 100;

    public FilterConfig(SaveService saveService, PenumbraMessager messager, UiConfig uiConfig)
        : base(saveService, messager, TimeSpan.FromMinutes(5))
    {
        _uiConfig                               =  uiConfig;
        _uiConfig.HideChangedItemFiltersChanged += OnHideChangedItemFiltersChange;
        Load();
        OnHideChangedItemFiltersChange(_uiConfig.HideChangedItemFilters, true);
    }

    private void OnHideChangedItemFiltersChange(bool newValue, bool _)
    {
        if (!newValue)
            return;

        ChangedItemTypeFilter    = ChangedItemFlagExtensions.AllFlags;
        ModChangedItemTypeFilter = ChangedItemFlagExtensions.AllFlags;
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

    protected override void LoadData(in JsonElement j)
    {
        LoadModsTab(j);
        LoadCollectionsTab(j);
        LoadChangedItemsTab(j);
        LoadEffectiveChangesTab(j);
        LoadOnScreenTab(j);
        LoadResourceManagerTab(j);
        LoadResourceWatcherTab(j);
    }

    public void MigrationLoad(in JsonElement j)
    {
        LoadedVersion   = j.PropertyOrDefault("Version"u8,   -1);
        LoadedTimestamp = j.PropertyOrDefault("Timestamp"u8, DateTimeOffset.UnixEpoch);
        LoadData(j);
    }

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.Config.Filters;

    public void Dispose()
        => _uiConfig.HideChangedItemFiltersChanged -= OnHideChangedItemFiltersChange;
}
