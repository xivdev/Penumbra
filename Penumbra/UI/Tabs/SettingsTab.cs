using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImSharp;
using Luna;
using OtterGui;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ModsTab.Selector;

namespace Penumbra.UI.Tabs;

public sealed class SettingsTab : ITab<TabType>
{
    public const int RootDirectoryMaxLength = 64;

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
    private readonly ModFileSystemSelector       _selector;
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
    private readonly CleanupService              _cleanupService;
    private readonly AttributeHook               _attributeHook;
    private readonly PcpService                  _pcpService;

    private readonly TagButtons _sharedTags = new();

    private string _lastCloudSyncTestedPath = string.Empty;
    private bool   _lastCloudSyncTestResult;

    public SettingsTab(IDalamudPluginInterface pluginInterface, Configuration config, FontReloader fontReloader, TutorialService tutorial,
        Penumbra penumbra, FileDialogService fileDialog, ModManager modManager, ModFileSystemSelector selector,
        CharacterUtility characterUtility, ResidentResourceManager residentResources, ModExportManager modExportManager,
        FileWatcher fileWatcher, HttpApi httpApi,
        DalamudSubstitutionProvider dalamudSubstitutionProvider, FileCompactor compactor, DalamudConfigService dalamudConfig,
        IDataManager gameData, PredefinedTagManager predefinedTagConfig, CrashHandlerService crashService,
        MigrationSectionDrawer migrationDrawer, CollectionAutoSelector autoSelector, CleanupService cleanupService,
        AttributeHook attributeHook, PcpService pcpService)
    {
        _pluginInterface             = pluginInterface;
        _config                      = config;
        _fontReloader                = fontReloader;
        _tutorial                    = tutorial;
        _penumbra                    = penumbra;
        _fileDialog                  = fileDialog;
        _modManager                  = modManager;
        _selector                    = selector;
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
        _cleanupService       = cleanupService;
        _attributeHook        = attributeHook;
        _pcpService           = pcpService;
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
        EphemeralCheckbox("Lock Main Window", "Prevent the main window from being resized or moved.", _config.Ephemeral.FixMainWindow,
            v => _config.Ephemeral.FixMainWindow = v);

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
        DrawSupportButtons();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(string label, string tooltip, bool current, Action<bool> setter)
    {
        using var id  = Im.Id.Push(label);
        var       tmp = current;
        if (Im.Checkbox(StringU8.Empty, ref tmp) && tmp != current)
        {
            setter(tmp);
            _config.Save();
        }

        Im.Line.Same();
        ImGuiUtil.LabeledHelpMarker(label, tooltip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void EphemeralCheckbox(string label, string tooltip, bool current, Action<bool> setter)
    {
        using var id  = Im.Id.Push(label);
        var       tmp = current;
        if (Im.Checkbox(StringU8.Empty, ref tmp) && tmp != current)
        {
            setter(tmp);
            _config.Ephemeral.Save();
        }

        Im.Line.Same();
        ImGuiUtil.LabeledHelpMarker(label, tooltip);
    }

    #region Main Settings

    /// <summary>
    /// Do not change the directory without explicitly pressing enter or this button.
    /// Shows up only if the current input does not correspond to the current directory.
    /// </summary>
    private bool DrawPressEnterWarning(string newName, string old, float width, bool saved, bool selected)
    {
        using var color = ImGuiColor.Button.Push(Colors.PressEnterWarningBg);
        var       w     = new Vector2(width, 0);
        var (text, valid) = CheckRootDirectoryPath(newName, old, selected);

        return (Im.Button(text, w) || saved) && valid;
    }

    /// <summary> Check a potential new root directory for validity and return the button text and whether it is valid. </summary>
    private (string Text, bool Valid) CheckRootDirectoryPath(string newName, string old, bool selected)
    {
        if (newName.Length > RootDirectoryMaxLength)
            return ($"Path is too long. The maximum length is {RootDirectoryMaxLength}.", false);

        if (Path.GetDirectoryName(newName).IsNullOrEmpty())
            return ("Path is not allowed to be a drive root. Please add a directory.", false);

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (IsSubPathOf(desktop, newName))
            return ("Path is not allowed to be on your Desktop.", false);

        var programFiles    = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (IsSubPathOf(programFiles, newName) || IsSubPathOf(programFilesX86, newName))
            return ("Path is not allowed to be in ProgramFiles.", false);

        var dalamud = _pluginInterface.ConfigDirectory.Parent!.Parent!;
        if (IsSubPathOf(dalamud.FullName, newName))
            return ("Path is not allowed to be inside your Dalamud directories.", false);

        if (Functions.GetDownloadsFolder(out var downloads) && IsSubPathOf(downloads, newName))
            return ("Path is not allowed to be inside your Downloads folder.", false);

        var gameDir = _gameData.GameData.DataPath.Parent!.Parent!.FullName;
        if (IsSubPathOf(gameDir, newName))
            return ("Path is not allowed to be inside your game folder.", false);

        if (_lastCloudSyncTestedPath != newName)
        {
            _lastCloudSyncTestResult = CloudApi.IsCloudSynced(newName);
            _lastCloudSyncTestedPath = newName;
        }

        if (_lastCloudSyncTestResult)
            return ("Path is not allowed to be cloud-synced.", false);

        return selected
            ? ($"Press Enter or Click Here to Save (Current Directory: {old})", true)
            : ($"Click Here to Save (Current Directory: {old})", true);

        static bool IsSubPathOf(string basePath, string subPath)
        {
            if (basePath.Length == 0)
                return false;

            var rel = Path.GetRelativePath(basePath, subPath);
            return rel == "." || !rel.StartsWith('.') && !Path.IsPathRooted(rel);
        }
    }

    /// <summary> Changing the base mod directory. </summary>
    private string? _newModDirectory;

    /// <summary>
    /// Draw a directory picker button that toggles the directory picker.
    /// Selecting a directory does behave the same as writing in the text input, i.e. needs to be saved.
    /// </summary>
    private void DrawDirectoryPickerButton()
    {
        if (!ImEx.Icon.Button(LunaStyle.FolderIcon, "Select a directory via dialog."u8))
            return;

        _newModDirectory ??= _config.ModDirectory;
        // Use the current input as start directory if it exists,
        // otherwise the current mod directory, otherwise the current application directory.
        var startDir = Directory.Exists(_newModDirectory)
            ? _newModDirectory
            : Directory.Exists(_config.ModDirectory)
                ? _config.ModDirectory
                : ".";

        _fileDialog.OpenFolderPicker("Choose Mod Directory", (b, s) => _newModDirectory = b ? s : _newModDirectory, startDir, false);
    }

    /// <summary>
    /// Draw the text input for the mod directory,
    /// as well as the directory picker button and the enter warning.
    /// </summary>
    private void DrawRootFolder()
    {
        if (_newModDirectory.IsNullOrEmpty())
            _newModDirectory = _config.ModDirectory;

        bool save, selected;
        using (Im.Group())
        {
            Im.Item.SetNextWidth(UiHelpers.InputTextMinusButton3);
            using (var color = ImStyleBorder.Frame.Push(Colors.RegexWarningBorder, Im.Style.GlobalScale, !_modManager.Valid))
            {
                color.Push(ImGuiColor.TextDisabled, Colors.RegexWarningBorder, !_modManager.Valid);
                save = Im.Input.Text("##rootDirectory"u8, ref _newModDirectory, "Enter Root Directory here (MANDATORY)..."u8,
                    InputTextFlags.EnterReturnsTrue, RootDirectoryMaxLength);
            }

            selected = Im.Item.Active;
            using var style = ImStyleDouble.ItemSpacing.Push(new Vector2(Im.Style.GlobalScale * 3, 0));
            Im.Line.Same();
            DrawDirectoryPickerButton();
            style.Pop();
            Im.Line.Same();

            const string tt = "This is where Penumbra will store your extracted mod files.\n"
              + "TTMP files are not copied, just extracted.\n"
              + "This directory needs to be accessible and you need write access here.\n"
              + "It is recommended that this directory is placed on a fast hard drive, preferably an SSD.\n"
              + "It should also be placed near the root of a logical drive - the shorter the total path to this folder, the better.\n"
              + "Definitely do not place it in your Dalamud directory or any sub-directory thereof.";
            ImGuiComponents.HelpMarker(tt);
            _tutorial.OpenTutorial(BasicTutorialSteps.GeneralTooltips);
            Im.Line.Same();
            Im.Text("Root Directory"u8);
            ImGuiUtil.HoverTooltip(tt);
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.ModDirectory);
        Im.Line.Same();
        var pos = Im.Cursor.X;
        Im.Line.New();

        if (_config.ModDirectory != _newModDirectory
         && _newModDirectory.Length is not 0
         && DrawPressEnterWarning(_newModDirectory, _config.ModDirectory, pos, save, selected))
            _modManager.DiscoverMods(_newModDirectory, out _newModDirectory);
    }

    /// <summary> Draw the Open Directory and Rediscovery buttons.</summary>
    private void DrawDirectoryButtons()
    {
        UiHelpers.DrawOpenDirectoryButton(0, _modManager.BasePath, _modManager.Valid);
        Im.Line.Same();
        var tt = _modManager.Valid
            ? "Force Penumbra to completely re-scan your root directory as if it was restarted."
            : "The currently selected folder is not valid. Please select a different folder.";
        if (ImGuiUtil.DrawDisabledButton("Rediscover Mods", Vector2.Zero, tt, !_modManager.Valid))
            _modManager.DiscoverMods();
    }

    /// <summary> Draw the Enable Mods Checkbox.</summary>
    private void DrawEnabledBox()
    {
        var enabled = _config.EnableMods;
        if (Im.Checkbox("Enable Mods"u8, ref enabled))
            _penumbra.SetEnabled(enabled);

        _tutorial.OpenTutorial(BasicTutorialSteps.EnableMods);
    }

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

        ImGuiUtil.LabeledHelpMarker("Upper Limit for Single-Selection Group Radio Buttons",
            "All Single-Selection Groups with more options than specified here will be displayed as Combo-Boxes at the top.\n"
          + "All other Single-Selection Groups will be displayed as a set of Radio-Buttons.");
    }

    /// <summary> Draw a selection for the minimum number of options after which a group is drawn as collapsible. </summary>
    private void DrawCollapsibleGroupMin()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##CollapsibleGroupMin"u8, _config.OptionGroupCollapsibleMin, out var newValue, 2, null, 0.01f,
                SliderFlags.AlwaysClamp))
        {
            _config.OptionGroupCollapsibleMin = newValue;
            _config.Save();
        }

        ImGuiUtil.LabeledHelpMarker("Collapsible Option Group Limit",
            "Lower Limit for option groups displaying the Collapse/Expand button at the top.");
    }


    /// <summary> Draw the window hiding state checkboxes.  </summary>
    private void DrawHidingSettings()
    {
        Checkbox("Open Config Window at Game Start", "Whether the Penumbra main window should be open or closed after launching the game.",
            _config.OpenWindowAtStart,               v => _config.OpenWindowAtStart = v);

        Checkbox("Hide Config Window when UI is Hidden",
            "Hide the Penumbra main window when you manually hide the in-game user interface.", _config.HideUiWhenUiHidden,
            v =>
            {
                _config.HideUiWhenUiHidden                   = v;
                _pluginInterface.UiBuilder.DisableUserUiHide = !v;
            });
        Checkbox("Hide Config Window when in Cutscenes",
            "Hide the Penumbra main window when you are currently watching a cutscene.", _config.HideUiInCutscenes,
            v =>
            {
                _config.HideUiInCutscenes                        = v;
                _pluginInterface.UiBuilder.DisableCutsceneUiHide = !v;
            });
        Checkbox("Hide Config Window when in GPose",
            "Hide the Penumbra main window when you are currently in GPose mode.", _config.HideUiInGPose,
            v =>
            {
                _config.HideUiInGPose                         = v;
                _pluginInterface.UiBuilder.DisableGposeUiHide = !v;
            });
    }

    /// <summary> Draw all settings that do not fit into other categories. </summary>
    private void DrawMiscSettings()
    {
        Checkbox("Automatically Select Character-Associated Collection",
            "On every login, automatically select the collection associated with the current character as the current collection for editing.",
            _config.AutoSelectCollection, _autoSelector.SetAutomaticSelection);
        Checkbox("Print Chat Command Success Messages to Chat",
            "Chat Commands usually print messages on failure but also on success to confirm your action. You can disable this here.",
            _config.PrintSuccessfulCommandsToChat, v => _config.PrintSuccessfulCommandsToChat = v);
        Checkbox("Hide Redraw Bar in Mod Panel", "Hides the lower redraw buttons in the mod panel in your Mods tab.",
            _config.HideRedrawBar,               v => _config.HideRedrawBar = v);
        Checkbox("Hide Changed Item Filters", "Hides the category filter line in the Changed Items tab and the Changed Items mod panel.",
            _config.HideChangedItemFilters,   v =>
            {
                _config.HideChangedItemFilters = v;
                if (v)
                {
                    _config.Ephemeral.ChangedItemFilter = ChangedItemFlagExtensions.AllFlags;
                    _config.Ephemeral.Save();
                }
            });

        ChangedItemModeExtensions.DrawCombo("##ChangedItemMode"u8, _config.ChangedItemDisplay, UiHelpers.InputTextWidth.X, v =>
        {
            _config.ChangedItemDisplay = v;
            _config.Save();
        });
        ImUtf8.LabeledHelpMarker("Mod Changed Item Display"u8,
            "Configure how to display the changed items of a single mod in the mods info panel."u8);

        Checkbox("Omit Machinist Offhands in Changed Items",
            "Omits all Aetherotransformers (machinist offhands) in the changed items tabs because any change on them changes all of them at the moment.\n\n"
          + "Changing this triggers a rediscovery of your mods so all changed items can be updated.",
            _config.HideMachinistOffhandFromChangedItems, v =>
            {
                _config.HideMachinistOffhandFromChangedItems = v;
                _modManager.DiscoverMods();
            });
        Checkbox("Hide Priority Numbers in Mod Selector",
            "Hides the bracketed non-zero priority numbers displayed in the mod selector when there is enough space for them.",
            _config.HidePrioritiesInSelector, v => _config.HidePrioritiesInSelector = v);
        DrawSingleSelectRadioMax();
        DrawCollapsibleGroupMin();
    }

    /// <summary> Draw all settings pertaining to actor identification for collections. </summary>
    private void DrawIdentificationSettings()
    {
        Checkbox("Use Interface Collection for other Plugin UIs",
            "Use the collection assigned to your interface for other plugins requesting UI-textures and icons through Dalamud.",
            _dalamudSubstitutionProvider.Enabled, _dalamudSubstitutionProvider.Set);
        Checkbox($"Use {"Assigned Collections"} in Lobby",
            "If this is disabled, no mods are applied to characters in the lobby or at the aesthetician.",
            _config.ShowModsInLobby, v => _config.ShowModsInLobby = v);
        Checkbox($"Use {"Assigned Collections"} in Character Window",
            "Use the individual collection for your characters name or the Your Character collection in your main character window, if it is set.",
            _config.UseCharacterCollectionInMainWindow, v => _config.UseCharacterCollectionInMainWindow = v);
        Checkbox($"Use {"Assigned Collections"} in Adventurer Cards",
            "Use the appropriate individual collection for the adventurer card you are currently looking at, based on the adventurer's name.",
            _config.UseCharacterCollectionsInCards, v => _config.UseCharacterCollectionsInCards = v);
        Checkbox($"Use {"Assigned Collections"} in Try-On Window",
            "Use the individual collection for your character's name in your try-on, dye preview or glamour plate window, if it is set.",
            _config.UseCharacterCollectionInTryOn, v => _config.UseCharacterCollectionInTryOn = v);
        Checkbox("Use No Mods in Inspect Windows", "Use the empty collection for characters you are inspecting, regardless of the character.\n"
          + "Takes precedence before the next option.", _config.UseNoModsInInspect, v => _config.UseNoModsInInspect = v);
        Checkbox($"Use {"Assigned Collections"} in Inspect Windows",
            "Use the appropriate individual collection for the character you are currently inspecting, based on their name.",
            _config.UseCharacterCollectionInInspect, v => _config.UseCharacterCollectionInInspect = v);
        Checkbox($"Use {"Assigned Collections"} based on Ownership",
            "Use the owner's name to determine the appropriate individual collection for mounts, companions, accessories and combat pets.",
            _config.UseOwnerNameForCharacterCollection, v => _config.UseOwnerNameForCharacterCollection = v);
    }

    /// <summary> Different supported sort modes as a combo. </summary>
    private void DrawFolderSortType()
    {
        var sortMode = _config.SortMode;
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        using (var combo = ImUtf8.Combo("##sortMode", sortMode.Name))
        {
            if (combo)
                foreach (var val in ISortMode.Valid.Values)
                {
                    if (Im.Selectable(val.Name, val.Equals(sortMode)) && !val.Equals(sortMode))
                    {
                        _config.SortMode = val;
                        _selector.SetFilterDirty();
                        _config.Save();
                    }

                    Im.Tooltip.OnHover(val.Description);
                }
        }

        ImGuiUtil.LabeledHelpMarker("Sort Mode", "Choose the sort mode for the mod selector in the mods tab.");
    }

    private void DrawRenameSettings()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        using (var combo = Im.Combo.Begin("##renameSettings"u8, _config.ShowRename.GetData().Name))
        {
            if (combo)
                foreach (var value in Enum.GetValues<RenameField>())
                {
                    var (name, desc) = value.GetData();
                    if (Im.Selectable(name, _config.ShowRename == value))
                    {
                        _config.ShowRename = value;
                        _selector.SetRenameSearchPath(value);
                        _config.Save();
                    }

                    ImGuiUtil.HoverTooltip(desc);
                }
        }

        Im.Line.Same();
        const string tt =
            "Select which of the two renaming input fields are visible when opening the right-click context menu of a mod in the mod selector.";
        ImGuiComponents.HelpMarker(tt);
        Im.Line.Same();
        Im.Text("Rename Fields in Mod Context Menu"u8);
        ImGuiUtil.HoverTooltip(tt);
    }

    /// <summary> Draw all settings pertaining to the mod selector. </summary>
    private void DrawModSelectorSettings()
    {
        DrawFolderSortType();
        DrawRenameSettings();
        Checkbox("Open Folders by Default", "Whether to start with all folders collapsed or expanded in the mod selector.",
            _config.OpenFoldersByDefault,   v =>
            {
                _config.OpenFoldersByDefault = v;
                _selector.SetFilterDirty();
            });

        KeySelector.DoubleModifier("Mod Deletion Modifier"u8,
            "A modifier you need to hold while clicking the Delete Mod button for it to take effect."u8, UiHelpers.InputTextWidth.X,
            _config.DeleteModModifier,
            v =>
            {
                _config.DeleteModModifier = v;
                _config.Save();
            });
        KeySelector.DoubleModifier("Incognito Modifier"u8,
            "A modifier you need to hold while clicking the Incognito or Temporary Settings Mode button for it to take effect."u8,
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
        Checkbox("Use Temporary Settings Per Default",
            "When you make any changes to your collection, apply them as temporary changes first and require a click to 'turn permanent' if you want to keep them.",
            _config.DefaultTemporaryMode, v => _config.DefaultTemporaryMode = v);
        Checkbox("Replace Non-Standard Symbols On Import",
            "Replace all non-ASCII symbols in mod and option names with underscores when importing mods.", _config.ReplaceNonAsciiOnImport,
            v => _config.ReplaceNonAsciiOnImport = v);
        Checkbox("Always Open Import at Default Directory",
            "Open the import window at the location specified here every time, forgetting your previous path.",
            _config.AlwaysOpenDefaultImport, v => _config.AlwaysOpenDefaultImport = v);
        Checkbox("Handle PCP Files",
            "When encountering specific mods, usually but not necessarily denoted by a .pcp file ending, Penumbra will automatically try to create an associated collection and assign it to a specific character for this mod package. This can turn this behaviour off if unwanted.",
            !_config.PcpSettings.DisableHandling, v => _config.PcpSettings.DisableHandling = !v);

        var active = _config.DeleteModModifier.IsActive();
        Im.Line.Same();
        if (ImUtf8.ButtonEx("Delete all PCP Mods"u8, "Deletes all mods tagged with 'PCP' from the mod list."u8, disabled: !active))
            _pcpService.CleanPcpMods();
        if (!active)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {_config.DeleteModModifier} while clicking.");

        Im.Line.Same();
        if (ImUtf8.ButtonEx("Delete all PCP Collections"u8, "Deletes all collections whose name starts with 'PCP/' from the collection list."u8,
                disabled: !active))
            _pcpService.CleanPcpCollections();
        if (!active)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {_config.DeleteModModifier} while clicking.");

        Checkbox("Allow Other Plugins Access to PCP Handling",
            "When creating or importing PCP files, other plugins can add and interpret their own data to the character.json file.",
            _config.PcpSettings.AllowIpc, v => _config.PcpSettings.AllowIpc = v);

        Checkbox("Create PCP Collections",
            "When importing PCP files, create the associated collection.",
            _config.PcpSettings.CreateCollection, v => _config.PcpSettings.CreateCollection = v);

        Checkbox("Assign PCP Collections",
            "When importing PCP files and creating the associated collection, assign it to the associated character.",
            _config.PcpSettings.AssignCollection, v => _config.PcpSettings.AssignCollection = v);
        DrawDefaultModImportPath();
        DrawDefaultModAuthor();
        DrawDefaultModImportFolder();
        DrawPcpFolder();
        DrawPcpExtension();
        DrawDefaultModExportPath();
        Checkbox("Enable Directory Watcher",
            "Enables a File Watcher that automatically listens for Mod files that enter a specified directory, causing Penumbra to open a popup to import these mods.",
            _config.EnableDirectoryWatch, _fileWatcher.Toggle);
        Checkbox("Enable Fully Automatic Import",
            "Uses the File Watcher in order to skip the query popup and automatically import any new mods.",
            _config.EnableAutomaticModImport, v => _config.EnableAutomaticModImport = v);
        DrawFileWatcherPath();
    }


    /// <summary> Draw input for the default import path for a mod. </summary>
    private void DrawDefaultModImportPath()
    {
        var       spacing = new Vector2(Im.Style.GlobalScale * 3);
        using var style   = ImStyleDouble.ItemSpacing.Push(spacing);

        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButton3);
        if (ImEx.InputOnDeactivation.Text("##defaultModImport"u8, _config.DefaultModImportPath, out string newDirectory))
        {
            _config.DefaultModImportPath = newDirectory;
            _config.Save();
        }

        Im.Line.Same();
        if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.Folder.ToIconString()}##import", UiHelpers.IconButtonSize,
                "Select a directory via dialog.", false, true))
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

        style.Pop();
        ImGuiUtil.LabeledHelpMarker("Default Mod Import Directory",
            "Set the directory that gets opened when using the file picker to import mods for the first time.");
    }

    /// <summary> Draw input for the default export/backup path for mods. </summary>
    private void DrawDefaultModExportPath()
    {
        var       spacing = new Vector2(Im.Style.GlobalScale * 3);
        using var style   = ImStyleDouble.ItemSpacing.Push(spacing);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButton3);
        if (ImEx.InputOnDeactivation.Text("##defaultModExport"u8, _config.ExportDirectory, out string newDirectory))
            _modExportManager.UpdateExportDirectory(newDirectory);

        Im.Line.Same();
        if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.Folder.ToIconString()}##export", UiHelpers.IconButtonSize,
                "Select a directory via dialog.", false, true))
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

        style.Pop();
        ImGuiUtil.LabeledHelpMarker("Default Mod Export Directory",
            "Set the directory mods get saved to when using the export function or loaded from when reimporting backups.\n"
          + "Keep this empty to use the root directory.");
    }

    /// <summary> Draw input for the Automatic Mod import path. </summary>
    private void DrawFileWatcherPath()
    {
        using var id      = Im.Id.Push("fw"u8);
        var       spacing = new Vector2(Im.Style.GlobalScale * 3);
        using var style   = ImStyleDouble.ItemSpacing.Push(spacing);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButton3);
        if (ImEx.InputOnDeactivation.Text("##path"u8, _config.WatchDirectory, out string newDirectory, maxLength: 256))
            _fileWatcher.UpdateDirectory(newDirectory);

        Im.Line.Same();
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

        style.Pop();
        ImGuiUtil.LabeledHelpMarker("Automatic Import Director",
            "Choose the Directory the File Watcher listens to.");
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

        ImGuiUtil.LabeledHelpMarker("Default Mod Author", "Set the default author stored for newly created mods.");
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

        ImGuiUtil.LabeledHelpMarker("Default Mod Import Organizational Folder",
            "Set the default Penumbra mod folder to place newly imported mods into.\nLeave blank to import into Root.");
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

        ImGuiUtil.LabeledHelpMarker("Default PCP Organizational Folder",
            "The folder any penumbra character packs are moved to on import.\nLeave blank to import into Root.");
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

        ImGuiUtil.LabeledHelpMarker("PCP Extension",
            "The extension used when exporting PCP files. Should generally be either \".pcp\" or \".pmp\".");
    }


    /// <summary> Draw all settings pertaining to advanced editing of mods. </summary>
    private void DrawModEditorSettings()
    {
        Checkbox("Advanced Editing: Edit Raw Tile UV Transforms",
            "Edit the raw matrix components of tile UV transforms, instead of having them decomposed into scale, rotation and shear.",
            _config.EditRawTileTransforms, v => _config.EditRawTileTransforms = v);

        Checkbox("Advanced Editing: Always Highlight Color Row Pair when Hovering Selection Button",
            "Make the whole color row pair selection button highlight the pair in game, instead of just the crosshair, even without holding Ctrl.",
            _config.WholePairSelectorAlwaysHighlights, v => _config.WholePairSelectorAlwaysHighlights = v);
    }

    #endregion

    /// <summary> Draw the entire Color subsection. </summary>
    private void DrawColorSettings()
    {
        if (!Im.Tree.Header("Colors"u8))
            return;

        foreach (var color in Enum.GetValues<ColorId>())
        {
            var (defaultColor, name, description) = color.Data();
            var currentColor = _config.Colors.GetValueOrDefault(color, defaultColor);
            if (Widget.ColorPicker(name, description, currentColor, c => _config.Colors[color] = c, defaultColor))
                _config.Save();
        }

        Im.Line.New();
    }

    #region Advanced Settings

    /// <summary> Draw all advanced settings. </summary>
    private void DrawAdvancedSettings()
    {
        var header = Im.Tree.Header("Advanced"u8);

        if (!header)
            return;

        DrawCrashHandler();
        DrawMinimumDimensionConfig();
        DrawHdrRenderTargets();
        Checkbox("Auto Deduplicate on Import",
            "Automatically deduplicate mod files on import. This will make mod file sizes smaller, but deletes (binary identical) files.",
            _config.AutoDeduplicateOnImport, v => _config.AutoDeduplicateOnImport = v);
        Checkbox("Auto Reduplicate UI Files on PMP Import",
            "Automatically reduplicate and normalize UI-specific files on import from PMP files. This is STRONGLY recommended because deduplicated UI files crash the game.",
            _config.AutoReduplicateUiOnImport, v => _config.AutoReduplicateUiOnImport = v);
        DrawCompressionBox();
        Checkbox("Keep Default Metadata Changes on Import",
            "Normally, metadata changes that equal their default values, which are sometimes exported by TexTools, are discarded. "
          + "Toggle this to keep them, for example if an option in a mod is supposed to disable a metadata change from a prior option.",
            _config.KeepDefaultMetaChanges, v => _config.KeepDefaultMetaChanges = v);
        Checkbox("Enable Custom Shape and Attribute Support",
            "Penumbra will allow for custom shape keys and attributes for modded models to be considered and combined.",
            _config.EnableCustomShapes, _attributeHook.SetState);
        DrawWaitForPluginsReflection();
        DrawEnableHttpApiBox();
        DrawEnableDebugModeBox();
        Im.Separator();
        DrawReloadResourceButton();
        DrawReloadFontsButton();
        Im.Separator();
        DrawCleanupButtons();
        Im.Line.New();
    }

    private void DrawCrashHandler()
    {
        Checkbox("Enable Penumbra Crash Logging (Experimental)",
            "Enables Penumbra to launch a secondary process that records some game activity which may or may not help diagnosing Penumbra-related game crashes.",
            _config.UseCrashHandler ?? false,
            v =>
            {
                if (v)
                    _crashService.Enable();
                else
                    _crashService.Disable();
            });
    }

    private void DrawCompressionBox()
    {
        if (!_compactor.CanCompact)
            return;

        Checkbox("Use Filesystem Compression",
            "Use Windows functionality to transparently reduce storage size of mod files on your computer. This might cost performance, but seems to generally be beneficial to performance by shifting more responsibility to the underused CPU and away from the overused hard drives.",
            _config.UseFileSystemCompression,
            v =>
            {
                _config.UseFileSystemCompression = v;
                _compactor.Enabled               = v;
            });
        Im.Line.Same();
        if (ImGuiUtil.DrawDisabledButton("Compress Existing Files", Vector2.Zero,
                "Try to compress all files in your root directory. This will take a while.",
                _compactor.MassCompactRunning || !_modManager.Valid))
            _compactor.StartMassCompact(_modManager.BasePath.EnumerateFiles("*.*", SearchOption.AllDirectories), CompressionAlgorithm.Xpress8K,
                true);

        Im.Line.Same();
        if (ImGuiUtil.DrawDisabledButton("Decompress Existing Files", Vector2.Zero,
                "Try to decompress all files in your root directory. This will take a while.",
                _compactor.MassCompactRunning || !_modManager.Valid))
            _compactor.StartMassCompact(_modManager.BasePath.EnumerateFiles("*.*", SearchOption.AllDirectories), CompressionAlgorithm.None,
                true);

        if (_compactor.MassCompactRunning)
        {
            Im.ProgressBar((float)_compactor.CurrentIndex / _compactor.TotalFiles,
                new Vector2(Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X - UiHelpers.IconButtonSize.X,
                    Im.Style.FrameHeight),
                _compactor.CurrentFile?.FullName[(_modManager.BasePath.FullName.Length + 1)..] ?? "Gathering Files...");
            Im.Line.Same();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Ban.ToIconString(), UiHelpers.IconButtonSize, "Cancel the mass action.",
                    !_compactor.MassCompactRunning, true))
                _compactor.CancelMassCompact();
        }
        else
        {
            Im.FrameDummy();
        }
    }

    /// <summary> Draw two integral inputs for minimum dimensions of this window. </summary>
    private void DrawMinimumDimensionConfig()
    {
        var warning = _config.MinimumSize.X < Configuration.Constants.MinimumSizeX
            ? _config.MinimumSize.Y < Configuration.Constants.MinimumSizeY
                ? "Size is smaller than default: This may look undesirable."u8
                : "Width is smaller than default: This may look undesirable."u8
            : _config.MinimumSize.Y < Configuration.Constants.MinimumSizeY
                ? "Height is smaller than default: This may look undesirable."u8
                : StringU8.Empty;
        var buttonWidth = UiHelpers.InputTextWidth.X / 2.5f;
        Im.Item.SetNextWidth(buttonWidth);
        if (ImEx.InputOnDeactivation.Drag("##xMinSize"u8, (int)_config.MinimumSize.X, out var newX, 500, 1500, 0.1f))
        {
            _config.MinimumSize.X = newX;
            _config.Save();
        }
        Im.Line.Same();
        Im.Item.SetNextWidth(buttonWidth);
        if (ImEx.InputOnDeactivation.Drag("##yMinSize"u8, (int)_config.MinimumSize.Y, out var newY, 300, 1500, 0.1f))
        {
            _config.MinimumSize.Y = newY;
            _config.Save();
        }

        Im.Line.Same();
        if (ImGuiUtil.DrawDisabledButton("Reset##resetMinSize", new Vector2(buttonWidth / 2 - Im.Style.ItemSpacing.X * 2, 0),
                $"Reset minimum dimensions to ({Configuration.Constants.MinimumSizeX}, {Configuration.Constants.MinimumSizeY}).",
                _config.MinimumSize is { X: Configuration.Constants.MinimumSizeX, Y: Configuration.Constants.MinimumSizeY }))
        {
            _config.MinimumSize = new Vector2(Configuration.Constants.MinimumSizeX, Configuration.Constants.MinimumSizeY);
            _config.Save();
        }

        ImGuiUtil.LabeledHelpMarker("Minimum Window Dimensions",
            "Set the minimum dimensions for resizing this window. Reducing these dimensions may cause the window to look bad or more confusing and is not recommended.");

        if (warning.Length > 0)
            ImEx.TextFramed(warning, UiHelpers.InputTextWidth, Colors.PressEnterWarningBg);
        else
            Im.Line.New();
    }

    private void DrawHdrRenderTargets()
    {
        Im.Item.SetNextWidth(ImUtf8.CalcTextSize("M"u8).X * 5.0f + Im.Style.FrameHeight);
        using (var combo = ImUtf8.Combo("##hdrRenderTarget"u8, _config.HdrRenderTargets ? "HDR"u8 : "SDR"u8))
        {
            if (combo)
            {
                if (ImUtf8.Selectable("HDR"u8, _config.HdrRenderTargets) && !_config.HdrRenderTargets)
                {
                    _config.HdrRenderTargets = true;
                    _config.Save();
                }

                if (ImUtf8.Selectable("SDR"u8, !_config.HdrRenderTargets) && _config.HdrRenderTargets)
                {
                    _config.HdrRenderTargets = false;
                    _config.Save();
                }
            }
        }

        Im.Line.Same();
        ImUtf8.LabeledHelpMarker("Diffuse Dynamic Range"u8,
            "Set the dynamic range that can be used for diffuse colors in materials without causing visual artifacts.\n"u8
          + "Changing this setting requires a game restart. It also only works if Wait for Plugins on Startup is enabled."u8);
    }

    /// <summary> Draw a checkbox for the HTTP API that creates and destroys the web server when toggled. </summary>
    private void DrawEnableHttpApiBox()
    {
        var http = _config.EnableHttpApi;
        if (Im.Checkbox("##http"u8, ref http))
        {
            if (http)
                _httpApi.CreateWebServer();
            else
                _httpApi.ShutdownWebServer();

            _config.EnableHttpApi = http;
            _config.Save();
        }

        Im.Line.Same();
        ImGuiUtil.LabeledHelpMarker("Enable HTTP API",
            "Enables other applications, e.g. Anamnesis, to use some Penumbra functions, like requesting redraws.");
    }

    /// <summary> Draw a checkbox to toggle Debug mode. </summary>
    private void DrawEnableDebugModeBox()
    {
        var tmp = _config.DebugMode;
        if (Im.Checkbox("##debugMode"u8, ref tmp) && tmp != _config.DebugMode)
        {
            _config.DebugMode = tmp;
            _config.Save();
        }

        Im.Line.Same();
        ImGuiUtil.LabeledHelpMarker("Enable Debug Mode",
            "[DEBUG] Enable the Debug Tab and Resource Manager Tab as well as some additional data collection. Also open the config window on plugin load.");
    }

    /// <summary> Draw a button that reloads resident resources. </summary>
    private void DrawReloadResourceButton()
    {
        if (ImGuiUtil.DrawDisabledButton("Reload Resident Resources", Vector2.Zero,
                "Reload some specific files that the game keeps in memory at all times.\nYou usually should not need to do this.",
                !_characterUtility.Ready))
            _residentResources.Reload();
    }

    /// <summary> Draw a button that reloads fonts. </summary>
    private void DrawReloadFontsButton()
    {
        if (ImGuiUtil.DrawDisabledButton("Reload Fonts", Vector2.Zero, "Force the game to reload its font files.", !_fontReloader.Valid))
            _fontReloader.Reload();
    }

    private void DrawCleanupButtons()
    {
        var enabled = _config.DeleteModModifier.IsActive();
        if (_cleanupService.Progress is not 0.0 and not 1.0)
        {
            ImUtf8.ProgressBar((float)_cleanupService.Progress, new Vector2(200 * Im.Style.GlobalScale, Im.Style.FrameHeight),
                $"{_cleanupService.Progress * 100}%");
            Im.Line.Same();
            if (ImUtf8.Button("Cancel##FileCleanup"u8))
                _cleanupService.Cancel();
        }
        else
        {
            Im.Line.New();
        }

        if (ImUtf8.ButtonEx("Clear Unused Local Mod Data Files"u8,
                "Delete all local mod data files that do not correspond to currently installed mods."u8, default,
                !enabled || _cleanupService.IsRunning))
            _cleanupService.CleanUnusedLocalData();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {_config.DeleteModModifier} while clicking to delete files.");

        if (ImUtf8.ButtonEx("Clear Backup Files"u8,
                "Delete all backups of .json configuration files in your configuration folder and all backups of mod group files in your mod directory."u8,
                default, !enabled || _cleanupService.IsRunning))
            _cleanupService.CleanBackupFiles();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {_config.DeleteModModifier} while clicking to delete files.");

        if (ImUtf8.ButtonEx("Clear All Unused Settings"u8,
                "Remove all mod settings in all of your collections that do not correspond to currently installed mods."u8, default,
                !enabled || _cleanupService.IsRunning))
            _cleanupService.CleanupAllUnusedSettings();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {_config.DeleteModModifier} while clicking to remove settings.");
    }

    /// <summary> Draw a checkbox that toggles the dalamud setting to wait for plugins on open. </summary>
    private void DrawWaitForPluginsReflection()
    {
        if (!_dalamudConfig.GetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, out bool value))
        {
            using var disabled = Im.Disabled();
            Checkbox("Wait for Plugins on Startup (Disabled, can not access Dalamud Configuration)", string.Empty, false, _ => { });
        }
        else
        {
            Checkbox("Wait for Plugins on Startup",
                "Some mods need to change files that are loaded once when the game starts and never afterwards.\n"
              + "This can cause issues with Penumbra loading after the files are already loaded.\n"
              + "This setting causes the game to wait until certain plugins have finished loading, making those mods work (in the base collection).\n\n"
              + "This changes a setting in the Dalamud Configuration found at /xlsettings -> General.",
                value,
                v => _dalamudConfig.SetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, v, "doWaitForPluginsOnStartup"));
        }
    }

    #endregion

    /// <summary> Draw the support button group on the right-hand side of the window. </summary>
    private void DrawSupportButtons()
    {
        var width = Im.Font.CalculateSize(UiHelpers.SupportInfoButtonText).X + Im.Style.FramePadding.X * 2;
        var xPos  = Im.Window.Width - width;
        // Respect the scroll bar width.
        if (Im.Scroll.MaximumY> 0)
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

        var tagIdx = _sharedTags.Draw("Predefined Tags: ",
            "Predefined tags that can be added or removed from mods with a single click.", _predefinedTagManager,
            out var editedTag);

        if (tagIdx >= 0)
            _predefinedTagManager.ChangeSharedTag(tagIdx, editedTag);
    }
}
