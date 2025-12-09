using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using OtterGui.Widgets;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Services;
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
    : ITab<ModPanelTab>
{
    private readonly TagButtons _modTags = new();

    private ModFileSystem.Leaf _leaf = null!;
    private Mod                _mod  = null!;

    public ReadOnlySpan<byte> Label
        => "Edit Mod"u8;

    public ModPanelTab Identifier
        => ModPanelTab.Edit;

    public void DrawContent()
    {
        using var child = Im.Child.Begin("##editChild"u8, Im.ContentRegion.Available);
        if (!child)
            return;

        _leaf = selector.SelectedLeaf!;
        _mod  = selector.Selected!;

        EditButtons();
        EditRegularMeta();
        UiHelpers.DefaultLineSpace();
        EditLocalData();
        UiHelpers.DefaultLineSpace();

        if (Input.Text("Mod Path"u8, Input.Path, Input.None, _leaf.FullName(), out var newPath, UiHelpers.InputTextWidth.X))
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
        var sharedTagButtonOffset = sharedTagsEnabled ? Im.Style.FrameHeight + Im.Style.FramePadding.X : 0;
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
        if (ImEx.Button("Open Mod Directory"u8, buttonSize, folderExists
                ? $"Open \"{_mod.ModPath.FullName}\" in the file explorer of your choice."
                : $"Mod directory \"{_mod.ModPath.FullName}\" does not exist.", !folderExists))
            Process.Start(new ProcessStartInfo(_mod.ModPath.FullName) { UseShellExecute = true });

        Im.Line.Same();
        if (ImEx.Button("Reload Mod"u8, buttonSize, "Reload the current mod from its files.\n"u8
              + "If the mod directory or meta file do not exist anymore or if the new mod name is empty, the mod is deleted instead."u8,
                false))
            modManager.ReloadMod(_mod);

        BackupButtons(buttonSize);
        MoveDirectory.Draw(modManager, _mod, buttonSize);

        UiHelpers.DefaultLineSpace();
    }

    private void BackupButtons(Vector2 buttonSize)
    {
        var backup = new ModBackup(modExportManager, _mod);
        if (ImEx.Button("Export Mod"u8, buttonSize, ModBackup.CreatingBackup
                ? "Already exporting a mod."
                : backup.Exists
                    ? $"Overwrite current exported mod \"{backup.Name}\" with current mod."
                    : $"Create exported archive of current mod at \"{backup.Name}\".", ModBackup.CreatingBackup))
            backup.CreateAsync();

        if (Im.Item.RightClicked())
            Im.Popup.Open("context"u8);

        Im.Line.Same();
        if (ImEx.Button("Delete Export"u8, buttonSize, backup.Exists
                ? $"Delete existing mod export \"{backup.Name}\" (hold {config.DeleteModModifier} while clicking)."
                : $"Exported mod \"{backup.Name}\" does not exist.", !backup.Exists || !config.DeleteModModifier.IsActive()))
            backup.Delete();

        Im.Line.Same();
        if (ImEx.Button("Restore From Export"u8, buttonSize, backup.Exists
                ? $"Restore mod from exported file \"{backup.Name}\" (hold {config.DeleteModModifier} while clicking)."
                : $"Exported mod \"{backup.Name}\" does not exist.", !backup.Exists || !config.DeleteModModifier.IsActive()))
            backup.Restore(modManager);
        if (backup.Exists)
        {
            Im.Line.Same();
            ImEx.Icon.Draw(FontAwesomeIcon.CheckCircle.Icon());
            Im.Tooltip.OnHover($"Export exists in \"{backup.Name}\".");
        }

        using var context = Im.Popup.Begin("context"u8);
        if (!context)
            return;

        if (Im.Selectable("Open Backup Directory"u8))
            Process.Start(new ProcessStartInfo(modExportManager.ExportDirectory.FullName) { UseShellExecute = true });
    }

    /// <summary> Anything about editing the regular meta information about the mod. </summary>
    private void EditRegularMeta()
    {
        if (Input.Text("Name"u8, Input.Name, Input.None, _mod.Name, out var newName, UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModName(_mod, newName);

        if (Input.Text("Author"u8, Input.Author, Input.None, _mod.Author, out var newAuthor, UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModAuthor(_mod, newAuthor);

        if (Input.Text("Version"u8, Input.Version, Input.None, _mod.Version, out var newVersion,
                UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModVersion(_mod, newVersion);

        if (Input.Text("Website"u8, Input.Website, Input.None, _mod.Website, out var newWebsite,
                UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModWebsite(_mod, newWebsite);

        using var style = ImStyleDouble.ItemSpacing.Push(new Vector2(Im.Style.GlobalScale * 3));

        var reducedSize = new Vector2(UiHelpers.InputTextMinusButton3, 0);
        if (Im.Button("Edit Description"u8, reducedSize))
            descriptionPopup.Open(_mod);


        Im.Line.Same();
        var fileExists = File.Exists(filenames.ModMetaPath(_mod));
        var tt = fileExists
            ? "Open the metadata json file in the text editor of your choice."u8
            : "The metadata json file does not exist."u8;
        using (Im.Id.Push("meta"u8))
        {
            if (ImEx.Icon.Button(LunaStyle.FileExportIcon, tt, !fileExists))
                Process.Start(new ProcessStartInfo(filenames.ModMetaPath(_mod)) { UseShellExecute = true });
        }

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
        Im.Line.Same(0, 3 * Im.Style.GlobalScale);

        var canRefresh = config.DeleteModModifier.IsActive();
        if (ImEx.Icon.Button(LunaStyle.RefreshIcon, canRefresh
                    ? "Reset the import date to the current date and time."u8
                    : $"Reset the import date to the current date and time.\nHold {config.DeleteModModifier} while clicking to refresh.",
                !canRefresh))
            modManager.DataEditor.ResetModImportDate(_mod);
        Im.Line.SameInner();
        Im.Text("Import Date"u8);
    }

    private void DrawOpenLocalData()
    {
        var file       = filenames.LocalDataFile(_mod);
        var fileExists = File.Exists(file);
        var tt = fileExists
            ? "Open the local mod data file in the text editor of your choice."u8
            : "The local mod data file does not exist."u8;
        if (ImEx.Button("Open Local Data"u8, UiHelpers.InputTextWidth, tt, !fileExists))
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    }

    private void DrawOpenDefaultMod()
    {
        var file       = filenames.OptionGroupFile(_mod, -1, false);
        var fileExists = File.Exists(file);
        var tt = fileExists
            ? "Open the default mod data file in the text editor of your choice."u8
            : "The default mod data file does not exist."u8;
        if (ImEx.Button("Open Default Data"u8, UiHelpers.InputTextWidth, tt, !fileExists))
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
            Im.Item.SetNextWidth(buttonSize.X * 2 + Im.Style.ItemSpacing.X);
            var tmp = _currentModDirectory ?? mod.ModPath.Name;
            if (Im.Input.Text("##newModMove"u8, ref tmp))
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
            if (ImEx.Button("Rename Mod Directory"u8, buttonSize, tt, disabled) && _currentModDirectory is not null)
            {
                modManager.MoveModDirectory(mod, _currentModDirectory);
                Reset();
            }

            Im.Line.SameInner();
            LunaStyle.DrawAlignedHelpMarker(StringU8.Empty,
                "The mod directory name is used to correspond stored settings and sort orders, otherwise it has no influence on anything that is displayed.\n"u8
              + "This can currently not be used on pre-existing folders and does not support merges or overwriting."u8);
        }
    }

    /// <summary> Handles input text and integers in separate fields without buffers for every single one. </summary>
    private static class Input
    {
        // Special field indices to reuse the same string buffer.
        public const int None    = -1;
        public const int Name    = -2;
        public const int Author  = -3;
        public const int Version = -4;
        public const int Website = -5;
        public const int Path    = -6;

        // Temporary strings
        private static string? _currentEdit;
        private static int     _currentField = None;
        private static int     _optionIndex  = None;

        public static void Reset()
        {
            _currentEdit  = null;
            _currentField = None;
            _optionIndex  = None;
        }

        public static bool Text(ReadOnlySpan<byte> label, int field, int option, string oldValue, out string value, float width)
        {
            var tmp = field == _currentField && option == _optionIndex ? _currentEdit ?? oldValue : oldValue;
            Im.Item.SetNextWidth(width);

            if (Im.Input.Text(label, ref tmp))
            {
                _currentEdit  = tmp;
                _optionIndex  = option;
                _currentField = field;
            }

            if (Im.Item.DeactivatedAfterEdit && _currentEdit is not null)
            {
                var ret = _currentEdit != oldValue;
                value = _currentEdit;
                Reset();
                return ret;
            }

            value = string.Empty;
            return false;
        }
    }
}
