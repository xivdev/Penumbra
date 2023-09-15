using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal.Notifications;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.ModsTab;

public class ModPanelEditTab : ITab
{
    private readonly ChatService           _chat;
    private readonly FilenameService       _filenames;
    private readonly ModManager            _modManager;
    private readonly ModExportManager      _modExportManager;
    private readonly ModFileSystem         _fileSystem;
    private readonly ModFileSystemSelector _selector;
    private readonly ModEditWindow         _editWindow;
    private readonly ModEditor             _editor;
    private readonly Configuration         _config;

    private readonly TagButtons _modTags = new();

    private Vector2            _cellPadding = Vector2.Zero;
    private Vector2            _itemSpacing = Vector2.Zero;
    private ModFileSystem.Leaf _leaf        = null!;
    private Mod                _mod         = null!;

    public ModPanelEditTab(ModManager modManager, ModFileSystemSelector selector, ModFileSystem fileSystem, ChatService chat,
        ModEditWindow editWindow, ModEditor editor, FilenameService filenames, ModExportManager modExportManager, Configuration config)
    {
        _modManager       = modManager;
        _selector         = selector;
        _fileSystem       = fileSystem;
        _chat             = chat;
        _editWindow       = editWindow;
        _editor           = editor;
        _filenames        = filenames;
        _modExportManager = modExportManager;
        _config           = config;
    }

    public ReadOnlySpan<byte> Label
        => "Edit Mod"u8;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##editChild", -Vector2.One);
        if (!child)
            return;

        _leaf = _selector.SelectedLeaf!;
        _mod  = _selector.Selected!;

        _cellPadding = ImGui.GetStyle().CellPadding with { X = 2 * UiHelpers.Scale };
        _itemSpacing = ImGui.GetStyle().CellPadding with { X = 4 * UiHelpers.Scale };

        EditButtons();
        EditRegularMeta();
        UiHelpers.DefaultLineSpace();

        if (Input.Text("Mod Path", Input.Path, Input.None, _leaf.FullName(), out var newPath, 256, UiHelpers.InputTextWidth.X))
            try
            {
                _fileSystem.RenameAndMove(_leaf, newPath);
            }
            catch (Exception e)
            {
                _chat.NotificationMessage(e.Message, "Warning", NotificationType.Warning);
            }

        UiHelpers.DefaultLineSpace();
        var tagIdx = _modTags.Draw("Mod Tags: ", "Edit tags by clicking them, or add new tags. Empty tags are removed.", _mod.ModTags,
            out var editedTag);
        if (tagIdx >= 0)
            _modManager.DataEditor.ChangeModTag(_mod, tagIdx, editedTag);

        UiHelpers.DefaultLineSpace();
        AddOptionGroup.Draw(_filenames, _modManager, _mod);
        UiHelpers.DefaultLineSpace();

        for (var groupIdx = 0; groupIdx < _mod.Groups.Count; ++groupIdx)
            EditGroup(groupIdx);

        EndActions();
        DescriptionEdit.DrawPopup(_modManager);
    }

    public void Reset()
    {
        AddOptionGroup.Reset();
        MoveDirectory.Reset();
        Input.Reset();
        OptionTable.Reset();
    }

    /// <summary> The general edit row for non-detailed mod edits. </summary>
    private void EditButtons()
    {
        var buttonSize   = new Vector2(150 * UiHelpers.Scale, 0);
        var folderExists = Directory.Exists(_mod.ModPath.FullName);
        var tt = folderExists
            ? $"Open \"{_mod.ModPath.FullName}\" in the file explorer of your choice."
            : $"Mod directory \"{_mod.ModPath.FullName}\" does not exist.";
        if (ImGuiUtil.DrawDisabledButton("Open Mod Directory", buttonSize, tt, !folderExists))
            Process.Start(new ProcessStartInfo(_mod.ModPath.FullName) { UseShellExecute = true });

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("Reload Mod", buttonSize, "Reload the current mod from its files.\n"
              + "If the mod directory or meta file do not exist anymore or if the new mod name is empty, the mod is deleted instead.",
                false))
            _modManager.ReloadMod(_mod);

        BackupButtons(buttonSize);
        MoveDirectory.Draw(_modManager, _mod, buttonSize);

        UiHelpers.DefaultLineSpace();
        DrawUpdateBibo(buttonSize);

        UiHelpers.DefaultLineSpace();
    }

    private void DrawUpdateBibo(Vector2 buttonSize)
    {
        if (ImGui.Button("Update Bibo Material", buttonSize))
        {
            _editor.LoadMod(_mod);
            _editor.MdlMaterialEditor.ReplaceAllMaterials("bibo",     "b");
            _editor.MdlMaterialEditor.ReplaceAllMaterials("bibopube", "c");
            _editor.MdlMaterialEditor.SaveAllModels(_editor.Compactor);
            _editWindow.UpdateModels();
        }

        ImGuiUtil.HoverTooltip(
            "For every model in this mod, change all material names that end in a _b or _c suffix to a _bibo or _bibopube suffix respectively.\n"
          + "Does nothing if the mod does not contain any such models or no model contains such materials.\n"
          + "Use this for outdated mods made for old Bibo bodies.\n"
          + "Go to Advanced Editing for more fine-tuned control over material assignment.");
    }

    private void BackupButtons(Vector2 buttonSize)
    {
        var backup = new ModBackup(_modExportManager, _mod);
        var tt = ModBackup.CreatingBackup
            ? "Already exporting a mod."
            : backup.Exists
                ? $"Overwrite current exported mod \"{backup.Name}\" with current mod."
                : $"Create exported archive of current mod at \"{backup.Name}\".";
        if (ImGuiUtil.DrawDisabledButton("Export Mod", buttonSize, tt, ModBackup.CreatingBackup))
            backup.CreateAsync();

        ImGui.SameLine();
        tt = backup.Exists
            ? $"Delete existing mod export \"{backup.Name}\" (hold {_config.DeleteModModifier} while clicking)."
            : $"Exported mod \"{backup.Name}\" does not exist.";
        if (ImGuiUtil.DrawDisabledButton("Delete Export", buttonSize, tt, !backup.Exists || !_config.DeleteModModifier.IsActive()))
            backup.Delete();

        tt = backup.Exists
            ? $"Restore mod from exported file \"{backup.Name}\" (hold {_config.DeleteModModifier} while clicking)."
            : $"Exported mod \"{backup.Name}\" does not exist.";
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("Restore From Export", buttonSize, tt, !backup.Exists || !_config.DeleteModModifier.IsActive()))
            backup.Restore(_modManager);
        if (backup.Exists)
        {
            ImGui.SameLine();
            using (var font = ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.TextUnformatted(FontAwesomeIcon.CheckCircle.ToIconString());
            }

            ImGuiUtil.HoverTooltip($"Export exists in \"{backup.Name}\".");
        }
    }

    /// <summary> Anything about editing the regular meta information about the mod. </summary>
    private void EditRegularMeta()
    {
        if (Input.Text("Name", Input.Name, Input.None, _mod.Name, out var newName, 256, UiHelpers.InputTextWidth.X))
            _modManager.DataEditor.ChangeModName(_mod, newName);

        if (Input.Text("Author", Input.Author, Input.None, _mod.Author, out var newAuthor, 256, UiHelpers.InputTextWidth.X))
            _modManager.DataEditor.ChangeModAuthor(_mod, newAuthor);

        if (Input.Text("Version", Input.Version, Input.None, _mod.Version, out var newVersion, 32,
                UiHelpers.InputTextWidth.X))
            _modManager.DataEditor.ChangeModVersion(_mod, newVersion);

        if (Input.Text("Website", Input.Website, Input.None, _mod.Website, out var newWebsite, 256,
                UiHelpers.InputTextWidth.X))
            _modManager.DataEditor.ChangeModWebsite(_mod, newWebsite);

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(UiHelpers.ScaleX3));

        var reducedSize = new Vector2(UiHelpers.InputTextMinusButton3, 0);
        if (ImGui.Button("Edit Description", reducedSize))
            _delayedActions.Enqueue(() => DescriptionEdit.OpenPopup(_mod, Input.Description));

        ImGui.SameLine();
        var fileExists = File.Exists(_filenames.ModMetaPath(_mod));
        var tt = fileExists
            ? "Open the metadata json file in the text editor of your choice."
            : "The metadata json file does not exist.";
        if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.FileExport.ToIconString()}##metaFile", UiHelpers.IconButtonSize, tt,
                !fileExists, true))
            Process.Start(new ProcessStartInfo(_filenames.ModMetaPath(_mod)) { UseShellExecute = true });
    }

    /// <summary> Do some edits outside of iterations. </summary>
    private readonly Queue<Action> _delayedActions = new();

    /// <summary> Delete a marked group or option outside of iteration. </summary>
    private void EndActions()
    {
        while (_delayedActions.TryDequeue(out var action))
            action.Invoke();
    }

    /// <summary> Text input to add a new option group at the end of the current groups. </summary>
    private static class AddOptionGroup
    {
        private static string _newGroupName = string.Empty;

        public static void Reset()
            => _newGroupName = string.Empty;

        public static void Draw(FilenameService filenames, ModManager modManager, Mod mod)
        {
            using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(UiHelpers.ScaleX3));
            ImGui.SetNextItemWidth(UiHelpers.InputTextMinusButton3);
            ImGui.InputTextWithHint("##newGroup", "Add new option group...", ref _newGroupName, 256);
            ImGui.SameLine();
            var defaultFile = filenames.OptionGroupFile(mod, -1);
            var fileExists  = File.Exists(defaultFile);
            var tt = fileExists
                ? "Open the default option json file in the text editor of your choice."
                : "The default option json file does not exist.";
            if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.FileExport.ToIconString()}##defaultFile", UiHelpers.IconButtonSize, tt,
                    !fileExists, true))
                Process.Start(new ProcessStartInfo(defaultFile) { UseShellExecute = true });

            ImGui.SameLine();

            var nameValid = ModOptionEditor.VerifyFileName(mod, null, _newGroupName, false);
            tt = nameValid ? "Add new option group to the mod." : "Can not add a group of this name.";
            if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), UiHelpers.IconButtonSize,
                    tt, !nameValid, true))
                return;

            modManager.OptionEditor.AddModGroup(mod, GroupType.Single, _newGroupName);
            Reset();
        }
    }

    /// <summary> A text input for the new directory name and a button to apply the move. </summary>
    private static class MoveDirectory
    {
        private static string?           _currentModDirectory;
        private static NewDirectoryState _state = NewDirectoryState.Identical;

        public static void Reset()
        {
            _currentModDirectory = null;
            _state               = NewDirectoryState.Identical;
        }

        public static void Draw(ModManager modManager, Mod mod, Vector2 buttonSize)
        {
            ImGui.SetNextItemWidth(buttonSize.X * 2 + ImGui.GetStyle().ItemSpacing.X);
            var tmp = _currentModDirectory ?? mod.ModPath.Name;
            if (ImGui.InputText("##newModMove", ref tmp, 64))
            {
                _currentModDirectory = tmp;
                _state               = modManager.NewDirectoryValid(mod.ModPath.Name, _currentModDirectory, out _);
            }

            var (disabled, tt) = _state switch
            {
                NewDirectoryState.Identical      => (true, "Current directory name is identical to new one."),
                NewDirectoryState.Empty          => (true, "Please enter a new directory name first."),
                NewDirectoryState.NonExisting    => (false, $"Move mod from {mod.ModPath.Name} to {_currentModDirectory}."),
                NewDirectoryState.ExistsEmpty    => (false, $"Move mod from {mod.ModPath.Name} to {_currentModDirectory}."),
                NewDirectoryState.ExistsNonEmpty => (true, $"{_currentModDirectory} already exists and is not empty."),
                NewDirectoryState.ExistsAsFile   => (true, $"{_currentModDirectory} exists as a file."),
                NewDirectoryState.ContainsInvalidSymbols => (true,
                    $"{_currentModDirectory} contains invalid symbols for FFXIV."),
                _ => (true, "Unknown error."),
            };
            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton("Rename Mod Directory", buttonSize, tt, disabled) && _currentModDirectory != null)
            {
                modManager.MoveModDirectory(mod, _currentModDirectory);
                Reset();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "The mod directory name is used to correspond stored settings and sort orders, otherwise it has no influence on anything that is displayed.\n"
              + "This can currently not be used on pre-existing folders and does not support merges or overwriting.");
        }
    }

    /// <summary> Open a popup to edit a multi-line mod or option description. </summary>
    private static class DescriptionEdit
    {
        private const  string PopupName                = "Edit Description";
        private static string _newDescription          = string.Empty;
        private static string _oldDescription          = string.Empty;
        private static int    _newDescriptionIdx       = -1;
        private static int    _newDescriptionOptionIdx = -1;
        private static Mod?   _mod;

        public static void OpenPopup(Mod mod, int groupIdx, int optionIdx = -1)
        {
            _newDescriptionIdx       = groupIdx;
            _newDescriptionOptionIdx = optionIdx;
            _newDescription = groupIdx < 0
                ? mod.Description
                : optionIdx < 0
                    ? mod.Groups[groupIdx].Description
                    : mod.Groups[groupIdx][optionIdx].Description;
            _oldDescription = _newDescription;

            _mod = mod;
            ImGui.OpenPopup(PopupName);
        }

        public static void DrawPopup(ModManager modManager)
        {
            if (_mod == null)
                return;

            using var popup = ImRaii.Popup(PopupName);
            if (!popup)
                return;

            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere();

            ImGui.InputTextMultiline("##editDescription", ref _newDescription, 4096, ImGuiHelpers.ScaledVector2(800, 800));
            UiHelpers.DefaultLineSpace();

            var buttonSize = ImGuiHelpers.ScaledVector2(100, 0);
            var width = 2 * buttonSize.X
              + 4 * ImGui.GetStyle().FramePadding.X
              + ImGui.GetStyle().ItemSpacing.X;
            ImGui.SetCursorPosX((800 * UiHelpers.Scale - width) / 2);

            var tooltip = _newDescription != _oldDescription ? string.Empty : "No changes made yet.";

            if (ImGuiUtil.DrawDisabledButton("Save", buttonSize, tooltip, tooltip.Length > 0))
            {
                switch (_newDescriptionIdx)
                {
                    case Input.Description:
                        modManager.DataEditor.ChangeModDescription(_mod, _newDescription);
                        break;
                    case >= 0:
                        if (_newDescriptionOptionIdx < 0)
                            modManager.OptionEditor.ChangeGroupDescription(_mod, _newDescriptionIdx, _newDescription);
                        else
                            modManager.OptionEditor.ChangeOptionDescription(_mod, _newDescriptionIdx, _newDescriptionOptionIdx,
                                _newDescription);

                        break;
                }

                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (!ImGui.Button("Cancel", buttonSize)
             && !ImGui.IsKeyPressed(ImGuiKey.Escape))
                return;

            _newDescriptionIdx = Input.None;
            _newDescription    = string.Empty;
            ImGui.CloseCurrentPopup();
        }
    }

    private void EditGroup(int groupIdx)
    {
        var       group = _mod.Groups[groupIdx];
        using var id    = ImRaii.PushId(groupIdx);
        using var frame = ImRaii.FramedGroup($"Group #{groupIdx + 1}");

        using var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, _cellPadding)
            .Push(ImGuiStyleVar.ItemSpacing, _itemSpacing);

        if (Input.Text("##Name", groupIdx, Input.None, group.Name, out var newGroupName, 256, UiHelpers.InputTextWidth.X))
            _modManager.OptionEditor.RenameModGroup(_mod, groupIdx, newGroupName);

        ImGuiUtil.HoverTooltip("Group Name");
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), UiHelpers.IconButtonSize,
                "Delete this option group.\nHold Control while clicking to delete.", !ImGui.GetIO().KeyCtrl, true))
            _delayedActions.Enqueue(() => _modManager.OptionEditor.DeleteModGroup(_mod, groupIdx));

        ImGui.SameLine();

        if (Input.Priority("##Priority", groupIdx, Input.None, group.Priority, out var priority, 50 * UiHelpers.Scale))
            _modManager.OptionEditor.ChangeGroupPriority(_mod, groupIdx, priority);

        ImGuiUtil.HoverTooltip("Group Priority");

        DrawGroupCombo(group, groupIdx);
        ImGui.SameLine();

        var tt = groupIdx == 0 ? "Can not move this group further upwards." : $"Move this group up to group {groupIdx}.";
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.ArrowUp.ToIconString(), UiHelpers.IconButtonSize,
                tt, groupIdx == 0, true))
            _delayedActions.Enqueue(() => _modManager.OptionEditor.MoveModGroup(_mod, groupIdx, groupIdx - 1));

        ImGui.SameLine();
        tt = groupIdx == _mod.Groups.Count - 1
            ? "Can not move this group further downwards."
            : $"Move this group down to group {groupIdx + 2}.";
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.ArrowDown.ToIconString(), UiHelpers.IconButtonSize,
                tt, groupIdx == _mod.Groups.Count - 1, true))
            _delayedActions.Enqueue(() => _modManager.OptionEditor.MoveModGroup(_mod, groupIdx, groupIdx + 1));

        ImGui.SameLine();

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Edit.ToIconString(), UiHelpers.IconButtonSize,
                "Edit group description.", false, true))
            _delayedActions.Enqueue(() => DescriptionEdit.OpenPopup(_mod, groupIdx));

        ImGui.SameLine();
        var fileName   = _filenames.OptionGroupFile(_mod, groupIdx);
        var fileExists = File.Exists(fileName);
        tt = fileExists
            ? $"Open the {group.Name} json file in the text editor of your choice."
            : $"The {group.Name} json file does not exist.";
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileExport.ToIconString(), UiHelpers.IconButtonSize, tt, !fileExists, true))
            Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });

        UiHelpers.DefaultLineSpace();

        OptionTable.Draw(this, groupIdx);
    }

    /// <summary> Draw the table displaying all options and the add new option line. </summary>
    private static class OptionTable
    {
        private const string DragDropLabel = "##DragOption";

        private static int    _newOptionNameIdx  = -1;
        private static string _newOptionName     = string.Empty;
        private static int    _dragDropGroupIdx  = -1;
        private static int    _dragDropOptionIdx = -1;

        public static void Reset()
        {
            _newOptionNameIdx  = -1;
            _newOptionName     = string.Empty;
            _dragDropGroupIdx  = -1;
            _dragDropOptionIdx = -1;
        }

        public static void Draw(ModPanelEditTab panel, int groupIdx)
        {
            using var table = ImRaii.Table(string.Empty, 6, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            ImGui.TableSetupColumn("idx",     ImGuiTableColumnFlags.WidthFixed, 60 * UiHelpers.Scale);
            ImGui.TableSetupColumn("default", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthFixed,
                UiHelpers.InputTextWidth.X - 72 * UiHelpers.Scale - ImGui.GetFrameHeight() - UiHelpers.IconButtonSize.X);
            ImGui.TableSetupColumn("description", ImGuiTableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
            ImGui.TableSetupColumn("delete",      ImGuiTableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
            ImGui.TableSetupColumn("priority",    ImGuiTableColumnFlags.WidthFixed, 50 * UiHelpers.Scale);

            var group = panel._mod.Groups[groupIdx];
            for (var optionIdx = 0; optionIdx < group.Count; ++optionIdx)
                EditOption(panel, group, groupIdx, optionIdx);

            DrawNewOption(panel, groupIdx, UiHelpers.IconButtonSize);
        }

        /// <summary> Draw a line for a single option. </summary>
        private static void EditOption(ModPanelEditTab panel, IModGroup group, int groupIdx, int optionIdx)
        {
            var       option = group[optionIdx];
            using var id     = ImRaii.PushId(optionIdx);
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Selectable($"Option #{optionIdx + 1}");
            Source(group, groupIdx, optionIdx);
            Target(panel, group, groupIdx, optionIdx);

            ImGui.TableNextColumn();


            if (group.Type == GroupType.Single)
            {
                if (ImGui.RadioButton("##default", group.DefaultSettings == optionIdx))
                    panel._modManager.OptionEditor.ChangeModGroupDefaultOption(panel._mod, groupIdx, (uint)optionIdx);

                ImGuiUtil.HoverTooltip($"Set {option.Name} as the default choice for this group.");
            }
            else
            {
                var isDefaultOption = ((group.DefaultSettings >> optionIdx) & 1) != 0;
                if (ImGui.Checkbox("##default", ref isDefaultOption))
                    panel._modManager.OptionEditor.ChangeModGroupDefaultOption(panel._mod, groupIdx, isDefaultOption
                        ? group.DefaultSettings | (1u << optionIdx)
                        : group.DefaultSettings & ~(1u << optionIdx));

                ImGuiUtil.HoverTooltip($"{(isDefaultOption ? "Disable" : "Enable")} {option.Name} per default in this group.");
            }

            ImGui.TableNextColumn();
            if (Input.Text("##Name", groupIdx, optionIdx, option.Name, out var newOptionName, 256, -1))
                panel._modManager.OptionEditor.RenameOption(panel._mod, groupIdx, optionIdx, newOptionName);

            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Edit.ToIconString(), UiHelpers.IconButtonSize, "Edit option description.",
                    false, true))
                panel._delayedActions.Enqueue(() => DescriptionEdit.OpenPopup(panel._mod, groupIdx, optionIdx));

            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), UiHelpers.IconButtonSize,
                    "Delete this option.\nHold Control while clicking to delete.", !ImGui.GetIO().KeyCtrl, true))
                panel._delayedActions.Enqueue(() => panel._modManager.OptionEditor.DeleteOption(panel._mod, groupIdx, optionIdx));

            ImGui.TableNextColumn();
            if (group.Type != GroupType.Multi)
                return;

            if (Input.Priority("##Priority", groupIdx, optionIdx, group.OptionPriority(optionIdx), out var priority,
                    50 * UiHelpers.Scale))
                panel._modManager.OptionEditor.ChangeOptionPriority(panel._mod, groupIdx, optionIdx, priority);

            ImGuiUtil.HoverTooltip("Option priority.");
        }

        /// <summary> Draw the line to add a new option. </summary>
        private static void DrawNewOption(ModPanelEditTab panel, int groupIdx, Vector2 iconButtonSize)
        {
            var mod   = panel._mod;
            var group = mod.Groups[groupIdx];
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Selectable($"Option #{group.Count + 1}");
            Target(panel, group, groupIdx, group.Count);
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var tmp = _newOptionNameIdx == groupIdx ? _newOptionName : string.Empty;
            if (ImGui.InputTextWithHint("##newOption", "Add new option...", ref tmp, 256))
            {
                _newOptionName    = tmp;
                _newOptionNameIdx = groupIdx;
            }

            ImGui.TableNextColumn();
            var canAddGroup = mod.Groups[groupIdx].Type != GroupType.Multi || mod.Groups[groupIdx].Count < IModGroup.MaxMultiOptions;
            var validName   = _newOptionName.Length > 0 && _newOptionNameIdx == groupIdx;
            var tt = canAddGroup
                ? validName ? "Add a new option to this group." : "Please enter a name for the new option."
                : $"Can not add more than {IModGroup.MaxMultiOptions} options to a multi group.";
            if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconButtonSize,
                    tt, !(canAddGroup && validName), true))
                return;

            panel._modManager.OptionEditor.AddOption(mod, groupIdx, _newOptionName);
            _newOptionName = string.Empty;
        }

        // Handle drag and drop to move options inside a group or into another group.
        private static void Source(IModGroup group, int groupIdx, int optionIdx)
        {
            using var source = ImRaii.DragDropSource();
            if (!source)
                return;

            if (ImGui.SetDragDropPayload(DragDropLabel, IntPtr.Zero, 0))
            {
                _dragDropGroupIdx  = groupIdx;
                _dragDropOptionIdx = optionIdx;
            }

            ImGui.TextUnformatted($"Dragging option {group[optionIdx].Name} from group {group.Name}...");
        }

        private static void Target(ModPanelEditTab panel, IModGroup group, int groupIdx, int optionIdx)
        {
            using var target = ImRaii.DragDropTarget();
            if (!target.Success || !ImGuiUtil.IsDropping(DragDropLabel))
                return;

            if (_dragDropGroupIdx >= 0 && _dragDropOptionIdx >= 0)
            {
                if (_dragDropGroupIdx == groupIdx)
                {
                    var sourceOption = _dragDropOptionIdx;
                    panel._delayedActions.Enqueue(
                        () => panel._modManager.OptionEditor.MoveOption(panel._mod, groupIdx, sourceOption, optionIdx));
                }
                else
                {
                    // Move from one group to another by deleting, then adding, then moving the option.
                    var sourceGroupIdx = _dragDropGroupIdx;
                    var sourceOption   = _dragDropOptionIdx;
                    var sourceGroup    = panel._mod.Groups[sourceGroupIdx];
                    var currentCount   = group.Count;
                    var option         = sourceGroup[sourceOption];
                    var priority       = sourceGroup.OptionPriority(_dragDropOptionIdx);
                    panel._delayedActions.Enqueue(() =>
                    {
                        panel._modManager.OptionEditor.DeleteOption(panel._mod, sourceGroupIdx, sourceOption);
                        panel._modManager.OptionEditor.AddOption(panel._mod, groupIdx, option, priority);
                        panel._modManager.OptionEditor.MoveOption(panel._mod, groupIdx, currentCount, optionIdx);
                    });
                }
            }

            _dragDropGroupIdx  = -1;
            _dragDropOptionIdx = -1;
        }
    }

    /// <summary> Draw a combo to select single or multi group and switch between them. </summary>
    private void DrawGroupCombo(IModGroup group, int groupIdx)
    {
        static string GroupTypeName(GroupType type)
            => type switch
            {
                GroupType.Single => "Single Group",
                GroupType.Multi  => "Multi Group",
                _                => "Unknown",
            };

        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X - 3 * (UiHelpers.IconButtonSize.X - 4 * UiHelpers.Scale));
        using var combo = ImRaii.Combo("##GroupType", GroupTypeName(group.Type));
        if (!combo)
            return;

        if (ImGui.Selectable(GroupTypeName(GroupType.Single), group.Type == GroupType.Single))
            _modManager.OptionEditor.ChangeModGroupType(_mod, groupIdx, GroupType.Single);

        var       canSwitchToMulti = group.Count <= IModGroup.MaxMultiOptions;
        using var style            = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.5f, !canSwitchToMulti);
        if (ImGui.Selectable(GroupTypeName(GroupType.Multi), group.Type == GroupType.Multi) && canSwitchToMulti)
            _modManager.OptionEditor.ChangeModGroupType(_mod, groupIdx, GroupType.Multi);

        style.Pop();
        if (!canSwitchToMulti)
            ImGuiUtil.HoverTooltip($"Can not convert group to multi group since it has more than {IModGroup.MaxMultiOptions} options.");
    }

    /// <summary> Handles input text and integers in separate fields without buffers for every single one. </summary>
    private static class Input
    {
        // Special field indices to reuse the same string buffer.
        public const int None        = -1;
        public const int Name        = -2;
        public const int Author      = -3;
        public const int Version     = -4;
        public const int Website     = -5;
        public const int Path        = -6;
        public const int Description = -7;

        // Temporary strings
        private static string? _currentEdit;
        private static int?    _currentGroupPriority;
        private static int     _currentField = None;
        private static int     _optionIndex  = None;

        public static void Reset()
        {
            _currentEdit          = null;
            _currentGroupPriority = null;
            _currentField         = None;
            _optionIndex          = None;
        }

        public static bool Text(string label, int field, int option, string oldValue, out string value, uint maxLength, float width)
        {
            var tmp = field == _currentField && option == _optionIndex ? _currentEdit ?? oldValue : oldValue;
            ImGui.SetNextItemWidth(width);
            if (ImGui.InputText(label, ref tmp, maxLength))
            {
                _currentEdit  = tmp;
                _optionIndex  = option;
                _currentField = field;
            }

            if (ImGui.IsItemDeactivatedAfterEdit() && _currentEdit != null)
            {
                var ret = _currentEdit != oldValue;
                value = _currentEdit;
                Reset();
                return ret;
            }

            value = string.Empty;
            return false;
        }

        public static bool Priority(string label, int field, int option, int oldValue, out int value, float width)
        {
            var tmp = field == _currentField && option == _optionIndex ? _currentGroupPriority ?? oldValue : oldValue;
            ImGui.SetNextItemWidth(width);
            if (ImGui.InputInt(label, ref tmp, 0, 0))
            {
                _currentGroupPriority = tmp;
                _optionIndex          = option;
                _currentField         = field;
            }

            if (ImGui.IsItemDeactivatedAfterEdit() && _currentGroupPriority != null)
            {
                var ret = _currentGroupPriority != oldValue;
                value = _currentGroupPriority.Value;
                Reset();
                return ret;
            }

            value = 0;
            return false;
        }
    }
}
