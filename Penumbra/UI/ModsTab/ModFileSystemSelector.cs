using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.FileSystem.Selector;
using OtterGui.Raii;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Import;
using Penumbra.Import.Structs;
using Penumbra.Mods;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.Util;

namespace Penumbra.UI.ModsTab;

public sealed partial class ModFileSystemSelector : FileSystemSelector<Mod, ModFileSystemSelector.ModState>
{
    private readonly CommunicatorService   _communicator;
    private readonly ChatService           _chat;
    private readonly Configuration         _config;
    private readonly FileDialogService     _fileDialog;
    private readonly ModManager           _modManager;
    private readonly CollectionManager _collectionManager;
    private readonly TutorialService       _tutorial;
    private readonly ModEditor             _modEditor;

    private TexToolsImporter? _import;
    public  ModSettings       SelectedSettings          { get; private set; } = ModSettings.Empty;
    public  ModCollection     SelectedSettingCollection { get; private set; } = ModCollection.Empty;

    private uint _infoPopupId = 0;

    public ModFileSystemSelector(CommunicatorService communicator, ModFileSystem fileSystem, ModManager modManager,
        CollectionManager collectionManager, Configuration config, TutorialService tutorial, FileDialogService fileDialog, ChatService chat,
        ModEditor modEditor)
        : base(fileSystem, DalamudServices.KeyState, HandleException)
    {
        _communicator      = communicator;
        _modManager        = modManager;
        _collectionManager = collectionManager;
        _config            = config;
        _tutorial          = tutorial;
        _fileDialog        = fileDialog;
        _chat              = chat;
        _modEditor         = modEditor;

        // @formatter:off
        SubscribeRightClickFolder(EnableDescendants, 10);
        SubscribeRightClickFolder(DisableDescendants, 10);
        SubscribeRightClickFolder(InheritDescendants, 15);
        SubscribeRightClickFolder(OwnDescendants, 15);
        SubscribeRightClickFolder(SetDefaultImportFolder, 100);
        SubscribeRightClickFolder(f => SetQuickMove(f, 0, _config.QuickMoveFolder1, s => { _config.QuickMoveFolder1 = s; _config.Save(); }), 110);
        SubscribeRightClickFolder(f => SetQuickMove(f, 1, _config.QuickMoveFolder2, s => { _config.QuickMoveFolder2 = s; _config.Save(); }), 120);
        SubscribeRightClickFolder(f => SetQuickMove(f, 2, _config.QuickMoveFolder3, s => { _config.QuickMoveFolder3 = s; _config.Save(); }), 130);
        SubscribeRightClickLeaf(ToggleLeafFavorite);
        SubscribeRightClickLeaf(l => QuickMove(l, _config.QuickMoveFolder1, _config.QuickMoveFolder2, _config.QuickMoveFolder3));
        SubscribeRightClickMain(ClearDefaultImportFolder, 100);
        SubscribeRightClickMain(() => ClearQuickMove(0, _config.QuickMoveFolder1, () => {_config.QuickMoveFolder1 = string.Empty; _config.Save();}), 110);
        SubscribeRightClickMain(() => ClearQuickMove(1, _config.QuickMoveFolder2, () => {_config.QuickMoveFolder2 = string.Empty; _config.Save();}), 120);
        SubscribeRightClickMain(() => ClearQuickMove(2, _config.QuickMoveFolder3, () => {_config.QuickMoveFolder3 = string.Empty; _config.Save();}), 130);
        AddButton(AddNewModButton,    0);
        AddButton(AddImportModButton, 1);
        AddButton(AddHelpButton,      2);
        AddButton(DeleteModButton,    1000);
        // @formatter:on
        SetFilterTooltip();

        SelectionChanged                              += OnSelectionChange;
        _communicator.CollectionChange.Event          += OnCollectionChange;
        _collectionManager.Current.ModSettingChanged  += OnSettingChange;
        _collectionManager.Current.InheritanceChanged += OnInheritanceChange;
        _communicator.ModDataChanged.Event            += OnModDataChange;
        _modManager.ModDiscoveryStarted               += StoreCurrentSelection;
        _modManager.ModDiscoveryFinished              += RestoreLastSelection;
        OnCollectionChange(CollectionType.Current, null, _collectionManager.Current, "");
    }

    public override void Dispose()
    {
        base.Dispose();
        _modManager.ModDiscoveryStarted               -= StoreCurrentSelection;
        _modManager.ModDiscoveryFinished              -= RestoreLastSelection;
        _communicator.ModDataChanged.Event            -= OnModDataChange;
        _collectionManager.Current.ModSettingChanged  -= OnSettingChange;
        _collectionManager.Current.InheritanceChanged -= OnInheritanceChange;
        _communicator.CollectionChange.Event          -= OnCollectionChange;
        _import?.Dispose();
        _import = null;
    }

    public new ModFileSystem.Leaf? SelectedLeaf
        => base.SelectedLeaf;

    #region Interface

    // Customization points.
    public override ISortMode<Mod> SortMode
        => _config.SortMode;

    protected override uint ExpandedFolderColor
        => ColorId.FolderExpanded.Value(_config);

    protected override uint CollapsedFolderColor
        => ColorId.FolderCollapsed.Value(_config);

    protected override uint FolderLineColor
        => ColorId.FolderLine.Value(_config);

    protected override bool FoldersDefaultOpen
        => _config.OpenFoldersByDefault;

    protected override void DrawPopups()
    {
        DrawHelpPopup();
        DrawInfoPopup();

        if (ImGuiUtil.OpenNameField("Create New Mod", ref _newModName))
            try
            {
                var newDir = Mod.Creator.CreateModFolder(Penumbra.ModManager.BasePath, _newModName);
                _modManager.DataEditor.CreateMeta(newDir, _newModName, Penumbra.Config.DefaultModAuthor, string.Empty, "1.0", string.Empty);
                Mod.Creator.CreateDefaultFiles(newDir);
                _modManager.AddMod(newDir);
                _newModName = string.Empty;
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not create directory for new Mod {_newModName}:\n{e}");
            }

        while (_modsToAdd.TryDequeue(out var dir))
        {
            _modManager.AddMod(dir);
            var mod = _modManager.LastOrDefault();
            if (mod == null)
                continue;

            MoveModToDefaultDirectory(mod);
            SelectByValue(mod);
        }
    }

    protected override void DrawLeafName(FileSystem<Mod>.Leaf leaf, in ModState state, bool selected)
    {
        var flags = selected ? ImGuiTreeNodeFlags.Selected | LeafFlags : LeafFlags;
        using var c = ImRaii.PushColor(ImGuiCol.Text, state.Color.Value(_config))
            .Push(ImGuiCol.HeaderHovered, 0x4000FFFF, leaf.Value.Favorite);
        using var id = ImRaii.PushId(leaf.Value.Index);
        ImRaii.TreeNode(leaf.Value.Name, flags).Dispose();
    }


    // Add custom context menu items.
    private void EnableDescendants(ModFileSystem.Folder folder)
    {
        if (ImGui.MenuItem("Enable Descendants"))
            SetDescendants(folder, true);
    }

    private void DisableDescendants(ModFileSystem.Folder folder)
    {
        if (ImGui.MenuItem("Disable Descendants"))
            SetDescendants(folder, false);
    }

    private void InheritDescendants(ModFileSystem.Folder folder)
    {
        if (ImGui.MenuItem("Inherit Descendants"))
            SetDescendants(folder, true, true);
    }

    private void OwnDescendants(ModFileSystem.Folder folder)
    {
        if (ImGui.MenuItem("Stop Inheriting Descendants"))
            SetDescendants(folder, false, true);
    }

    private void ToggleLeafFavorite(FileSystem<Mod>.Leaf mod)
    {
        if (ImGui.MenuItem(mod.Value.Favorite ? "Remove Favorite" : "Mark as Favorite"))
            _modManager.DataEditor.ChangeModFavorite(mod.Value, !mod.Value.Favorite);
    }

    private void SetDefaultImportFolder(ModFileSystem.Folder folder)
    {
        if (!ImGui.MenuItem("Set As Default Import Folder"))
            return;

        var newName = folder.FullName();
        if (newName == _config.DefaultImportFolder)
            return;

        _config.DefaultImportFolder = newName;
        _config.Save();
    }

    private void ClearDefaultImportFolder()
    {
        if (!ImGui.MenuItem("Clear Default Import Folder") || _config.DefaultImportFolder.Length <= 0)
            return;

        _config.DefaultImportFolder = string.Empty;
        _config.Save();
    }

    private string _newModName = string.Empty;

    private void AddNewModButton(Vector2 size)
    {
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), size, "Create a new, empty mod of a given name.",
                !_modManager.Valid, true))
            ImGui.OpenPopup("Create New Mod");
    }

    /// <summary> Add an import mods button that opens a file selector. </summary>
    private void AddImportModButton(Vector2 size)
    {
        _infoPopupId = ImGui.GetID("Import Status");
        var button = ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileImport.ToIconString(), size,
            "Import one or multiple mods from Tex Tools Mod Pack Files or Penumbra Mod Pack Files.", !Penumbra.ModManager.Valid, true);
        _tutorial.OpenTutorial(BasicTutorialSteps.ModImport);
        if (!button)
            return;

        var modPath = !_config.AlwaysOpenDefaultImport ? null
            : _config.DefaultModImportPath.Length > 0  ? _config.DefaultModImportPath
            : _config.ModDirectory.Length > 0          ? _config.ModDirectory : null;

        _fileDialog.OpenFilePicker("Import Mod Pack",
            "Mod Packs{.ttmp,.ttmp2,.pmp},TexTools Mod Packs{.ttmp,.ttmp2},Penumbra Mod Packs{.pmp},Archives{.zip,.7z,.rar}", (s, f) =>
            {
                if (!s)
                    return;

                _import = new TexToolsImporter(_modManager.BasePath, f.Count, f.Select(file => new FileInfo(file)),
                    AddNewMod, _config, _modEditor, _modManager);
                ImGui.OpenPopup(_infoPopupId);
            }, 0, modPath, _config.AlwaysOpenDefaultImport);
    }

    /// <summary> Draw the progress information for import. </summary>
    private void DrawInfoPopup()
    {
        var display = ImGui.GetIO().DisplaySize;
        var height  = Math.Max(display.Y / 4, 15 * ImGui.GetFrameHeightWithSpacing());
        var width   = display.X / 8;
        var size    = new Vector2(width * 2, height);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, Vector2.One / 2);
        ImGui.SetNextWindowSize(size);
        var infoPopupId = ImGui.GetID("Import Status");
        using var popup = ImRaii.Popup("Import Status", ImGuiWindowFlags.Modal);
        if (_import == null || !popup.Success)
            return;

        using (var child = ImRaii.Child("##import", new Vector2(-1, size.Y - ImGui.GetFrameHeight() * 2)))
        {
            if (child)
                _import.DrawProgressInfo(new Vector2(-1, ImGui.GetFrameHeight()));
        }

        if ((_import.State != ImporterState.Done || !ImGui.Button("Close", -Vector2.UnitX))
         && (_import.State == ImporterState.Done || !_import.DrawCancelButton(-Vector2.UnitX)))
            return;

        _import?.Dispose();
        _import = null;
        ImGui.CloseCurrentPopup();
    }

    /// <summary> Mods need to be added thread-safely outside of iteration. </summary>
    private readonly ConcurrentQueue<DirectoryInfo> _modsToAdd = new();

    /// <summary>
    /// Clean up invalid directory if necessary.
    /// Add successfully extracted mods.
    /// </summary>
    private void AddNewMod(FileInfo file, DirectoryInfo? dir, Exception? error)
    {
        if (error != null)
        {
            if (dir != null && Directory.Exists(dir.FullName))
                try
                {
                    Directory.Delete(dir.FullName, true);
                }
                catch (Exception e)
                {
                    Penumbra.Log.Error($"Error cleaning up failed mod extraction of {file.FullName} to {dir.FullName}:\n{e}");
                }

            if (error is not OperationCanceledException)
                Penumbra.Log.Error($"Error extracting {file.FullName}, mod skipped:\n{error}");
        }
        else if (dir != null)
        {
            _modsToAdd.Enqueue(dir);
        }
    }

    private void DeleteModButton(Vector2 size)
    {
        var keys = _config.DeleteModModifier.IsActive();
        var tt = SelectedLeaf == null
            ? "No mod selected."
            : "Delete the currently selected mod entirely from your drive.\n"
          + "This can not be undone.";
        if (!keys)
            tt += $"\nHold {_config.DeleteModModifier} while clicking to delete the mod.";

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), size, tt, SelectedLeaf == null || !keys, true)
         && Selected != null)
            _modManager.DeleteMod(Selected.Index);
    }

    private void AddHelpButton(Vector2 size)
    {
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.QuestionCircle.ToIconString(), size, "Open extended help.", false, true))
            ImGui.OpenPopup("ExtendedHelp");

        _tutorial.OpenTutorial(BasicTutorialSteps.AdvancedHelp);
    }

    private void SetDescendants(ModFileSystem.Folder folder, bool enabled, bool inherit = false)
    {
        var mods = folder.GetAllDescendants(ISortMode<Mod>.Lexicographical).OfType<ModFileSystem.Leaf>().Select(l =>
        {
            // Any mod handled here should not stay new.
            _modManager.NewMods.Remove(l.Value);
            return l.Value;
        });

        if (inherit)
            _collectionManager.Current.SetMultipleModInheritances(mods, enabled);
        else
            _collectionManager.Current.SetMultipleModStates(mods, enabled);
    }

    /// <summary>
    /// If a default import folder is setup, try to move the given mod in there.
    /// If the folder does not exist, create it if possible.
    /// </summary>
    /// <param name="mod"></param>
    private void MoveModToDefaultDirectory(Mod mod)
    {
        if (_config.DefaultImportFolder.Length == 0)
            return;

        try
        {
            var leaf = FileSystem.Root.GetChildren(ISortMode<Mod>.Lexicographical)
                .FirstOrDefault(f => f is FileSystem<Mod>.Leaf l && l.Value == mod);
            if (leaf == null)
                throw new Exception("Mod was not found at root.");

            var folder = FileSystem.FindOrCreateAllFolders(Penumbra.Config.DefaultImportFolder);
            FileSystem.Move(leaf, folder);
        }
        catch (Exception e)
        {
            _chat.NotificationMessage(
                $"Could not move newly imported mod {mod.Name} to default import folder {_config.DefaultImportFolder}:\n{e}", "Warning",
                NotificationType.Warning);
        }
    }

    private void DrawHelpPopup()
    {
        ImGuiUtil.HelpPopup("ExtendedHelp", new Vector2(1000 * UiHelpers.Scale, 34.5f * ImGui.GetTextLineHeightWithSpacing()), () =>
        {
            ImGui.Dummy(Vector2.UnitY * ImGui.GetTextLineHeight());
            ImGui.TextUnformatted("Mod Management");
            ImGui.BulletText("You can create empty mods or import mods with the buttons in this row.");
            using var indent = ImRaii.PushIndent();
            ImGui.BulletText("Supported formats for import are: .ttmp, .ttmp2, .pmp.");
            ImGui.BulletText(
                "You can also support .zip, .7z or .rar archives, but only if they already contain Penumbra-styled mods with appropriate metadata.");
            indent.Pop(1);
            ImGui.BulletText("You can also create empty mod folders and delete mods.");
            ImGui.BulletText("For further editing of mods, select them and use the Edit Mod tab in the panel or the Advanced Editing popup.");
            ImGui.Dummy(Vector2.UnitY * ImGui.GetTextLineHeight());
            ImGui.TextUnformatted("Mod Selector");
            ImGui.BulletText("Select a mod to obtain more information or change settings.");
            ImGui.BulletText("Names are colored according to your config and their current state in the collection:");
            indent.Push();
            ImGuiUtil.BulletTextColored(ColorId.EnabledMod.Value(_config),           "enabled in the current collection.");
            ImGuiUtil.BulletTextColored(ColorId.DisabledMod.Value(_config),          "disabled in the current collection.");
            ImGuiUtil.BulletTextColored(ColorId.InheritedMod.Value(_config),         "enabled due to inheritance from another collection.");
            ImGuiUtil.BulletTextColored(ColorId.InheritedDisabledMod.Value(_config), "disabled due to inheritance from another collection.");
            ImGuiUtil.BulletTextColored(ColorId.UndefinedMod.Value(_config),         "unconfigured in all inherited collections.");
            ImGuiUtil.BulletTextColored(ColorId.NewMod.Value(_config),
                "newly imported during this session. Will go away when first enabling a mod or when Penumbra is reloaded.");
            ImGuiUtil.BulletTextColored(ColorId.HandledConflictMod.Value(_config),
                "enabled and conflicting with another enabled Mod, but on different priorities (i.e. the conflict is solved).");
            ImGuiUtil.BulletTextColored(ColorId.ConflictingMod.Value(_config),
                "enabled and conflicting with another enabled Mod on the same priority.");
            ImGuiUtil.BulletTextColored(ColorId.FolderExpanded.Value(_config),  "expanded mod folder.");
            ImGuiUtil.BulletTextColored(ColorId.FolderCollapsed.Value(_config), "collapsed mod folder");
            indent.Pop(1);
            ImGui.BulletText("Right-click a mod to enter its sort order, which is its name by default, possibly with a duplicate number.");
            indent.Push();
            ImGui.BulletText("A sort order differing from the mods name will not be displayed, it will just be used for ordering.");
            ImGui.BulletText(
                "If the sort order string contains Forward-Slashes ('/'), the preceding substring will be turned into folders automatically.");
            indent.Pop(1);
            ImGui.BulletText(
                "You can drag and drop mods and subfolders into existing folders. Dropping them onto mods is the same as dropping them onto the parent of the mod.");
            ImGui.BulletText("Right-clicking a folder opens a context menu.");
            ImGui.BulletText("Right-clicking empty space allows you to expand or collapse all folders at once.");
            ImGui.BulletText("Use the Filter Mods... input at the top to filter the list for mods whose name or path contain the text.");
            indent.Push();
            ImGui.BulletText("You can enter n:[string] to filter only for names, without path.");
            ImGui.BulletText("You can enter c:[string] to filter for Changed Items instead.");
            ImGui.BulletText("You can enter a:[string] to filter for Mod Authors instead.");
            indent.Pop(1);
            ImGui.BulletText("Use the expandable menu beside the input to filter for mods fulfilling specific criteria.");
        });
    }

    private static void HandleException(Exception e)
        => Penumbra.ChatService.NotificationMessage(e.Message, "Failure", NotificationType.Warning);

    #endregion

    #region Automatic cache update functions.

    private void OnSettingChange(ModSettingChange type, int modIdx, int oldValue, int groupIdx, bool inherited)
    {
        // TODO: maybe make more efficient
        SetFilterDirty();
        if (modIdx == Selected?.Index)
            OnSelectionChange(Selected, Selected, default);
    }

    private void OnModDataChange(ModDataChangeType type, Mod mod, string? oldName)
    {
        switch (type)
        {
            case ModDataChangeType.Name:
            case ModDataChangeType.Author:
            case ModDataChangeType.ModTags:
            case ModDataChangeType.LocalTags:
            case ModDataChangeType.Favorite:
                SetFilterDirty();
                break;
        }
    }

    private void OnInheritanceChange(bool _)
    {
        SetFilterDirty();
        OnSelectionChange(Selected, Selected, default);
    }

    private void OnCollectionChange(CollectionType collectionType, ModCollection? oldCollection, ModCollection? newCollection, string _)
    {
        if (collectionType != CollectionType.Current || oldCollection == newCollection)
            return;

        if (oldCollection != null)
        {
            oldCollection.ModSettingChanged  -= OnSettingChange;
            oldCollection.InheritanceChanged -= OnInheritanceChange;
        }

        if (newCollection != null)
        {
            newCollection.ModSettingChanged  += OnSettingChange;
            newCollection.InheritanceChanged += OnInheritanceChange;
        }

        SetFilterDirty();
        OnSelectionChange(Selected, Selected, default);
    }

    private void OnSelectionChange(Mod? _1, Mod? newSelection, in ModState _2)
    {
        if (newSelection == null)
        {
            SelectedSettings          = ModSettings.Empty;
            SelectedSettingCollection = ModCollection.Empty;
        }
        else
        {
            (var settings, SelectedSettingCollection) = _collectionManager.Current[newSelection.Index];
            SelectedSettings                          = settings ?? ModSettings.Empty;
        }
    }

    // Keep selections across rediscoveries if possible.
    private string _lastSelectedDirectory = string.Empty;

    private void StoreCurrentSelection()
    {
        _lastSelectedDirectory = Selected?.ModPath.FullName ?? string.Empty;
        ClearSelection();
    }

    private void RestoreLastSelection()
    {
        if (_lastSelectedDirectory.Length <= 0)
            return;

        var leaf = (ModFileSystem.Leaf?)FileSystem.Root.GetAllDescendants(ISortMode<Mod>.Lexicographical)
            .FirstOrDefault(l => l is ModFileSystem.Leaf m && m.Value.ModPath.FullName == _lastSelectedDirectory);
        Select(leaf);
        _lastSelectedDirectory = string.Empty;
    }

    #endregion

    #region Filters

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ModState
    {
        public ColorId Color;
    }

    private const StringComparison IgnoreCase   = StringComparison.OrdinalIgnoreCase;
    private       LowerString      _modFilter   = LowerString.Empty;
    private       int              _filterType  = -1;
    private       ModFilter        _stateFilter = ModFilterExtensions.UnfilteredStateMods;

    private void SetFilterTooltip()
    {
        FilterTooltip = "Filter mods for those where their full paths or names contain the given substring.\n"
          + "Enter c:[string] to filter for mods changing specific items.\n"
          + "Enter t:[string] to filter for mods set to specific tags.\n"
          + "Enter n:[string] to filter only for mod names and no paths.\n"
          + "Enter a:[string] to filter for mods by specific authors.";
    }

    /// <summary> Appropriately identify and set the string filter and its type. </summary>
    protected override bool ChangeFilter(string filterValue)
    {
        (_modFilter, _filterType) = filterValue.Length switch
        {
            0 => (LowerString.Empty, -1),
            > 1 when filterValue[1] == ':' =>
                filterValue[0] switch
                {
                    'n' => filterValue.Length == 2 ? (LowerString.Empty, -1) : (new LowerString(filterValue[2..]), 1),
                    'N' => filterValue.Length == 2 ? (LowerString.Empty, -1) : (new LowerString(filterValue[2..]), 1),
                    'a' => filterValue.Length == 2 ? (LowerString.Empty, -1) : (new LowerString(filterValue[2..]), 2),
                    'A' => filterValue.Length == 2 ? (LowerString.Empty, -1) : (new LowerString(filterValue[2..]), 2),
                    'c' => filterValue.Length == 2 ? (LowerString.Empty, -1) : (new LowerString(filterValue[2..]), 3),
                    'C' => filterValue.Length == 2 ? (LowerString.Empty, -1) : (new LowerString(filterValue[2..]), 3),
                    't' => filterValue.Length == 2 ? (LowerString.Empty, -1) : (new LowerString(filterValue[2..]), 4),
                    'T' => filterValue.Length == 2 ? (LowerString.Empty, -1) : (new LowerString(filterValue[2..]), 4),
                    _   => (new LowerString(filterValue), 0),
                },
            _ => (new LowerString(filterValue), 0),
        };

        return true;
    }

    /// <summary>
    /// Check the state filter for a specific pair of has/has-not flags.
    /// Uses count == 0 to check for has-not and count != 0 for has.
    /// Returns true if it should be filtered and false if not. 
    /// </summary>
    private bool CheckFlags(int count, ModFilter hasNoFlag, ModFilter hasFlag)
    {
        return count switch
        {
            0 when _stateFilter.HasFlag(hasNoFlag) => false,
            0                                      => true,
            _ when _stateFilter.HasFlag(hasFlag)   => false,
            _                                      => true,
        };
    }

    /// <summary>
    /// The overwritten filter method also computes the state.
    /// Folders have default state and are filtered out on the direct string instead of the other options.
    /// If any filter is set, they should be hidden by default unless their children are visible,
    /// or they contain the path search string.
    /// </summary>
    protected override bool ApplyFiltersAndState(FileSystem<Mod>.IPath path, out ModState state)
    {
        if (path is ModFileSystem.Folder f)
        {
            state = default;
            return ModFilterExtensions.UnfilteredStateMods != _stateFilter
             || FilterValue.Length > 0 && !f.FullName().Contains(FilterValue, IgnoreCase);
        }

        return ApplyFiltersAndState((ModFileSystem.Leaf)path, out state);
    }

    /// <summary> Apply the string filters. </summary>
    private bool ApplyStringFilters(ModFileSystem.Leaf leaf, Mod mod)
    {
        return _filterType switch
        {
            -1 => false,
            0  => !(leaf.FullName().Contains(_modFilter.Lower, IgnoreCase) || mod.Name.Contains(_modFilter)),
            1  => !mod.Name.Contains(_modFilter),
            2  => !mod.Author.Contains(_modFilter),
            3  => !mod.LowerChangedItemsString.Contains(_modFilter.Lower),
            4  => !mod.AllTagsLower.Contains(_modFilter.Lower),
            _  => false, // Should never happen
        };
    }

    /// <summary> Only get the text color for a mod if no filters are set. </summary>
    private ColorId GetTextColor(Mod mod, ModSettings? settings, ModCollection collection)
    {
        if (Penumbra.ModManager.NewMods.Contains(mod))
            return ColorId.NewMod;

        if (settings == null)
            return ColorId.UndefinedMod;

        if (!settings.Enabled)
            return collection != _collectionManager.Current ? ColorId.InheritedDisabledMod : ColorId.DisabledMod;

        var conflicts = _collectionManager.Current.Conflicts(mod);
        if (conflicts.Count == 0)
            return collection != _collectionManager.Current ? ColorId.InheritedMod : ColorId.EnabledMod;

        return conflicts.Any(c => !c.Solved)
            ? ColorId.ConflictingMod
            : ColorId.HandledConflictMod;
    }

    private bool CheckStateFilters(Mod mod, ModSettings? settings, ModCollection collection, ref ModState state)
    {
        var isNew = _modManager.NewMods.Contains(mod);
        // Handle mod details.
        if (CheckFlags(mod.TotalFileCount,     ModFilter.HasNoFiles,             ModFilter.HasFiles)
         || CheckFlags(mod.TotalSwapCount,     ModFilter.HasNoFileSwaps,         ModFilter.HasFileSwaps)
         || CheckFlags(mod.TotalManipulations, ModFilter.HasNoMetaManipulations, ModFilter.HasMetaManipulations)
         || CheckFlags(mod.HasOptions ? 1 : 0, ModFilter.HasNoConfig,            ModFilter.HasConfig)
         || CheckFlags(isNew ? 1 : 0,          ModFilter.NotNew,                 ModFilter.IsNew))
            return true;

        // Handle Favoritism
        if (!_stateFilter.HasFlag(ModFilter.Favorite) && mod.Favorite
         || !_stateFilter.HasFlag(ModFilter.NotFavorite) && !mod.Favorite)
            return true;

        // Handle Inheritance
        if (collection == _collectionManager.Current)
        {
            if (!_stateFilter.HasFlag(ModFilter.Uninherited))
                return true;
        }
        else
        {
            state.Color = ColorId.InheritedMod;
            if (!_stateFilter.HasFlag(ModFilter.Inherited))
                return true;
        }

        // Handle settings.
        if (settings == null)
        {
            state.Color = ColorId.UndefinedMod;
            if (!_stateFilter.HasFlag(ModFilter.Undefined)
             || !_stateFilter.HasFlag(ModFilter.Disabled)
             || !_stateFilter.HasFlag(ModFilter.NoConflict))
                return true;
        }
        else if (!settings.Enabled)
        {
            state.Color = collection == _collectionManager.Current ? ColorId.DisabledMod : ColorId.InheritedDisabledMod;
            if (!_stateFilter.HasFlag(ModFilter.Disabled)
             || !_stateFilter.HasFlag(ModFilter.NoConflict))
                return true;
        }
        else
        {
            if (!_stateFilter.HasFlag(ModFilter.Enabled))
                return true;

            // Conflicts can only be relevant if the mod is enabled.
            var conflicts = _collectionManager.Current.Conflicts(mod);
            if (conflicts.Count > 0)
            {
                if (conflicts.Any(c => !c.Solved))
                {
                    if (!_stateFilter.HasFlag(ModFilter.UnsolvedConflict))
                        return true;

                    state.Color = ColorId.ConflictingMod;
                }
                else
                {
                    if (!_stateFilter.HasFlag(ModFilter.SolvedConflict))
                        return true;

                    state.Color = ColorId.HandledConflictMod;
                }
            }
            else if (!_stateFilter.HasFlag(ModFilter.NoConflict))
            {
                return true;
            }
        }

        // isNew color takes precedence before other colors.
        if (isNew)
            state.Color = ColorId.NewMod;

        return false;
    }

    /// <summary> Combined wrapper for handling all filters and setting state. </summary>
    private bool ApplyFiltersAndState(ModFileSystem.Leaf leaf, out ModState state)
    {
        state = new ModState { Color = ColorId.EnabledMod };
        var mod = leaf.Value;
        var (settings, collection) = _collectionManager.Current[mod.Index];

        if (ApplyStringFilters(leaf, mod))
            return true;

        if (_stateFilter != ModFilterExtensions.UnfilteredStateMods)
            return CheckStateFilters(mod, settings, collection, ref state);

        state.Color = GetTextColor(mod, settings, collection);
        return false;
    }

    private void DrawFilterCombo(ref bool everything)
    {
        using var combo = ImRaii.Combo("##filterCombo", string.Empty,
            ImGuiComboFlags.NoPreview | ImGuiComboFlags.PopupAlignLeft | ImGuiComboFlags.HeightLargest);
        if (!combo)
            return;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
            ImGui.GetStyle().ItemSpacing with { Y = 3 * UiHelpers.Scale });
        var flags = (int)_stateFilter;


        if (ImGui.Checkbox("Everything", ref everything))
        {
            _stateFilter = everything ? ModFilterExtensions.UnfilteredStateMods : 0;
            SetFilterDirty();
        }

        ImGui.Dummy(new Vector2(0, 5 * UiHelpers.Scale));
        foreach (ModFilter flag in Enum.GetValues(typeof(ModFilter)))
        {
            if (ImGui.CheckboxFlags(flag.ToName(), ref flags, (int)flag))
            {
                _stateFilter = (ModFilter)flags;
                SetFilterDirty();
            }
        }
    }

    /// <summary> Add the state filter combo-button to the right of the filter box. </summary>
    protected override float CustomFilters(float width)
    {
        var pos            = ImGui.GetCursorPos();
        var remainingWidth = width - ImGui.GetFrameHeight();
        var comboPos       = new Vector2(pos.X + remainingWidth, pos.Y);

        var everything = _stateFilter == ModFilterExtensions.UnfilteredStateMods;

        ImGui.SetCursorPos(comboPos);
        // Draw combo button
        using var color = ImRaii.PushColor(ImGuiCol.Button, Colors.FilterActive, !everything);
        DrawFilterCombo(ref everything);
        _tutorial.OpenTutorial(BasicTutorialSteps.ModFilters);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _stateFilter = ModFilterExtensions.UnfilteredStateMods;
            SetFilterDirty();
        }

        ImGuiUtil.HoverTooltip("Filter mods for their activation status.\nRight-Click to clear all filters.");
        ImGui.SetCursorPos(pos);
        return remainingWidth;
    }

    #endregion
}
