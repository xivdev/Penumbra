using System.Text.Json;
using Luna;
using Luna.Generators;
using Penumbra.Files;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ModsTab.Selector;

namespace Penumbra;

public sealed partial class UiConfig : ConfigurationFile<FilenameService>
{
    public const int NumQuickMoveFolders = 3;

    public readonly ColorDictionary<ColorId, ColorIdData> Colors = new();
    public readonly ColorCache<ColorId, ColorIdData>      ColorCache;

    #region Window

    [ConfigProperty]
    private bool _openWindowAtStart = false;

    [ConfigProperty]
    private bool _hideUiInGPose = false;

    [ConfigProperty]
    private bool _hideUiInCutscenes = true;

    [ConfigProperty]
    private bool _hideUiWhenUiHidden = false;

    #endregion

    #region Filters

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

    #endregion

    #region Display

    [ConfigProperty]
    private ChangedItemMode _changedItemDisplay = ChangedItemMode.GroupedCollapsed;

    [ConfigProperty(EventName = "HideChangedItemFiltersChanged")]
    private bool _hideChangedItemFilters = false;

    [ConfigProperty]
    private bool _hideRedrawBar = false;

    [ConfigProperty(EventName = "HideMachinistOffhandChanged")]
    private bool _hideMachinistOffhandFromChangedItems = true;

    #endregion

    #region ModSelector

    private QuickMoveFolders _quickMoveFolder = QuickMoveFolders.Create();

    public string QuickMoveFolder(int i)
        => _quickMoveFolder[i];

    public void SetQuickMoveFolder(int i, string value)
    {
        var current = _quickMoveFolder[i];
        if (current == value)
            return;

        _quickMoveFolder[i] = value;
        Save();
    }

    [ConfigProperty(EventName = "ShowRenameChanged")]
    private RenameField _showRename = RenameField.BothDataPrio;

    [ConfigProperty(EventName = "SortModeChanged")]
    private ISortMode _sortMode = ISortMode.FoldersFirst;

    [ConfigProperty]
    private bool _hidePrioritiesInSelector = false;

    [ConfigProperty]
    private bool _openFoldersByDefault = false;

    #endregion

    #region ModConfig

    [ConfigProperty]
    private int _singleGroupRadioMax = 2;

    [ConfigProperty]
    private bool _displayPages = true;

    [ConfigProperty]
    private float _modSettingItemSpacingFactor = 1f;

    [ConfigProperty]
    private float _modSettingBorderScale = 2f;

    [ConfigProperty]
    private float _modSettingLineScale = 2f;

    [ConfigProperty]
    private float _modSettingMaximumExtendLabelWidth = 200f;

    [ConfigProperty]
    private float _modSettingLabelAlignment;

    [ConfigProperty]
    private float _modSettingComboAlignment;

    /// <inheritdoc/>
    public UiConfig(SaveService saveService, PenumbraMessager messages)
        : base(saveService, messages)
    {
        ColorCache = new ColorCache<ColorId, ColorIdData>(Colors);
        UI.Classes.Colors.SetCache(ColorCache);
        Load();
    }

    #endregion

    public override int CurrentVersion
        => 100;

    protected override void AddData(Utf8JsonWriter j)
    {
        using (var tempObject = j.TemporaryObject("Window"u8))
        {
            tempObject.WriteIfNot("OpenWindowAtStart"u8,  OpenWindowAtStart,  false);
            tempObject.WriteIfNot("HideUiInGPose"u8,      HideUiInGPose,      false);
            tempObject.WriteIfNot("HideUiInCutscenes"u8,  HideUiInCutscenes,  true);
            tempObject.WriteIfNot("HideUiWhenUiHidden"u8, HideUiWhenUiHidden, false);
        }

        using (var tempObject = j.TemporaryObject("Filters"u8))
        {
            tempObject.WriteIfNot("RememberMod"u8,              RememberModFilters,              true);
            tempObject.WriteIfNot("RememberCollection"u8,       RememberCollectionFilters,       true);
            tempObject.WriteIfNot("RememberOnScreen"u8,         RememberOnScreenFilters,         true);
            tempObject.WriteIfNot("RememberChangedItem"u8,      RememberChangedItemFilters,      true);
            tempObject.WriteIfNot("RememberEffectiveChanges"u8, RememberEffectiveChangesFilters, true);
            tempObject.WriteIfNot("RememberResourceManager"u8,  RememberResourceManagerFilters,  true);
        }

        using (var tempObject = j.TemporaryObject("Display"u8))
        {
            tempObject.WriteIfNot("HideChangedItemFilters"u8,               HideChangedItemFilters,               false);
            tempObject.WriteIfNot("HideRedrawBar"u8,                        HideRedrawBar,                        false);
            tempObject.WriteIfNot("HideMachinistOffhandFromChangedItems"u8, HideMachinistOffhandFromChangedItems, true);
            tempObject.WriteEnumIfNot("ChangedItemDisplay"u8, ChangedItemDisplay, ChangedItemMode.GroupedCollapsed);
        }

        using (var tempObject = j.TemporaryObject("ModSelector"u8))
        {
            var quick = "QuickMoveFolder#"u8.ToArray();
            for (var i = 0; i < NumQuickMoveFolders; ++i)
            {
                quick[^1] = (byte)('1' + i);
                tempObject.WriteNonEmptyString(quick, _quickMoveFolder[i]);
            }

            tempObject.WriteEnumIfNot("ShowRename"u8, ShowRename, RenameField.BothDataPrio);
            tempObject.WriteIfNot("SortMode"u8,                 SortMode.GetType().Name,  nameof(ISortMode.FoldersFirst));
            tempObject.WriteIfNot("HidePrioritiesInSelector"u8, HidePrioritiesInSelector, false);
            tempObject.WriteIfNot("OpenFoldersByDefault"u8,     OpenFoldersByDefault,     false);
        }

        using (var tempObject = j.TemporaryObject("ModConfig"u8))
        {
            tempObject.WriteIfNot("DisplayPages"u8,            DisplayPages,                      true);
            tempObject.WriteIfNot("SingleGroupRadioMax"u8,     SingleGroupRadioMax,               2);
            tempObject.WriteIfNot("ItemSpacingFactor"u8,       ModSettingItemSpacingFactor,       1f);
            tempObject.WriteIfNot("BorderScale"u8,             ModSettingBorderScale,             2f);
            tempObject.WriteIfNot("LineScale"u8,               ModSettingLineScale,               2f);
            tempObject.WriteIfNot("MaximumExtendLabelWidth"u8, ModSettingMaximumExtendLabelWidth, 200f);
            tempObject.WriteIfNot("LabelAlignment"u8,          ModSettingLabelAlignment,          0f);
            tempObject.WriteIfNot("ComboAlignment"u8,          ModSettingComboAlignment,          0f);
        }

        j.WritePropertyName("Colors"u8);
        Colors.Serialize(j, false);
    }

    protected override void LoadData(in JsonElement j)
    {
        if (j.TryReadObject("Window"u8, out var window))
        {
            OpenWindowAtStart  = window.PropertyOrDefault("OpenWindowAtStart"u8,  OpenWindowAtStart);
            HideUiInGPose      = window.PropertyOrDefault("HideUiInGPose"u8,      HideUiInGPose);
            HideUiInCutscenes  = window.PropertyOrDefault("HideUiInCutscenes"u8,  HideUiInCutscenes);
            HideUiWhenUiHidden = window.PropertyOrDefault("HideUiWhenUiHidden"u8, HideUiWhenUiHidden);
        }

        if (j.TryReadObject("Filters"u8, out var filters))
        {
            RememberModFilters              = filters.PropertyOrDefault("RememberModFilters"u8,              RememberModFilters);
            RememberCollectionFilters       = filters.PropertyOrDefault("RememberCollectionFilters"u8,       RememberCollectionFilters);
            RememberOnScreenFilters         = filters.PropertyOrDefault("RememberOnScreenFilters"u8,         RememberOnScreenFilters);
            RememberChangedItemFilters      = filters.PropertyOrDefault("RememberChangedItemFilters"u8,      RememberChangedItemFilters);
            RememberEffectiveChangesFilters = filters.PropertyOrDefault("RememberEffectiveChangesFilters"u8, RememberEffectiveChangesFilters);
            RememberResourceManagerFilters  = filters.PropertyOrDefault("RememberResourceManagerFilters"u8,  RememberResourceManagerFilters);
        }

        if (j.TryReadObject("Display"u8, out var display))
        {
            HideChangedItemFilters = display.PropertyOrDefault("HideChangedItemFilters"u8, HideChangedItemFilters);
            HideRedrawBar          = display.PropertyOrDefault("HideRedrawBar"u8,          HideRedrawBar);
            HideMachinistOffhandFromChangedItems =
                display.PropertyOrDefault("HideMachinistOffhandFromChangedItems"u8, HideMachinistOffhandFromChangedItems);
            ChangedItemDisplay = display.EnumOrDefault("ChangedItemDisplay"u8, ChangedItemDisplay);
        }

        if (j.TryReadObject("ModSelector"u8, out var modSelector))
        {
            var quick = "QuickMoveFolder#"u8.ToArray();
            for (var i = 0; i < NumQuickMoveFolders; ++i)
            {
                quick[^1] = (byte)('1' + i);
                SetQuickMoveFolder(i, modSelector.PropertyOrDefault(quick, QuickMoveFolder(i)));
            }

            ShowRename = modSelector.EnumOrDefault("ShowRename"u8, ShowRename);
            SortMode = modSelector.TryReadProperty("SortMode"u8, out string? mode, true)
             && ISortMode.Valid.TryGetValue(mode ?? SortMode.GetType().Name, out var s)
                    ? s
                    : SortMode;
            HidePrioritiesInSelector = modSelector.PropertyOrDefault("HidePrioritiesInSelector"u8, HidePrioritiesInSelector);
            OpenFoldersByDefault     = modSelector.PropertyOrDefault("OpenFoldersByDefault"u8,     OpenFoldersByDefault);
        }

        if (j.TryReadObject("ModConfig"u8, out var modConfig))
        {
            DisplayPages                      = modConfig.PropertyOrDefault("DisplayPages"u8,            DisplayPages);
            SingleGroupRadioMax               = modConfig.PropertyOrDefault("SingleGroupRadioMax"u8,     SingleGroupRadioMax);
            ModSettingItemSpacingFactor       = modConfig.PropertyOrDefault("ItemSpacingFactor"u8,       ModSettingItemSpacingFactor);
            ModSettingBorderScale             = modConfig.PropertyOrDefault("BorderScale"u8,             ModSettingBorderScale);
            ModSettingLineScale               = modConfig.PropertyOrDefault("LineScale"u8,               ModSettingLineScale);
            ModSettingMaximumExtendLabelWidth = modConfig.PropertyOrDefault("MaximumExtendLabelWidth"u8, ModSettingMaximumExtendLabelWidth);
            ModSettingLabelAlignment          = modConfig.PropertyOrDefault("LabelAlignment"u8,          ModSettingLabelAlignment);
            ModSettingComboAlignment          = modConfig.PropertyOrDefault("ComboAlignment"u8,          ModSettingComboAlignment);
        }

        if (j.TryReadObject("Colors"u8, out var colors))
        {
#pragma warning disable CA1869
            var options = new JsonSerializerOptions(JsonFunctions.SerializerOptions);
#pragma warning restore CA1869
            options.Converters.Add(new ColorDictionaryConverter<ColorId, ColorIdData>(Messager, true, true, true));
            if (colors.Deserialize<ColorDictionary<ColorId, ColorIdData>>(options) is { } dict)
                dict.Apply(Colors, true);
        }
    }

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.Config.Ui;

    [InlineArray(NumQuickMoveFolders)]
    private struct QuickMoveFolders
    {
        public static QuickMoveFolders Create()
        {
            var ret = new QuickMoveFolders();
            for (var i = 0; i < NumQuickMoveFolders; ++i)
                ret[i] = string.Empty;
            return ret;
        }

        private string _0;
    }
}
