using Dalamud.Interface;
using Dalamud.Interface.DragDrop;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.FileSystem.Selector;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Text.Widget;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Services;
using Penumbra.UI.Classes;
using MessageService = Penumbra.Services.MessageService;

namespace Penumbra.UI.ModsTab;

public sealed class ModFileSystemSelector : FileSystemSelector<Mod, ModFileSystemSelector.ModState>, IUiService
{
    private readonly CommunicatorService     _communicator;
    private readonly Configuration           _config;
    private readonly FileDialogService       _fileDialog;
    private readonly ModManager              _modManager;
    private readonly CollectionManager       _collectionManager;
    private readonly TutorialService         _tutorial;
    private readonly ModImportManager        _modImportManager;
    private readonly IDragDropManager        _dragDrop;
    private readonly ModSearchStringSplitter _filter = new();
    private readonly ModSelection            _selection;

    public ModFileSystemSelector(IKeyState keyState, CommunicatorService communicator, ModFileSystem fileSystem, ModManager modManager,
        CollectionManager collectionManager, Configuration config, TutorialService tutorial, FileDialogService fileDialog,
        MessageService messager, ModImportManager modImportManager, IDragDropManager dragDrop, ModSelection selection)
        : base(fileSystem, keyState, Penumbra.Log, HandleException, allowMultipleSelection: true)
    {
        _communicator      = communicator;
        _modManager        = modManager;
        _collectionManager = collectionManager;
        _config            = config;
        _tutorial          = tutorial;
        _fileDialog        = fileDialog;
        _modImportManager  = modImportManager;
        _dragDrop          = dragDrop;
        _selection         = selection;

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
        UnsubscribeRightClickLeaf(RenameLeaf);
        SetRenameSearchPath(_config.ShowRename);
        AddButton(AddNewModButton,    0);
        AddButton(AddImportModButton, 1);
        AddButton(AddHelpButton,      2);
        AddButton(DeleteModButton,    1000);
        // @formatter:on
        SetFilterTooltip();

        if (_selection.Mod != null)
            SelectByValue(_selection.Mod);
        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.ModFileSystemSelector);
        _communicator.ModSettingChanged.Subscribe(OnSettingChange, ModSettingChanged.Priority.ModFileSystemSelector);
        _communicator.CollectionInheritanceChanged.Subscribe(OnInheritanceChange, CollectionInheritanceChanged.Priority.ModFileSystemSelector);
        _communicator.ModDataChanged.Subscribe(OnModDataChange, ModDataChanged.Priority.ModFileSystemSelector);
        _communicator.ModDiscoveryStarted.Subscribe(StoreCurrentSelection, ModDiscoveryStarted.Priority.ModFileSystemSelector);
        _communicator.ModDiscoveryFinished.Subscribe(RestoreLastSelection, ModDiscoveryFinished.Priority.ModFileSystemSelector);
        SetFilterDirty();
        SelectionChanged += OnSelectionChanged;
    }

    public void SetRenameSearchPath(RenameField value)
    {
        switch (value)
        {
            case RenameField.RenameSearchPath:
                SubscribeRightClickLeaf(RenameLeafMod, 1000);
                UnsubscribeRightClickLeaf(RenameMod);
                break;
            case RenameField.RenameData:
                UnsubscribeRightClickLeaf(RenameLeafMod);
                SubscribeRightClickLeaf(RenameMod, 1000);
                break;
            case RenameField.BothSearchPathPrio:
                UnsubscribeRightClickLeaf(RenameLeafMod);
                UnsubscribeRightClickLeaf(RenameMod);
                SubscribeRightClickLeaf(RenameLeafMod, 1001);
                SubscribeRightClickLeaf(RenameMod,     1000);
                break;
            case RenameField.BothDataPrio:
                UnsubscribeRightClickLeaf(RenameLeafMod);
                UnsubscribeRightClickLeaf(RenameMod);
                SubscribeRightClickLeaf(RenameLeafMod, 1000);
                SubscribeRightClickLeaf(RenameMod,     1001);
                break;
            default:
                UnsubscribeRightClickLeaf(RenameLeafMod);
                UnsubscribeRightClickLeaf(RenameMod);
                break;
        }
    }

    private static readonly string[] ValidModExtensions =
    [
        ".ttmp",
        ".ttmp2",
        ".pmp",
        ".zip",
        ".rar",
        ".7z",
    ];

    public new void Draw(float width)
    {
        _dragDrop.CreateImGuiSource("ModDragDrop", m => m.Extensions.Any(e => ValidModExtensions.Contains(e.ToLowerInvariant())), m =>
        {
            ImGui.TextUnformatted($"Dragging mods for import:\n\t{string.Join("\n\t", m.Files.Select(Path.GetFileName))}");
            return true;
        });
        base.Draw(width);
        if (_dragDrop.CreateImGuiTarget("ModDragDrop", out var files, out _))
            _modImportManager.AddUnpack(files.Where(f => ValidModExtensions.Contains(Path.GetExtension(f.ToLowerInvariant()))));
    }

    public override void Dispose()
    {
        base.Dispose();
        _communicator.ModDiscoveryStarted.Unsubscribe(StoreCurrentSelection);
        _communicator.ModDiscoveryFinished.Unsubscribe(RestoreLastSelection);
        _communicator.ModDataChanged.Unsubscribe(OnModDataChange);
        _communicator.ModSettingChanged.Unsubscribe(OnSettingChange);
        _communicator.CollectionInheritanceChanged.Unsubscribe(OnInheritanceChange);
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
    }

    public new ModFileSystem.Leaf? SelectedLeaf
        => base.SelectedLeaf;

    #region Interface

    // Customization points.
    public override ISortMode<Mod> SortMode
        => _config.SortMode;

    protected override uint ExpandedFolderColor
        => ColorId.FolderExpanded.Value();

    protected override uint CollapsedFolderColor
        => ColorId.FolderCollapsed.Value();

    protected override uint FolderLineColor
        => ColorId.FolderLine.Value();

    protected override bool FoldersDefaultOpen
        => _config.OpenFoldersByDefault;

    protected override void DrawPopups()
    {
        DrawHelpPopup();

        if (ImGuiUtil.OpenNameField("Create New Mod", ref _newModName))
        {
            var newDir = _modManager.Creator.CreateEmptyMod(_modManager.BasePath, _newModName);
            if (newDir != null)
            {
                _modManager.AddMod(newDir, false);
                _newModName = string.Empty;
            }
        }

        while (_modImportManager.AddUnpackedMod(out var mod))
            SelectByValue(mod);
    }

    protected override void DrawLeafName(FileSystem<Mod>.Leaf leaf, in ModState state, bool selected)
    {
        var flags = selected ? ImGuiTreeNodeFlags.Selected | LeafFlags : LeafFlags;
        using var c = ImRaii.PushColor(ImGuiCol.Text, state.Color.Value())
            .Push(ImGuiCol.HeaderHovered, 0x4000FFFF, leaf.Value.Favorite);
        using var id = ImRaii.PushId(leaf.Value.Index);
        ImRaii.TreeNode(leaf.Value.Name, flags).Dispose();
        if (ImGui.IsItemClicked(ImGuiMouseButton.Middle))
        {
            _modManager.SetKnown(leaf.Value);
            var (setting, collection) = _collectionManager.Active.Current[leaf.Value.Index];
            if (_config.DeleteModModifier.ForcedModifier(new DoubleModifier(ModifierHotkey.Control, ModifierHotkey.Shift)).IsActive())
            {
                _collectionManager.Editor.SetModInheritance(_collectionManager.Active.Current, leaf.Value, true);
            }
            else
            {
                var inherited = collection != _collectionManager.Active.Current;
                if (inherited)
                    _collectionManager.Editor.SetModInheritance(_collectionManager.Active.Current, leaf.Value, false);
                _collectionManager.Editor.SetModState(_collectionManager.Active.Current, leaf.Value, setting is not { Enabled: true });
            }
        }

        if (!state.Priority.IsDefault && !_config.HidePrioritiesInSelector)
        {
            var line           = ImGui.GetItemRectMin().Y;
            var itemPos        = ImGui.GetItemRectMax().X;
            var maxWidth       = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
            var priorityString = $"[{state.Priority}]";
            var size           = ImGui.CalcTextSize(priorityString).X;
            var remainingSpace = maxWidth - itemPos;
            var offset         = remainingSpace - size;
            if (ImGui.GetScrollMaxY() == 0)
                offset -= ImGui.GetStyle().ItemInnerSpacing.X;

            if (offset > ImGui.GetStyle().ItemSpacing.X)
                ImGui.GetWindowDrawList().AddText(new Vector2(itemPos + offset, line), ColorId.SelectorPriority.Value(), priorityString);
        }
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
        var button = ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileImport.ToIconString(), size,
            "Import one or multiple mods from Tex Tools Mod Pack Files or Penumbra Mod Pack Files.", !_modManager.Valid, true);
        _tutorial.OpenTutorial(BasicTutorialSteps.ModImport);
        if (!button)
            return;

        var modPath = _config.DefaultModImportPath.Length > 0
            ? _config.DefaultModImportPath
            : _config.ModDirectory.Length > 0
                ? _config.ModDirectory
                : null;

        _fileDialog.OpenFilePicker("Import Mod Pack",
            "Mod Packs{.ttmp,.ttmp2,.pmp},TexTools Mod Packs{.ttmp,.ttmp2},Penumbra Mod Packs{.pmp},Archives{.zip,.7z,.rar}", (s, f) =>
            {
                if (!s)
                    return;

                _modImportManager.AddUnpack(f);
            }, 0, modPath, _config.AlwaysOpenDefaultImport);
    }

    private void RenameLeafMod(ModFileSystem.Leaf leaf)
    {
        ImGui.Separator();
        RenameLeaf(leaf);
    }

    private void RenameMod(ModFileSystem.Leaf leaf)
    {
        ImGui.Separator();
        var currentName = leaf.Value.Name.Text;
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere(0);
        ImGui.TextUnformatted("Rename Mod:");
        if (ImGui.InputText("##RenameMod", ref currentName, 256, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _modManager.DataEditor.ChangeModName(leaf.Value, currentName);
            ImGui.CloseCurrentPopup();
        }

        ImGuiUtil.HoverTooltip("Enter a new name here to rename the changed mod.");
    }

    private void DeleteModButton(Vector2 size)
        => DeleteSelectionButton(size, _config.DeleteModModifier, "mod", "mods", _modManager.DeleteMod);

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
            _modManager.SetKnown(l.Value);
            return l.Value;
        });

        if (inherit)
            _collectionManager.Editor.SetMultipleModInheritances(_collectionManager.Active.Current, mods, enabled);
        else
            _collectionManager.Editor.SetMultipleModStates(_collectionManager.Active.Current, mods, enabled);
    }

    private void DrawHelpPopup()
    {
        ImGuiUtil.HelpPopup("ExtendedHelp", new Vector2(1000 * UiHelpers.Scale, 38.5f * ImGui.GetTextLineHeightWithSpacing()), () =>
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
            ImGuiUtil.BulletTextColored(ColorId.EnabledMod.Value(),           "enabled in the current collection.");
            ImGuiUtil.BulletTextColored(ColorId.DisabledMod.Value(),          "disabled in the current collection.");
            ImGuiUtil.BulletTextColored(ColorId.InheritedMod.Value(),         "enabled due to inheritance from another collection.");
            ImGuiUtil.BulletTextColored(ColorId.InheritedDisabledMod.Value(), "disabled due to inheritance from another collection.");
            ImGuiUtil.BulletTextColored(ColorId.UndefinedMod.Value(),         "unconfigured in all inherited collections.");
            ImGuiUtil.BulletTextColored(ColorId.NewMod.Value(),
                "newly imported during this session. Will go away when first enabling a mod or when Penumbra is reloaded.");
            ImGuiUtil.BulletTextColored(ColorId.HandledConflictMod.Value(),
                "enabled and conflicting with another enabled Mod, but on different priorities (i.e. the conflict is solved).");
            ImGuiUtil.BulletTextColored(ColorId.ConflictingMod.Value(),
                "enabled and conflicting with another enabled Mod on the same priority.");
            ImGuiUtil.BulletTextColored(ColorId.FolderExpanded.Value(),  "expanded mod folder.");
            ImGuiUtil.BulletTextColored(ColorId.FolderCollapsed.Value(), "collapsed mod folder");
            indent.Pop(1);
            ImGui.BulletText("Middle-click a mod to disable it if it is enabled or enable it if it is disabled.");
            indent.Push();
            ImGui.BulletText(
                $"Holding {_config.DeleteModModifier.ForcedModifier(new DoubleModifier(ModifierHotkey.Control, ModifierHotkey.Shift))} while middle-clicking lets it inherit, discarding settings.");
            indent.Pop(1);
            ImGui.BulletText("Right-click a mod to enter its sort order, which is its name by default, possibly with a duplicate number.");
            indent.Push();
            ImGui.BulletText("A sort order differing from the mods name will not be displayed, it will just be used for ordering.");
            ImGui.BulletText(
                "If the sort order string contains Forward-Slashes ('/'), the preceding substring will be turned into folders automatically.");
            indent.Pop(1);
            ImGui.BulletText(
                "You can drag and drop mods and subfolders into existing folders. Dropping them onto mods is the same as dropping them onto the parent of the mod.");
            indent.Push();
            ImGui.BulletText(
                "You can select multiple mods and folders by holding Control while clicking them, and then drag all of them at once.");
            ImGui.BulletText(
                "Selected mods inside an also selected folder will be ignored when dragging and move inside their folder instead of directly into the target.");
            indent.Pop(1);
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
        => Penumbra.Messager.NotificationMessage(e, e.Message, NotificationType.Warning);

    #endregion

    #region Automatic cache update functions.

    private void OnSettingChange(ModCollection collection, ModSettingChange type, Mod? mod, Setting oldValue, int groupIdx, bool inherited)
    {
        if (collection == _collectionManager.Active.Current)
            SetFilterDirty();
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

    private void OnInheritanceChange(ModCollection collection, bool _)
    {
        if (collection == _collectionManager.Active.Current)
            SetFilterDirty();
    }

    private void OnCollectionChange(CollectionType collectionType, ModCollection? oldCollection, ModCollection? newCollection, string _)
    {
        if (collectionType is CollectionType.Current && oldCollection != newCollection)
            SetFilterDirty();
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
        Select(leaf, AllowMultipleSelection);
        _lastSelectedDirectory = string.Empty;
    }

    private void OnSelectionChanged(Mod? oldSelection, Mod? newSelection, in ModState state)
        => _selection.SelectMod(newSelection);

    #endregion

    #region Filters

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ModState
    {
        public ColorId     Color;
        public ModPriority Priority;
    }

    private ModFilter _stateFilter = ModFilterExtensions.UnfilteredStateMods;

    private void SetFilterTooltip()
    {
        FilterTooltip = "Filter mods for those where their full paths or names contain the given strings, split by spaces.\n"
          + "Enter c:[string] to filter for mods changing specific items.\n"
          + "Enter t:[string] to filter for mods set to specific tags.\n"
          + "Enter n:[string] to filter only for mod names and no paths.\n"
          + "Enter a:[string] to filter for mods by specific authors.\n"
          + $"Enter s:[string] to filter for mods by the categories of the items they change (1-{ChangedItemFlagExtensions.NumCategories + 1} or partial category name).\n\n"
          + "Use None as a placeholder value that only matches empty lists or names.\n"
          + "Regularly, a mod has to match all supplied criteria separately.\n"
          + "Put a - in front of a search token to search only for mods not matching the criterion.\n"
          + "Put a ? in front of a search token to search for mods matching at least one of the '?'-criteria.\n"
          + "Wrap spaces in \"[string with space]\" to match this exact combination of words.\n\n"
          + "Example: 't:Tag1 t:\"Tag 2\" -t:Tag3 -a:None s:Body -c:Hempen ?c:Camise ?n:Top' will match any mod that\n"
          + "    - contains the tags 'tag1' and 'tag 2'\n"
          + "    - does not contain the tag 'tag3'\n"
          + "    - has any author set (negating None means Any)\n"
          + "    - changes an item of the 'Body' category\n"
          + "    - and either contains a changed item with 'camise' in it's name, or has 'top' in the mod's name.";
    }

    /// <summary> Appropriately identify and set the string filter and its type. </summary>
    protected override bool ChangeFilter(string filterValue)
    {
        _filter.Parse(filterValue);
        return true;
    }

    /// <summary>
    /// Check the state filter for a specific pair of has/has-not flags.
    /// Uses count == 0 to check for has-not and count != 0 for has.
    /// Returns true if it should be filtered and false if not. 
    /// </summary>
    private bool CheckFlags(int count, ModFilter hasNoFlag, ModFilter hasFlag)
        => count switch
        {
            0 when _stateFilter.HasFlag(hasNoFlag) => false,
            0                                      => true,
            _ when _stateFilter.HasFlag(hasFlag)   => false,
            _                                      => true,
        };

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
             || !_filter.IsVisible(f);
        }

        return ApplyFiltersAndState((ModFileSystem.Leaf)path, out state);
    }

    /// <summary> Apply the string filters. </summary>
    private bool ApplyStringFilters(ModFileSystem.Leaf leaf, Mod mod)
        => !_filter.IsVisible(leaf);

    /// <summary> Only get the text color for a mod if no filters are set. </summary>
    private ColorId GetTextColor(Mod mod, ModSettings? settings, ModCollection collection)
    {
        if (_modManager.IsNew(mod))
            return ColorId.NewMod;

        if (settings == null)
            return ColorId.UndefinedMod;

        if (!settings.Enabled)
            return collection != _collectionManager.Active.Current ? ColorId.InheritedDisabledMod : ColorId.DisabledMod;

        var conflicts = _collectionManager.Active.Current.Conflicts(mod);
        if (conflicts.Count == 0)
            return collection != _collectionManager.Active.Current ? ColorId.InheritedMod : ColorId.EnabledMod;

        return conflicts.Any(c => !c.Solved)
            ? ColorId.ConflictingMod
            : ColorId.HandledConflictMod;
    }

    private bool CheckStateFilters(Mod mod, ModSettings? settings, ModCollection collection, ref ModState state)
    {
        var isNew = _modManager.IsNew(mod);
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
        if (collection == _collectionManager.Active.Current)
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
            state.Color = collection == _collectionManager.Active.Current ? ColorId.DisabledMod : ColorId.InheritedDisabledMod;
            if (!_stateFilter.HasFlag(ModFilter.Disabled)
             || !_stateFilter.HasFlag(ModFilter.NoConflict))
                return true;
        }
        else
        {
            if (!_stateFilter.HasFlag(ModFilter.Enabled))
                return true;

            // Conflicts can only be relevant if the mod is enabled.
            var conflicts = _collectionManager.Active.Current.Conflicts(mod);
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
        var mod = leaf.Value;
        var (settings, collection) = _collectionManager.Active.Current[mod.Index];

        state = new ModState
        {
            Color    = ColorId.EnabledMod,
            Priority = settings?.Priority ?? ModPriority.Default,
        };
        if (ApplyStringFilters(leaf, mod))
            return true;

        if (_stateFilter != ModFilterExtensions.UnfilteredStateMods)
            return CheckStateFilters(mod, settings, collection, ref state);

        state.Color = GetTextColor(mod, settings, collection);
        return false;
    }

    private bool DrawFilterCombo(ref bool everything)
    {
        using var combo = ImRaii.Combo("##filterCombo", string.Empty,
            ImGuiComboFlags.NoPreview | ImGuiComboFlags.PopupAlignLeft | ImGuiComboFlags.HeightLargest);
        var ret = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        if (!combo)
            return ret;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
            ImGui.GetStyle().ItemSpacing with { Y = 3 * UiHelpers.Scale });

        if (ImGui.Checkbox("Everything", ref everything))
        {
            _stateFilter = everything ? ModFilterExtensions.UnfilteredStateMods : 0;
            SetFilterDirty();
        }

        ImGui.Dummy(new Vector2(0, 5 * UiHelpers.Scale));
        foreach (var (onFlag, offFlag, name) in ModFilterExtensions.TriStatePairs)
        {
            if (TriStateCheckbox.Instance.Draw(name, ref _stateFilter, onFlag, offFlag))
                SetFilterDirty();
        }

        foreach (var group in ModFilterExtensions.Groups)
        {
            ImGui.Separator();
            foreach (var (flag, name) in group)
            {
                if (ImUtf8.Checkbox(name, ref _stateFilter, flag))
                    SetFilterDirty();
            }
        }

        return ret;
    }

    /// <summary> Add the state filter combo-button to the right of the filter box. </summary>
    protected override (float, bool) CustomFilters(float width)
    {
        var pos            = ImGui.GetCursorPos();
        var remainingWidth = width - ImGui.GetFrameHeight();
        var comboPos       = new Vector2(pos.X + remainingWidth, pos.Y);

        var everything = _stateFilter == ModFilterExtensions.UnfilteredStateMods;

        ImGui.SetCursorPos(comboPos);
        // Draw combo button
        using var color      = ImRaii.PushColor(ImGuiCol.Button, Colors.FilterActive, !everything);
        var       rightClick = DrawFilterCombo(ref everything);
        _tutorial.OpenTutorial(BasicTutorialSteps.ModFilters);
        if (rightClick)
        {
            _stateFilter = ModFilterExtensions.UnfilteredStateMods;
            SetFilterDirty();
        }

        ImGuiUtil.HoverTooltip("Filter mods for their activation status.\nRight-Click to clear all filters.");
        ImGui.SetCursorPos(pos);
        return (remainingWidth, rightClick);
    }

    #endregion
}
