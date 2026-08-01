using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImSharp;
using Luna;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Import.Textures;
using Penumbra.Interop;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.Integration;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ModsTab.Selector;

namespace Penumbra.UI;

public sealed class SettingsTab : ITab<TabType>
{
    public TabType Identifier
        => TabType.Settings;

    public ReadOnlySpan<byte> Label
        => "Settings"u8;

    private readonly Configuration               _config;
    private readonly FontReloader                _fontReloader;
    private readonly TutorialService             _tutorial;
    private readonly Penumbra                    _penumbra;
    private readonly FileDialogService           _fileDialog;
    private readonly ModManager                  _modManager;
    private readonly FileWatcher                 _fileWatcher;
    private readonly ModExportManager            _modExportManager;
    private readonly CharacterUtility            _characterUtility;
    private readonly ResidentResourceManager     _residentResources;
    private readonly HttpApi                     _httpApi;
    private readonly DalamudSubstitutionProvider _dalamudSubstitutionProvider;
    private readonly FileCompactor               _compactor;
    private readonly DalamudConfigService        _dalamudConfig;
    private readonly IDalamudPluginInterface     _pluginInterface;
    private readonly IDataManager                _gameData;
    private readonly PredefinedTagManager        _predefinedTagManager;
    private readonly CrashHandlerService         _crashService;
    private readonly MigrationSectionDrawer      _migrationDrawer;
    private readonly CollectionAutoSelector      _autoSelector;
    private readonly AttributeHook               _attributeHook;
    private readonly PcpService                  _pcpService;
    private readonly IntegrationSettingsRegistry _integrationSettings;
    private readonly ModFileSystemDrawer         _modFileSystemDrawer;

    

    public SettingsTab(IDalamudPluginInterface pluginInterface, Configuration config, FontReloader fontReloader, TutorialService tutorial,
        Penumbra penumbra, FileDialogService fileDialog, ModManager modManager, CharacterUtility characterUtility,
        ResidentResourceManager residentResources, ModExportManager modExportManager,
        FileWatcher fileWatcher, HttpApi httpApi,
        DalamudSubstitutionProvider dalamudSubstitutionProvider, FileCompactor compactor, DalamudConfigService dalamudConfig,
        IDataManager gameData, PredefinedTagManager predefinedTagConfig, CrashHandlerService crashService,
        MigrationSectionDrawer migrationDrawer, CollectionAutoSelector autoSelector, AttributeHook attributeHook, PcpService pcpService,
        IntegrationSettingsRegistry integrationSettings, ModFileSystemDrawer modFileSystemDrawer)
    {
        _pluginInterface             = pluginInterface;
        _config                      = config;
        _fontReloader                = fontReloader;
        _tutorial                    = tutorial;
        _penumbra                    = penumbra;
        _fileDialog                  = fileDialog;
        _modManager                  = modManager;
        _characterUtility            = characterUtility;
        _residentResources           = residentResources;
        _modExportManager            = modExportManager;
        _fileWatcher                 = fileWatcher;
        _httpApi                     = httpApi;
        _dalamudSubstitutionProvider = dalamudSubstitutionProvider;
        _compactor                   = compactor;
        _dalamudConfig               = dalamudConfig;
        _gameData                    = gameData;
        if (_compactor.CanCompact)
            _compactor.Enabled = _config.UseFileSystemCompression;
        _predefinedTagManager = predefinedTagConfig;
        _crashService         = crashService;
        _migrationDrawer      = migrationDrawer;
        _autoSelector         = autoSelector;
        _attributeHook        = attributeHook;
        _pcpService           = pcpService;
        _integrationSettings  = integrationSettings;
        _modFileSystemDrawer  = modFileSystemDrawer;
    }

    public void PostTabButton()
    {
        _tutorial.OpenTutorial(BasicTutorialSteps.Fin);
        _tutorial.OpenTutorial(BasicTutorialSteps.Faq1);
        _tutorial.OpenTutorial(BasicTutorialSteps.Faq2);
    }

    public void DrawContent()
    {
        using var child = Im.Child.Begin("##SettingsTab"u8, -Vector2.One);
        if (!child)
            return;

        DrawEnabledBox();
        Im.Line.New();

        Im.Line.New();
        DrawRootFolder();
        DrawDirectoryButtons();
        Im.Line.New();
        Im.Line.New();

        DrawGeneralSettings();
        _migrationDrawer.Draw();
        DrawColorSettings();
        DrawPredefinedTagsSection();
        DrawAdvancedSettings();
        _integrationSettings.Draw();
        DrawSupportButtons();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool current, Action<bool> setter)
    {
        using var id  = Im.Id.Push(label);
        var       tmp = current;
        if (Im.Checkbox(StringU8.Empty, ref tmp) && tmp != current)
        {
            setter(tmp);
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel(label, tooltip);
    }

    #region Main Settings

    

    #endregion

    #region General Settings

    /// <summary> Draw all settings pertaining to the Mod Selector. </summary>
    private void DrawGeneralSettings()
    {
        if (!Im.Tree.Header("General"u8))
        {
            _tutorial.OpenTutorial(BasicTutorialSteps.GeneralSettings);
            return;
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.GeneralSettings);

        DrawHidingSettings();
        UiHelpers.DefaultLineSpace();

        DrawMiscSettings();
        UiHelpers.DefaultLineSpace();

        DrawIdentificationSettings();
        UiHelpers.DefaultLineSpace();

        DrawModSelectorSettings();
        UiHelpers.DefaultLineSpace();

        DrawModHandlingSettings();
        UiHelpers.DefaultLineSpace();

        DrawModEditorSettings();
        Im.Line.New();
    }

    /// <summary> Draw a selection for the maximum number of single select options displayed as a radio toggle. </summary>
    private void DrawSingleSelectRadioMax()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##SingleSelectRadioMax"u8, _config.SingleGroupRadioMax, out var newValue, 1, null, 0.01f,
                SliderFlags.AlwaysClamp))
        {
            _config.SingleGroupRadioMax = newValue;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Upper Limit for Single-Selection Group Radio Buttons"u8,
            "All Single-Selection Groups with more options than specified here will be displayed as Combo-Boxes at the top.\n"u8
          + "All other Single-Selection Groups will be displayed as a set of Radio-Buttons."u8);
    }

    /// <summary> Draw the window hiding state checkboxes.  </summary>
    private void DrawHidingSettings()
    {
        Checkbox("Open Config Window at Game Start"u8,
            "Whether the Penumbra main window should be open or closed after launching the game."u8,
            _config.OpenWindowAtStart, v => _config.OpenWindowAtStart = v);

        Checkbox("Hide Config Window when UI is Hidden"u8,
            "Hide the Penumbra main window when you manually hide the in-game user interface."u8, _config.HideUiWhenUiHidden,
            v =>
            {
                _config.HideUiWhenUiHidden                   = v;
                _pluginInterface.UiBuilder.DisableUserUiHide = !v;
            });
        Checkbox("Hide Config Window when in Cutscenes"u8,
            "Hide the Penumbra main window when you are currently watching a cutscene."u8, _config.HideUiInCutscenes,
            v =>
            {
                _config.HideUiInCutscenes                        = v;
                _pluginInterface.UiBuilder.DisableCutsceneUiHide = !v;
            });
        Checkbox("Hide Config Window when in GPose"u8,
            "Hide the Penumbra main window when you are currently in GPose mode."u8, _config.HideUiInGPose,
            v =>
            {
                _config.HideUiInGPose                         = v;
                _pluginInterface.UiBuilder.DisableGposeUiHide = !v;
            });

        Im.Separator();
        Checkbox("Remember Mod Filters Across Sessions"u8,
            "Whether filters in the Mods tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberModFilters, v => _config.RememberModFilters = v);
        Checkbox("Remember Collection Filters Across Sessions"u8,
            "Whether filters in the Collections tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberCollectionFilters, v => _config.RememberCollectionFilters = v);
        Checkbox("Remember Changed Items Filters Across Sessions"u8,
            "Whether filters in the Changed Items tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberChangedItemFilters, v => _config.RememberChangedItemFilters = v);
        Checkbox("Remember Effective Changes Filters Across Sessions"u8,
            "Whether filters in the Effective Changes tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberEffectiveChangesFilters, v => _config.RememberEffectiveChangesFilters = v);
        Checkbox("Remember On-Screen Filters Across Sessions"u8,
            "Whether filters in the On-Screen tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberOnScreenFilters, v => _config.RememberOnScreenFilters = v);
        Checkbox("Remember Resource Manager Filters Across Sessions"u8,
            "Whether filters in the Resource Manager tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberResourceManagerFilters, v => _config.RememberResourceManagerFilters = v);
    }

    /// <summary> Draw all settings that do not fit into other categories. </summary>
    private void DrawMiscSettings()
    {
        Checkbox("Automatically Select Character-Associated Collection"u8,
            "On every login, automatically select the collection associated with the current character as the current collection for editing."u8,
            _config.AutoSelectCollection, _autoSelector.SetAutomaticSelection);
        Checkbox("Print Chat Command Success Messages to Chat"u8,
            "Chat Commands usually print messages on failure but also on success to confirm your action. You can disable this here."u8,
            _config.PrintSuccessfulCommandsToChat, v => _config.PrintSuccessfulCommandsToChat = v);
        Checkbox("Hide Redraw Bar in Mod Panel"u8, "Hides the lower redraw buttons in the mod panel in your Mods tab."u8,
            _config.HideRedrawBar,                 v => _config.HideRedrawBar = v);
        Checkbox("Hide Changed Item Filters"u8,
            "Hides the category filter line in the Changed Items tab and the Changed Items mod panel."u8,
            _config.HideChangedItemFilters, v =>
            {
                _config.HideChangedItemFilters = v;
                if (v)
                {
                    _config.Filters.ModChangedItemTypeFilter = ChangedItemFlagExtensions.AllFlags;
                    _config.Filters.ChangedItemTypeFilter    = ChangedItemFlagExtensions.AllFlags;
                    _config.Ephemeral.Save();
                }
            });

        ChangedItemModeExtensions.DrawCombo("##ChangedItemMode"u8, _config.ChangedItemDisplay, UiHelpers.InputTextWidth.X, v =>
        {
            _config.ChangedItemDisplay = v;
            _config.Save();
        });
        LunaStyle.DrawAlignedHelpMarkerLabel("Mod Changed Item Display"u8,
            "Configure how to display the changed items of a single mod in the mods info panel."u8);

        Checkbox("Omit Machinist Offhands in Changed Items"u8,
            "Omits all Aetherotransformers (machinist offhands) in the changed items tabs because any change on them changes all of them at the moment.\n\n"u8
          + "Changing this triggers a rediscovery of your mods so all changed items can be updated."u8,
            _config.HideMachinistOffhandFromChangedItems, v =>
            {
                _config.HideMachinistOffhandFromChangedItems = v;
                _modManager.DiscoverMods();
            });
        Checkbox("Hide Priority Numbers in Mod Selector"u8,
            "Hides the bracketed non-zero priority numbers displayed in the mod selector when there is enough space for them."u8,
            _config.HidePrioritiesInSelector, v => _config.HidePrioritiesInSelector = v);
        Checkbox("Draw Tabs for Option Pages"u8,
            "When this is on, pages set for options in a mod's metadata are drawn as a tab bar. When it is off, pages are drawn successively on the same page using sections of collapsing headers."u8,
            _config.DisplayPages, v => _config.DisplayPages = v);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupLine"u8, _config.Ui.ModSettingLineScale,
                out var newLine, "%.2f"u8, 0, 4, 0.005f, SliderFlags.AlwaysClamp))
            _config.Ui.ModSettingLineScale = newLine;
        LunaStyle.DrawAlignedHelpMarkerLabel("Group Settings Line Factor"u8,
            "The thickness of the tree line connecting group settings."u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupBorder"u8, _config.Ui.ModSettingBorderScale,
                out var newBorder, "%.2f"u8, 1, 4, 0.005f, SliderFlags.AlwaysClamp))
            _config.Ui.ModSettingBorderScale = newBorder;
        LunaStyle.DrawAlignedHelpMarkerLabel("Group Settings Border Factor"u8,
            "The thickness of the border around UI elements connected by the tree line in group settings."u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##vertSpace"u8, _config.Ui.ModSettingItemSpacingFactor,
                out var newFactor, "%.2f"u8, 0, 10, 0.01f, SliderFlags.AlwaysClamp))
            _config.Ui.ModSettingItemSpacingFactor = newFactor;
        LunaStyle.DrawAlignedHelpMarkerLabel("Vertical Spacing between Option Groups Factor"u8, 
            "An additional factor applied to your regular ImGui style's item spacing in the vertical direction between the nodes in your mod settings tab.\n\n"u8
          + "A value of 1 means that the normal item spacing is used."u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupAlign"u8, _config.Ui.ModSettingLabelAlignment,
                out var newAlignment, "%.2f"u8, 0, 1, 0.0005f, SliderFlags.AlwaysClamp))
            _config.Ui.ModSettingLabelAlignment = newAlignment;
        LunaStyle.DrawAlignedHelpMarkerLabel("Group Label Text Alignment"u8,
            "The alignment of the text in group labels. A value of 0 means the text is left-aligned, and a value of 1 means it is right-aligned. "u8
          + "The caret is always left-aligned, and the tooltip icon is always right-aligned."u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##comboAlign"u8, _config.Ui.ModSettingComboAlignment,
                out var newCombo, "%.2f"u8, 0, 1, 0.0005f, SliderFlags.AlwaysClamp))
            _config.Ui.ModSettingComboAlignment = newCombo;
        LunaStyle.DrawAlignedHelpMarkerLabel("Setting Combo Preview Text Alignment"u8,
            "The alignment of the preview text in single select combos. A value of 0 means the text is left-aligned, and a value of 1 means it is right-aligned. "u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupHomo"u8, _config.Ui.ModSettingMaximumExtendLabelWidth,
                out var newExtend, "%.0f"u8, -1))
            _config.Ui.ModSettingMaximumExtendLabelWidth = newExtend;
        LunaStyle.DrawAlignedHelpMarkerLabel("Maximum Group Label Homogenization"u8,
            "The maximum width in unscaled pixels that group labels are extended in the settings screen. "u8
          + "Labels are sized according to the largest group label available, up to this value. "u8
          + "If a group label requires more space than this, it is an outlier and other labels are not extended to its width."u8);

        DrawSingleSelectRadioMax();
    }

    /// <summary> Draw all settings pertaining to actor identification for collections. </summary>
    private void DrawIdentificationSettings()
    {
        Checkbox("Use Interface Collection for other Plugin UIs"u8,
            "Use the collection assigned to your interface for other plugins requesting UI-textures and icons through Dalamud."u8,
            _dalamudSubstitutionProvider.Enabled, _dalamudSubstitutionProvider.Set);
        Checkbox("Use Assigned Collections in Lobby"u8,
            "If this is disabled, no mods are applied to characters in the lobby or at the aesthetician."u8,
            _config.ShowModsInLobby, v => _config.ShowModsInLobby = v);
        Checkbox("Use Assigned Collections in Character Window"u8,
            "Use the individual collection for your characters name or the Your Character collection in your main character window, if it is set."u8,
            _config.UseCharacterCollectionInMainWindow, v => _config.UseCharacterCollectionInMainWindow = v);
        Checkbox("Use Assigned Collections in Adventurer Cards"u8,
            "Use the appropriate individual collection for the adventurer card you are currently looking at, based on the adventurer's name."u8,
            _config.UseCharacterCollectionsInCards, v => _config.UseCharacterCollectionsInCards = v);
        Checkbox("Use Assigned Collections in Try-On Window"u8,
            "Use the individual collection for your character's name in your try-on, dye preview or glamour plate window, if it is set."u8,
            _config.UseCharacterCollectionInTryOn, v => _config.UseCharacterCollectionInTryOn = v);
        Checkbox("Use No Mods in Inspect Windows"u8,
            "Use the empty collection for characters you are inspecting, regardless of the character.\n"u8
          + "Takes precedence before the next option."u8, _config.UseNoModsInInspect, v => _config.UseNoModsInInspect = v);
        Checkbox("Use Assigned Collections in Inspect Windows"u8,
            "Use the appropriate individual collection for the character you are currently inspecting, based on their name."u8,
            _config.UseCharacterCollectionInInspect, v => _config.UseCharacterCollectionInInspect = v);
        Checkbox("Use Assigned Collections based on Ownership"u8,
            "Use the owner's name to determine the appropriate individual collection for mounts, companions, accessories and combat pets. This includes trust or squadron companions."u8,
            _config.UseOwnerNameForCharacterCollection, v => _config.UseOwnerNameForCharacterCollection = v);
        if (_config.UseOwnerNameForCharacterCollection)
            using (Im.Indent(Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X))
            {
                Checkbox("Include Hostile Owned Actors"u8,
                    "Include any hostile actors that are owned by the character, such as enemies spawned for solo quests."u8,
                    _config.UseOwnerForHostiles, v => _config.UseOwnerForHostiles = v);
            }
    }

    /// <summary> Different supported sort modes as a combo. </summary>
    private void DrawFolderSortType()
    {
        if (SortModeCombo.DrawCombo(ISortMode.Valid.Values, "##sortMode"u8, _config.SortMode, out var newSortMode, false,
                UiHelpers.InputTextWidth.X))
        {
            _config.SortMode              = newSortMode!;
            _modFileSystemDrawer.SortMode = newSortMode!;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Sort Mode"u8, "Choose the sort mode for the mod selector in the mods tab."u8);
    }

    private void DrawRenameSettings()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        using (var combo = Im.Combo.Begin("##renameSettings"u8, _config.ShowRename.ToNameU8()))
        {
            if (combo)
                foreach (var value in RenameField.Values)
                {
                    if (Im.Selectable(value.ToNameU8(), _config.ShowRename == value))
                        _config.ShowRename = value;

                    Im.Tooltip.OnHover(value.Tooltip());
                }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Rename Fields in Mod Context Menu"u8,
            "Select which of the two renaming input fields are visible when opening the right-click context menu of a mod in the mod selector."u8);
    }

    /// <summary> Draw all settings pertaining to the mod selector. </summary>
    private void DrawModSelectorSettings()
    {
        DrawFolderSortType();
        DrawRenameSettings();
        Checkbox("Open Folders by Default"u8, "Whether to start with all folders collapsed or expanded in the mod selector."u8,
            _config.OpenFoldersByDefault,     v =>
            {
                _config.OpenFoldersByDefault = v;
                // TODO
                // SetFilterDirty
            });

        KeySelector.DoubleModifier("Destructive Modifier"u8,
            "A modifier you need to hold while clicking buttons that perform particularly destructive and generally irrecoverable actions, like deletions."u8,
            UiHelpers.InputTextWidth.X,
            _config.DeleteModModifier,
            v =>
            {
                _config.DeleteModModifier = v;
                _config.Save();
            });
        KeySelector.DoubleModifier("Misclick Modifier"u8,
            "A modifier you need to hold while clicking buttons that should not be toggled by accident, but are generally easily revertible, like the Incognito or Temporary Settings Mode toggles.."u8,
            UiHelpers.InputTextWidth.X,
            _config.IncognitoModifier,
            v =>
            {
                _config.IncognitoModifier = v;
                _config.Save();
            });
    }

    /// <summary> Draw all settings pertaining to import and export of mods. </summary>
    private void DrawModHandlingSettings()
    {
        Checkbox("Use Temporary Settings Per Default"u8,
            "When you make any changes to your collection, apply them as temporary changes first and require a click to 'turn permanent' if you want to keep them."u8,
            _config.DefaultTemporaryMode, v => _config.DefaultTemporaryMode = v);
        Checkbox("Replace Non-Standard Symbols On Import"u8,
            "Replace all non-ASCII symbols in mod and option names with underscores when importing mods."u8,
            _config.ReplaceNonAsciiOnImport,
            v => _config.ReplaceNonAsciiOnImport = v);
        Checkbox("Always Open Import at Default Directory"u8,
            "Open the import window at the location specified here every time, forgetting your previous path."u8,
            _config.AlwaysOpenDefaultImport, v => _config.AlwaysOpenDefaultImport = v);
        Checkbox("Handle PCP Files"u8,
            "When encountering specific mods, usually but not necessarily denoted by a .pcp file ending, Penumbra will automatically try to create an associated collection and assign it to a specific character for this mod package. This can turn this behaviour off if unwanted."u8,
            !_config.PcpSettings.DisableHandling, v => _config.PcpSettings.DisableHandling = !v);

        var active = _config.DeleteModModifier.IsActive();
        Im.Line.Same();
        if (ImEx.Button("Delete all PCP Mods"u8, default, "Deletes all mods tagged with 'PCP' from the mod list."u8, !active))
            _pcpService.CleanPcpMods();
        if (!active)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {_config.DeleteModModifier} while clicking.");

        Im.Line.Same();
        if (ImEx.Button("Delete all PCP Collections"u8, default,
                "Deletes all collections whose name starts with 'PCP/' from the collection list."u8, !active))
            _pcpService.CleanPcpCollections();
        if (!active)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {_config.DeleteModModifier} while clicking.");

        Checkbox("Allow Other Plugins Access to PCP Handling"u8,
            "When creating or importing PCP files, other plugins can add and interpret their own data to the character.json file."u8,
            _config.PcpSettings.AllowIpc, v => _config.PcpSettings.AllowIpc = v);

        Checkbox("Create PCP Collections"u8,
            "When importing PCP files, create the associated collection."u8,
            _config.PcpSettings.CreateCollection, v => _config.PcpSettings.CreateCollection = v);

        Checkbox("Assign PCP Collections"u8,
            "When importing PCP files and creating the associated collection, assign it to the associated character."u8,
            _config.PcpSettings.AssignCollection, v => _config.PcpSettings.AssignCollection = v);
        DrawDefaultModImportPath();
        DrawDefaultModAuthor();
        DrawDefaultModImportFolder();
        DrawPcpFolder();
        DrawPcpExtension();
        DrawDefaultModExportPath();
        Checkbox("Enable Directory Watcher"u8,
            "Enables a File Watcher that automatically listens for Mod files that enter a specified directory, causing Penumbra to open a popup to import these mods."u8,
            _config.EnableDirectoryWatch, _fileWatcher.Toggle);
        Checkbox("Enable Archive Peeking"u8,
            "Enables the File Watcher to Peek inside .rar .zip and .7z archives, extracting mods inside and causing Penumbra to open a popup to import these mods."u8,
            _config.EnableContainerPeeking, _fileWatcher.ToggleContainerPeeking);
        Checkbox("Enable Fully Automatic Import"u8,
            "Uses the File Watcher in order to skip the query popup and automatically import any new mods."u8,
            _config.EnableAutomaticModImport, v => _config.EnableAutomaticModImport = v);
        Checkbox("Prevent Exported Mods From Being Automatically Reimported"u8,
            "If your Automatic Import Directory is the same as your Default Mod Export Directory, prevents mods and character packs you export from being reimported or showing a query popup."u8,
            _config.PreventExportLoopback, v => _config.PreventExportLoopback = v);
        DrawFileWatcherPath();
        Checkbox("Always Open Detailed Mod Import Popup"u8,
            "Always open the detailed modal popup at the center of the screen with information about the latest imports, instead of the Dalamud notification."u8,
            _config.AlwaysShowDetailedModImport, v => _config.AlwaysShowDetailedModImport = v);
        Checkbox("Automatically Dismiss Reports of Successful Mod Imports"u8,
            "Makes report notifications automatically disappear after a few seconds if all the mods were successfully imported.\nReports that contain errors will still have to be manually dismissed."u8,
            _config.AutoDismissModImportSuccessReports, v => _config.AutoDismissModImportSuccessReports = v);
    }


    /// <summary> Draw input for the default import path for a mod. </summary>
    private void DrawDefaultModImportPath()
    {
        using var id = Im.Id.Push("##dmi"u8);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, _config.DefaultModImportPath, out string newDirectory))
        {
            _config.DefaultModImportPath = newDirectory;
            _config.Save();
        }

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "Select a directory via dialog."u8))
        {
            var startDir = _config.DefaultModImportPath.Length > 0 && Directory.Exists(_config.DefaultModImportPath)
                ? _config.DefaultModImportPath
                : Directory.Exists(_config.ModDirectory)
                    ? _config.ModDirectory
                    : null;

            _fileDialog.OpenFolderPicker("Choose Default Import Directory", (b, s) =>
            {
                if (!b)
                    return;

                _config.DefaultModImportPath = s;
                _config.Save();
            }, startDir, false);
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Default Mod Import Directory"u8,
            "Set the directory that gets opened when using the file picker to import mods for the first time."u8);
    }

    /// <summary> Draw input for the default export/backup path for mods. </summary>
    private void DrawDefaultModExportPath()
    {
        using var id = Im.Id.Push("##dme"u8);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, _config.ExportDirectory, out string newDirectory))
            _modExportManager.UpdateExportDirectory(newDirectory);

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "Select a directory via dialog."u8))
        {
            var startDir = _config.ExportDirectory.Length > 0 && Directory.Exists(_config.ExportDirectory)
                ? _config.ExportDirectory
                : Directory.Exists(_config.ModDirectory)
                    ? _config.ModDirectory
                    : null;
            _fileDialog.OpenFolderPicker("Choose Default Export Directory", (b, s) =>
            {
                if (b)
                    _modExportManager.UpdateExportDirectory(s);
            }, startDir, false);
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Default Mod Export Directory"u8,
            "Set the directory mods get saved to when using the export function or loaded from when reimporting backups.\n"u8
          + "Keep this empty to use the root directory."u8);
    }

    /// <summary> Draw input for the Automatic Mod import path. </summary>
    private void DrawFileWatcherPath()
    {
        using var id = Im.Id.Push("fw"u8);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, _config.WatchDirectory, out string newDirectory, maxLength: 256))
            _fileWatcher.UpdateDirectory(newDirectory);

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "Select a directory via dialog."u8))
        {
            var startDir = _config.WatchDirectory.Length > 0 && Directory.Exists(_config.WatchDirectory)
                ? _config.WatchDirectory
                : Directory.Exists(_config.ModDirectory)
                    ? _config.ModDirectory
                    : null;
            _fileDialog.OpenFolderPicker("Choose Automatic Import Directory", (b, s) =>
            {
                if (b)
                    _fileWatcher.UpdateDirectory(s);
            }, startDir, false);
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Automatic Import Directory"u8,
            "Choose the Directory the File Watcher listens to."u8);
    }

    /// <summary> Draw input for the default name to input as author into newly generated mods. </summary>
    private void DrawDefaultModAuthor()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##author"u8, _config.DefaultModAuthor, out string newAuthor))
        {
            _config.DefaultModAuthor = newAuthor;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Default Mod Author"u8, "Set the default author stored for newly created mods."u8);
    }

    /// <summary> Draw input for the default folder to sort put newly imported mods into. </summary>
    private void DrawDefaultModImportFolder()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##importFolder"u8, _config.DefaultImportFolder, out string newFolder))
        {
            _config.DefaultImportFolder = newFolder;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Default Mod Import Organizational Folder"u8,
            "Set the default Penumbra mod folder to place newly imported mods into.\nLeave blank to import into Root."u8);
    }

    /// <summary> Draw input for the default folder to sort put newly imported mods into. </summary>
    private void DrawPcpFolder()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##pcpFolder"u8, _config.PcpSettings.FolderName, out string newFolder))
        {
            _config.PcpSettings.FolderName = newFolder;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Default PCP Organizational Folder"u8,
            "The folder any penumbra character packs are moved to on import.\nLeave blank to import into Root."u8);
    }

    private void DrawPcpExtension()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##pcpExtension"u8, _config.PcpSettings.PcpExtension, out string newExtension))
        {
            _config.PcpSettings.PcpExtension = newExtension;
            _config.Save();
        }

        Im.Line.SameInner();
        if (ImEx.Button("Reset##pcpExtension"u8, Vector2.Zero, "Reset the extension to its default value of \".pcp\"."u8,
                _config.PcpSettings.PcpExtension is ".pcp"))
        {
            _config.PcpSettings.PcpExtension = ".pcp";
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("PCP Extension"u8,
            "The extension used when exporting PCP files. Should generally be either \".pcp\" or \".pmp\"."u8);
    }


    /// <summary> Draw all settings pertaining to advanced editing of mods. </summary>
    private void DrawModEditorSettings()
    {
        Checkbox("Advanced Editing: Automatically Pin Mod in Editing Window"u8,
            "Determines the default pinning behavior when opening a new Advanced Editing window.\n\nPinned: The editing window will stay on the mod it was on at the time of opening/pinning.\nUnpinned: When changing your selected mod in the main window, the editing window will follow the selection, unless a pinned window exists for the new selected mod."u8,
            _config.DefaultEditWindowModPinned, v => _config.DefaultEditWindowModPinned = v);

        Checkbox("Advanced Editing: Edit Raw Tile UV Transforms"u8,
            "Edit the raw matrix components of tile UV transforms, instead of having them decomposed into scale, rotation and shear."u8,
            _config.EditRawTileTransforms, v => _config.EditRawTileTransforms = v);

        Checkbox("Advanced Editing: Always Highlight Color Row Pair when Hovering Selection Button"u8,
            "Make the whole color row pair selection button highlight the pair in game, instead of just the crosshair, even without holding Control."u8,
            _config.WholePairSelectorAlwaysHighlights, v => _config.WholePairSelectorAlwaysHighlights = v);

        Checkbox("Advanced Editing: Unlock More Dye Chanels"u8,
            "Although the vanilla game is limited to two dye channels, the current material file format supports four.\nThis option will allow the use of those four dye channels in the material editor.\nPlease note, though, that this has limited usefulness: at the time of writing, those four channels are only usable within the material editor."u8,
            _config.AllDyeChannels, v => _config.AllDyeChannels = v);
    }

    #endregion

    /// <summary> Draw the entire Color subsection. </summary>
    private void DrawColorSettings()
    {
        using var header = Im.Tree.HeaderId("Colors"u8);
        if (!header)
            return;

        if (ColorSettingsDrawer.Draw(Penumbra.Messager, _config.Ui.Colors, _config.Ui.ColorCache))
        {
            CacheManager.Instance.SetColorsDirty();
            _config.Ui.Save();
        }

        Im.Line.New();
    }

    /// <summary> Draw the support button group on the right-hand side of the window. </summary>
    private void DrawSupportButtons()
    {
        var width = Im.Font.CalculateSize(UiHelpers.SupportInfoButtonText).X + Im.Style.FramePadding.X * 2;
        var xPos  = Im.Window.Width - width;
        // Respect the scroll bar width.
        if (Im.Scroll.MaximumY > 0)
            xPos -= Im.Style.ScrollbarSize + Im.Style.FramePadding.X;

        Im.Cursor.Position = new Vector2(xPos, Im.Style.FrameHeightWithSpacing);
        UiHelpers.DrawSupportButton(_penumbra);

        Im.Cursor.Position = new Vector2(xPos, 0);
        SupportButton.Discord(Penumbra.Messager, width);

        Im.Cursor.Position = new Vector2(xPos, 2 * Im.Style.FrameHeightWithSpacing);
        SupportButton.ReniGuide(Penumbra.Messager, width);

        Im.Cursor.Position = new Vector2(xPos, 3 * Im.Style.FrameHeightWithSpacing);
        if (Im.Button("Restart Tutorial"u8, new Vector2(width, 0)))
        {
            _config.Ephemeral.TutorialStep = 0;
            _config.Ephemeral.Save();
        }

        Im.Cursor.Position = new Vector2(xPos, 4 * Im.Style.FrameHeightWithSpacing);
        if (Im.Button("Show Changelogs"u8, new Vector2(width, 0)))
            _penumbra.ForceChangelogOpen();

        Im.Cursor.Position = new Vector2(xPos, 5 * Im.Style.FrameHeightWithSpacing);
        SupportButton.KoFiPatreon(Penumbra.Messager, new Vector2(width, 0));
    }

    private void DrawPredefinedTagsSection()
    {
        if (!Im.Tree.Header("Tags"u8))
            return;

        var tagIdx = TagButtons.Draw("Predefined Tags: "u8,
            "Predefined tags that can be added or removed from mods with a single click."u8, _predefinedTagManager,
            out var editedTag);

        if (tagIdx >= 0)
            _predefinedTagManager.ChangeSharedTag(tagIdx, editedTag);
    }
}
