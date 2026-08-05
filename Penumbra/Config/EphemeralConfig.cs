using System.Text.Json;
using Luna;
using Luna.Generators;
using Penumbra.Files;
using Penumbra.Services;
using Penumbra.UI;
using Penumbra.UI.ManagementTab;
using Penumbra.UI.ModsTab;
using Penumbra.UI.Tabs;
using TabType = Penumbra.Api.Enums.TabType;

namespace Penumbra;

public sealed partial class EphemeralConfig : ConfigurationFile<FilenameService>
{
    #region Selection

    [ConfigProperty]
    private TabType _selectedTab = TabType.Settings;

    [ConfigProperty]
    private ManagementTabType _selectedManagementTab = ManagementTabType.UnusedMods;

    [ConfigProperty]
    private ModPanelTab _selectedModPanelTab = ModPanelTab.Settings;

    [ConfigProperty]
    private CollectionPanelMode _collectionPanel = CollectionPanelMode.SimpleAssignment;

    [ConfigProperty]
    private HashSet<string> _advancedEditingOpenForModPaths = [];

    #endregion

    #region State

    [ConfigProperty]
    private int _lastSeenVersion = PenumbraChangelog.LastChangelogVersion;

    [ConfigProperty]
    private int _tutorialStep = 0;

    [ConfigProperty]
    private bool _debugSeparateWindow = false;

    [ConfigProperty]
    private bool _incognitoMode = false;

    [ConfigProperty]
    private bool _forceRedrawOnFileChange = false;

    #endregion State

    #region Ui

    [ConfigProperty]
    private TwoPanelWidth _collectionsTabScale = new(0.25f, ScalingMode.Percentage);

    [ConfigProperty]
    private TwoPanelWidth _modTabScale = new(0.3f, ScalingMode.Percentage);

    /// <inheritdoc/>
    public EphemeralConfig(SaveService save, PenumbraMessager messages)
        : base(save, messages)
        => Load();

    #endregion

    public override int CurrentVersion
        => 100;

    protected override void AddData(Utf8JsonWriter j)
    {
        j.WriteStartObject("State"u8);
        j.WriteNumber("LastSeenVersion"u8, LastSeenVersion);
        j.WriteIfNot("TutorialStep"u8,            TutorialStep,            0);
        j.WriteIfNot("DebugSeparateWindow"u8,     DebugSeparateWindow,     false);
        j.WriteIfNot("IncognitoMode"u8,           IncognitoMode,           false);
        j.WriteIfNot("ForceRedrawOnFileChange"u8, ForceRedrawOnFileChange, false);
        j.WriteEndObject();

        using (var tmp = j.TemporaryObject("Ui"u8))
        {
            if (tmp.MarkUsed(CollectionsTabScale.Mode is not ScalingMode.Percentage || CollectionsTabScale.Width is not 0.25f))
                CollectionsTabScale.WriteJson(j, "CollectionsTabScale"u8);
            if (tmp.MarkUsed(ModTabScale.Mode is not ScalingMode.Percentage || ModTabScale.Width is not 0.3f))
                ModTabScale.WriteJson(j, "ModTabScale"u8);
        }

        using (var tmp = j.TemporaryObject("Selection"u8))
        {
            tmp.WriteEnumIfNot("Tab"u8,             SelectedTab,           TabType.Settings);
            tmp.WriteEnumIfNot("ManagementTab"u8,   SelectedManagementTab, ManagementTabType.UnusedMods);
            tmp.WriteEnumIfNot("ModPanelTab"u8,     SelectedModPanelTab,   ModPanelTab.Settings);
            tmp.WriteEnumIfNot("CollectionPanel"u8, CollectionPanel,       CollectionPanelMode.SimpleAssignment);
            if (tmp.MarkUsed(AdvancedEditingOpenForModPaths.Count > 0))
            {
                j.WriteStartArray("AdvancedEditingOpen"u8);
                foreach (var path in AdvancedEditingOpenForModPaths)
                    j.WriteStringValue(path);
                j.WriteEndArray();
            }
        }
    }

    protected override void LoadData(in JsonElement j)
    {
        if (j.TryReadObject("State"u8, out var state))
        {
            LastSeenVersion         = state.PropertyOrDefault("LastSeenVersion"u8,         LastSeenVersion);
            TutorialStep            = state.PropertyOrDefault("TutorialStep"u8,            TutorialStep);
            DebugSeparateWindow     = state.PropertyOrDefault("DebugSeparateWindow"u8,     DebugSeparateWindow);
            IncognitoMode           = state.PropertyOrDefault("IncognitoMode"u8,           IncognitoMode);
            ForceRedrawOnFileChange = state.PropertyOrDefault("ForceRedrawOnFileChange"u8, ForceRedrawOnFileChange);
        }

        if (j.TryReadObject("Ui"u8, out var ui))
        {
            CollectionsTabScale = TwoPanelWidth.ReadJson(ui, "CollectionsTabScale"u8, CollectionsTabScale);
            ModTabScale         = TwoPanelWidth.ReadJson(ui, "ModTabScale"u8,         ModTabScale);
        }

        if (j.TryReadObject("Selection"u8, out var selection))
        {
            SelectedTab           = selection.EnumOrDefault("Tab"u8,             SelectedTab);
            SelectedManagementTab = selection.EnumOrDefault("ManagementTab"u8,   SelectedManagementTab);
            SelectedModPanelTab   = selection.EnumOrDefault("ModPanelTab"u8,     SelectedModPanelTab);
            CollectionPanel       = selection.EnumOrDefault("CollectionPanel"u8, CollectionPanel);
            if (selection.TryReadObject("AdvancedEditingOpen"u8, out var advanced))
                AdvancedEditingOpenForModPaths = advanced.Deserialize<HashSet<string>>() ?? AdvancedEditingOpenForModPaths;
        }
    }

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.Config.Ephemeral;
}
