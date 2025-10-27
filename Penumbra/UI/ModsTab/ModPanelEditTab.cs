using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;
using ImSharp;
using Luna;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using OtterGui.Text;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.Mods.Settings;
using Penumbra.UI.ModsTab.Groups;

namespace Penumbra.UI.ModsTab;

public class ModPanelEditTab(
    ModManager modManager,
    ModFileSystemSelector selector,
    ModFileSystem fileSystem,
    Services.MessageService messager,
    FilenameService filenames,
    ModExportManager modExportManager,
    Configuration config,
    PredefinedTagManager predefinedTagManager,
    ModGroupEditDrawer groupEditDrawer,
    DescriptionEditPopup descriptionPopup,
    AddGroupDrawer addGroupDrawer)
    : ITab, IUiService
{
    private readonly TagButtons _modTags = new();

    private ModFileSystem.Leaf _leaf = null!;
    private Mod                _mod  = null!;

    public ReadOnlySpan<byte> Label
        => "Edit Mod"u8;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##editChild", -Vector2.One);
        if (!child)
            return;

        _leaf = selector.SelectedLeaf!;
        _mod  = selector.Selected!;

        EditButtons();
        EditRegularMeta();
        UiHelpers.DefaultLineSpace();
        EditLocalData();
        UiHelpers.DefaultLineSpace();

        if (Input.Text("Mod Path", Input.Path, Input.None, _leaf.FullName(), out var newPath, 256, UiHelpers.InputTextWidth.X))
            try
            {
                fileSystem.RenameAndMove(_leaf, newPath);
            }
            catch (Exception e)
            {
                messager.NotificationMessage(e.Message, NotificationType.Warning, false);
            }

        UiHelpers.DefaultLineSpace();

        FeatureChecker.DrawFeatureFlagInput(modManager.DataEditor, _mod, UiHelpers.InputTextWidth.X);

        UiHelpers.DefaultLineSpace();
        var sharedTagsEnabled     = predefinedTagManager.Enabled;
        var sharedTagButtonOffset = sharedTagsEnabled ? Im.Style.FrameHeight + ImGui.GetStyle().FramePadding.X : 0;
        var tagIdx = _modTags.Draw("Mod Tags: ", "Edit tags by clicking them, or add new tags. Empty tags are removed.", _mod.ModTags,
            out var editedTag, rightEndOffset: sharedTagButtonOffset);
        if (tagIdx >= 0)
            modManager.DataEditor.ChangeModTag(_mod, tagIdx, editedTag);

        if (sharedTagsEnabled)
            predefinedTagManager.DrawAddFromSharedTagsAndUpdateTags(selector.Selected!.LocalTags, selector.Selected!.ModTags, false,
                selector.Selected!);


        UiHelpers.DefaultLineSpace();
        addGroupDrawer.Draw(_mod, UiHelpers.InputTextWidth.X);
        UiHelpers.DefaultLineSpace();

        groupEditDrawer.Draw(_mod);
        descriptionPopup.Draw();
    }

    public void Reset()
    {
        MoveDirectory.Reset();
        Input.Reset();
    }

    /// <summary> The general edit row for non-detailed mod edits. </summary>
    private void EditButtons()
    {
        var buttonSize   = new Vector2(150 * Im.Style.GlobalScale, 0);
        var folderExists = Directory.Exists(_mod.ModPath.FullName);
        var tt = folderExists
            ? $"Open \"{_mod.ModPath.FullName}\" in the file explorer of your choice."
            : $"Mod directory \"{_mod.ModPath.FullName}\" does not exist.";
        if (ImGuiUtil.DrawDisabledButton("Open Mod Directory", buttonSize, tt, !folderExists))
            Process.Start(new ProcessStartInfo(_mod.ModPath.FullName) { UseShellExecute = true });

        Im.Line.Same();
        if (ImGuiUtil.DrawDisabledButton("Reload Mod", buttonSize, "Reload the current mod from its files.\n"
              + "If the mod directory or meta file do not exist anymore or if the new mod name is empty, the mod is deleted instead.",
                false))
            modManager.ReloadMod(_mod);

        BackupButtons(buttonSize);
        MoveDirectory.Draw(modManager, _mod, buttonSize);

        UiHelpers.DefaultLineSpace();
    }

    private void BackupButtons(Vector2 buttonSize)
    {
        var backup = new ModBackup(modExportManager, _mod);
        var tt = ModBackup.CreatingBackup
            ? "Already exporting a mod."
            : backup.Exists
                ? $"Overwrite current exported mod \"{backup.Name}\" with current mod."
                : $"Create exported archive of current mod at \"{backup.Name}\".";
        if (ImUtf8.ButtonEx("Export Mod"u8, tt, buttonSize, ModBackup.CreatingBackup))
            backup.CreateAsync();

        if (Im.Item.RightClicked())
            ImUtf8.OpenPopup("context"u8);

        Im.Line.Same();
        tt = backup.Exists
            ? $"Delete existing mod export \"{backup.Name}\" (hold {config.DeleteModModifier} while clicking)."
            : $"Exported mod \"{backup.Name}\" does not exist.";
        if (ImUtf8.ButtonEx("Delete Export"u8, tt, buttonSize, !backup.Exists || !config.DeleteModModifier.IsActive()))
            backup.Delete();

        tt = backup.Exists
            ? $"Restore mod from exported file \"{backup.Name}\" (hold {config.DeleteModModifier} while clicking)."
            : $"Exported mod \"{backup.Name}\" does not exist.";
        Im.Line.Same();
        if (ImUtf8.ButtonEx("Restore From Export"u8, tt, buttonSize, !backup.Exists || !config.DeleteModModifier.IsActive()))
            backup.Restore(modManager);
        if (backup.Exists)
        {
            Im.Line.Same();
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImUtf8.Text(FontAwesomeIcon.CheckCircle.ToIconString());
            }

            ImUtf8.HoverTooltip($"Export exists in \"{backup.Name}\".");
        }

        using var context = ImUtf8.Popup("context"u8);
        if (!context)
            return;

        if (ImUtf8.Selectable("Open Backup Directory"u8))
            Process.Start(new ProcessStartInfo(modExportManager.ExportDirectory.FullName) { UseShellExecute = true });
    }

    /// <summary> Anything about editing the regular meta information about the mod. </summary>
    private void EditRegularMeta()
    {
        if (Input.Text("Name", Input.Name, Input.None, _mod.Name, out var newName, 256, UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModName(_mod, newName);

        if (Input.Text("Author", Input.Author, Input.None, _mod.Author, out var newAuthor, 256, UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModAuthor(_mod, newAuthor);

        if (Input.Text("Version", Input.Version, Input.None, _mod.Version, out var newVersion, 32,
                UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModVersion(_mod, newVersion);

        if (Input.Text("Website", Input.Website, Input.None, _mod.Website, out var newWebsite, 256,
                UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModWebsite(_mod, newWebsite);

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(Im.Style.GlobalScale * 3));

        var reducedSize = new Vector2(UiHelpers.InputTextMinusButton3, 0);
        if (ImGui.Button("Edit Description", reducedSize))
            descriptionPopup.Open(_mod);


        Im.Line.Same();
        var fileExists = File.Exists(filenames.ModMetaPath(_mod));
        var tt = fileExists
            ? "Open the metadata json file in the text editor of your choice."
            : "The metadata json file does not exist.";
        if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.FileExport.ToIconString()}##metaFile", UiHelpers.IconButtonSize, tt,
                !fileExists, true))
            Process.Start(new ProcessStartInfo(filenames.ModMetaPath(_mod)) { UseShellExecute = true });

        DrawOpenDefaultMod();
    }

    private void EditLocalData()
    {
        DrawImportDate();
        DrawOpenLocalData();
    }

    private void DrawImportDate()
    {
        ImEx.TextFramed($"{DateTimeOffset.FromUnixTimeMilliseconds(_mod.ImportDate).ToLocalTime():yyyy/MM/dd HH:mm}",
            new Vector2(UiHelpers.InputTextMinusButton3, 0), ImGuiColor.FrameBackground.Get(0.5f));
        ImGui.SameLine(0, 3 * ImUtf8.GlobalScale);

        var canRefresh = config.DeleteModModifier.IsActive();
        var tt = canRefresh
            ? "Reset the import date to the current date and time."
            : $"Reset the import date to the current date and time.\nHold {config.DeleteModModifier} while clicking to refresh.";

        if (ImUtf8.IconButton(FontAwesomeIcon.Sync, tt, disabled: !canRefresh))
            modManager.DataEditor.ResetModImportDate(_mod);
        ImUtf8.SameLineInner();
        ImUtf8.Text("Import Date"u8);
    }

    private void DrawOpenLocalData()
    {
        var file       = filenames.LocalDataFile(_mod);
        var fileExists = File.Exists(file);
        var tt = fileExists
            ? "Open the local mod data file in the text editor of your choice."u8
            : "The local mod data file does not exist."u8;
        if (ImUtf8.ButtonEx("Open Local Data"u8, tt, UiHelpers.InputTextWidth, !fileExists))
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    }

    private void DrawOpenDefaultMod()
    {
        var file       = filenames.OptionGroupFile(_mod, -1, false);
        var fileExists = File.Exists(file);
        var tt = fileExists
            ? "Open the default mod data file in the text editor of your choice."
            : "The default mod data file does not exist.";
        if (ImGuiUtil.DrawDisabledButton("Open Default Data", UiHelpers.InputTextWidth, tt, !fileExists))
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
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
            Im.Line.Same();
            if (ImGuiUtil.DrawDisabledButton("Rename Mod Directory", buttonSize, tt, disabled) && _currentModDirectory != null)
            {
                modManager.MoveModDirectory(mod, _currentModDirectory);
                Reset();
            }

            Im.Line.Same();
            ImGuiComponents.HelpMarker(
                "The mod directory name is used to correspond stored settings and sort orders, otherwise it has no influence on anything that is displayed.\n"
              + "This can currently not be used on pre-existing folders and does not support merges or overwriting.");
        }
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
        private static string?      _currentEdit;
        private static ModPriority? _currentGroupPriority;
        private static int          _currentField = None;
        private static int          _optionIndex  = None;

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

            if (ImGui.InputText(label, ref tmp))
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

        public static bool Priority(string label, int field, int option, ModPriority oldValue, out ModPriority value, float width)
        {
            var tmp = (field == _currentField && option == _optionIndex ? _currentGroupPriority ?? oldValue : oldValue).Value;
            ImGui.SetNextItemWidth(width);
            if (ImGui.InputInt(label, ref tmp, 0, 0))
            {
                _currentGroupPriority = new ModPriority(tmp);
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

            value = ModPriority.Default;
            return false;
        }
    }
}
