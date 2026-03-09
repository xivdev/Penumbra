using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;
using Penumbra.UI.FileEditing;

namespace Penumbra.UI.AdvancedWindow;

public sealed class FileEditor(
    ModEditWindow owner,
    CommunicatorService communicator,
    Configuration config,
    FileCompactor compactor,
    FileDialogService fileDialog,
    IFramework framework,
    string tabName,
    string fileType,
    Func<IEnumerable<FileRegistry>> getFiles,
    Func<string> getInitialPath,
    FileEditorRegistry fileEditorRegistry,
    Func<FileEditingContext> getFileEditingContext)
    : IDisposable
{
    public void Draw()
    {
        using var tab = Im.TabBar.BeginItem(tabName);
        if (!tab)
        {
            _quickImport = null;
            return;
        }

        Im.Line.New();
        DrawFileSelectCombo();
        SaveButton();
        Im.Line.Same();
        ResetButton();
        Im.Line.Same();
        RedrawOnSaveBox();
        Im.Line.Same();
        DefaultInput();
        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));

        DrawFilePanel();
    }

    private void RedrawOnSaveBox()
    {
        var redraw = config.Ephemeral.ForceRedrawOnFileChange;
        if (Im.Checkbox("Redraw on Save"u8, ref redraw))
        {
            config.Ephemeral.ForceRedrawOnFileChange = redraw;
            config.Ephemeral.Save();
        }

        Im.Tooltip.OnHover("Force a redraw of your player character whenever you save a file here."u8);
    }

    public void Dispose()
    {
        ClearCurrentFile();
        ClearDefaultFile();
    }

    private void ClearCurrentFile()
    {
        _currentFile?.SaveRequested -= SaveFile;
        _currentFile?.Dispose();
        _currentFile = null;
    }

    private void ClearDefaultFile()
    {
        _defaultFile?.Dispose();
        _defaultFile = null;
    }

    private FileRegistry? CurrentPath
    {
        get => _combo.Selected;
        set => _combo.Selected = value;
    }

    private IFileEditor? _currentFile;
    private Exception?   _currentException;
    private bool         _changed;

    private string       _defaultPath = fileType is ".pbd" ? GamePaths.Pbd.Path : string.Empty;
    private bool         _inInput;
    private Utf8GamePath _defaultPathUtf8;
    private bool         _isDefaultPathUtf8Valid;
    private IFileEditor? _defaultFile;
    private Exception?   _defaultException;

    private readonly Combo _combo = new(getFiles);

    private ModEditWindow.QuickImportAction? _quickImport;

    private void DefaultInput()
    {
        using var spacing = ImStyleDouble.ItemSpacing.PushX(Im.Style.GlobalScale * 3);
        Im.Item.SetNextWidth(Im.ContentRegion.Available.X - 2 * (Im.Style.GlobalScale * 3 + Im.Style.FrameHeight));
        Im.Input.Text("##defaultInput"u8, ref _defaultPath, "Input game path to compare..."u8, maxLength: Utf8GamePath.MaxGamePathLength);
        _inInput = Im.Item.Active;
        if (Im.Item.DeactivatedAfterEdit && _defaultPath.Length > 0)
        {
            _isDefaultPathUtf8Valid = Utf8GamePath.FromString(_defaultPath, out _defaultPathUtf8);
            _quickImport            = null;
            fileDialog.Reset();
            _defaultException = null;
            ClearDefaultFile(); // Avoid double disposal if an exception occurs during the parsing of the new file.
            try
            {
                _defaultFile = fileEditorRegistry.CreateForGameFile(_defaultPath, getFileEditingContext());
            }
            catch (Exception e)
            {
                _defaultException = e;
            }
        }

        Im.Line.Same();
        if (ImEx.Icon.Button(LunaStyle.SaveIcon, "Export this file."u8, _defaultFile is null))
            fileDialog.OpenSavePicker($"Export {_defaultPath} to...", fileType, Path.GetFileNameWithoutExtension(_defaultPath), fileType,
                async void (success, name) =>
                {
                    if (!success)
                        return;

                    try
                    {
                        if (_defaultFile is null)
                            throw new Exception("File invalid.");

                        await compactor.WriteAllBytesAsync(name, await _defaultFile.WriteAsync());
                    }
                    catch (Exception e)
                    {
                        Penumbra.Messager.NotificationMessage(e, $"Could not export {_defaultPath}.", NotificationType.Error);
                    }
                }, getInitialPath(), false);

        _quickImport ??=
            ModEditWindow.QuickImportAction.Prepare(owner, _isDefaultPathUtf8Valid ? _defaultPathUtf8 : Utf8GamePath.Empty, _defaultFile);
        Im.Line.Same();
        if (ImEx.Icon.Button(LunaStyle.ImportIcon, $"Add a copy of this file to {_quickImport.OptionName}.", !_quickImport.CanExecute))
        {
            try
            {
                UpdateCurrentFile(_quickImport.Execute());
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not add a copy of {_quickImport.GamePath} to {_quickImport.OptionName}:\n{e}");
            }

            _quickImport = null;
        }
    }

    public void Reset()
    {
        _currentException = null;
        CurrentPath       = null;
        ClearCurrentFile();
        _changed = false;
    }

    private void DrawFileSelectCombo()
    {
        if (CurrentPath is not null)
        {
            if (_combo.Draw("##select"u8, CurrentPath.RelPath.Path.Span, StringU8.Empty, Im.ContentRegion.Available.X, out var newSelection))
                UpdateCurrentFile(newSelection);
        }
        else
        {
            if (_combo.Draw("##select"u8, $"Select {fileType} File...", StringU8.Empty, Im.ContentRegion.Available.X, out var newSelection))
                UpdateCurrentFile(newSelection);
        }
    }

    private void UpdateCurrentFile(FileRegistry path)
    {
        if (ReferenceEquals(CurrentPath, path))
            return;

        _changed          = false;
        CurrentPath       = path;
        _currentException = null;
        ClearCurrentFile(); // Avoid double disposal if an exception occurs during the parsing of the new file.
        try
        {
            _currentFile = fileEditorRegistry.CreateForFile(CurrentPath.File.FullName, true, null, getFileEditingContext());
        }
        catch (Exception e)
        {
            _currentException = e;
        }

        _currentFile?.SaveRequested += SaveFile;
    }

    private void SaveButton()
    {
        var canSave = _changed && _currentFile is { Valid: true };
        if (ImEx.Button("Save to File"u8, Vector2.Zero,
                $"Save the selected {fileType} file with all changes applied. This is not revertible.", !canSave))
            SaveFile();
    }

    public void SaveFile()
    {
        SaveFileAsync().ContinueWith(t => t.Exception!.Handle(e =>
        {
            Penumbra.Messager.NotificationMessage(e, $"Could not save {CurrentPath?.File.FullName}.", NotificationType.Error);
            return true;
        }), TaskContinuationOptions.OnlyOnFaulted);
    }

    public async Task SaveFileAsync()
    {
        await compactor.WriteAllBytesAsync(CurrentPath!.File.FullName, await _currentFile!.WriteAsync());
        if (owner.Mod is not null)
            await framework.Run(() => communicator.ModFileChanged.Invoke(new ModFileChanged.Arguments(owner.Mod, CurrentPath)));
        _changed = false;
    }

    private void ResetButton()
    {
        if (ImEx.Button("Reset Changes"u8, Vector2.Zero,
                $"Reset all changes made to the {fileType} file.", !_changed))
        {
            var tmp = CurrentPath;
            CurrentPath = null;
            UpdateCurrentFile(tmp!);
        }
    }

    private void DrawFilePanel()
    {
        using var child = Im.Child.Begin("##filePanel"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        if (CurrentPath is not null)
        {
            if (_currentFile is null)
            {
                Im.Text($"Could not parse selected {fileType} file.");
                if (_currentException is not null)
                {
                    using var tab = Im.Indent();
                    Im.TextWrapped($"{_currentException}");
                }
            }
            else
            {
                using var id = Im.Id.Push(0);
                _changed |= _currentFile.DrawToolbar(false);
                _changed |= _currentFile.DrawPanel(false);
            }
        }

        if (!_inInput && _defaultPath.Length > 0)
        {
            if (CurrentPath is not null)
            {
                Im.Line.New();
                Im.Line.New();
                Im.Text($"Preview of {_defaultPath}:");
                Im.Separator();
            }

            if (_defaultFile == null)
            {
                Im.Text($"Could not parse provided {fileType} game file:\n");
                if (_defaultException is not null)
                {
                    using var tab = Im.Indent();
                    Im.TextWrapped($"{_defaultException}");
                }
            }
            else
            {
                using var id = Im.Id.Push(1);
                _defaultFile.DrawToolbar(true);
                _defaultFile.DrawPanel(true);
            }
        }
    }

    private sealed class Combo : FilterComboBase<FileRegistry>
    {
        private sealed class FileFilter : RegexFilterBase<FileRegistry>
        {
            // TODO: Avoid ToString.
            public override bool WouldBeVisible(in FileRegistry item, int globalIndex)
                => WouldBeVisible(item.File.FullName) || item.SubModUsage.Any(f => WouldBeVisible(f.Item2.ToString()));

            /// <summary> Unused. </summary>
            protected override string ToFilterString(in FileRegistry item, int globalIndex)
                => string.Empty;
        }

        private readonly Func<IEnumerable<FileRegistry>> _getFiles;

        public FileRegistry? Selected;

        public Combo(Func<IEnumerable<FileRegistry>> getFiles)
        {
            _getFiles                = getFiles;
            Filter                   = new FileFilter();
            DirtyCacheOnClosingPopup = true;
        }

        protected override IEnumerable<FileRegistry> GetItems()
            => _getFiles();

        protected override float ItemHeight
            => Im.Style.TextHeightWithSpacing;

        protected override bool DrawItem(in FileRegistry item, int globalIndex, bool selected)
        {
            bool ret;
            using (ImGuiColor.Text.Push(ColorId.HandledConflictMod.Value(), item.IsOnPlayer))
            {
                ret = Im.Selectable(item.RelPath.Path.Span, selected);
            }

            if (Im.Item.Hovered())
            {
                using var style = Im.Style.PushDefault(ImStyleDouble.WindowPadding);
                using var tt    = Im.Tooltip.Begin();
                Im.Text("All Game Paths"u8);
                Im.Separator();
                using var t = Im.Table.Begin("##Tooltip"u8, 2, TableFlags.SizingFixedFit);
                if (t)
                    foreach (var (option, gamePath) in item.SubModUsage)
                    {
                        t.DrawColumn(gamePath.Path.Span);
                        using var color = ImGuiColor.Text.Push(ColorId.ItemId.Value());
                        t.DrawColumn(option.GetFullName());
                    }
            }

            if (item.SubModUsage.Count > 0)
            {
                Im.Line.Same();
                using var color = ImGuiColor.Text.Push(ColorId.ItemId.Value());
                ImEx.TextRightAligned($"{item.SubModUsage[0].Item2.Path}");
            }

            return ret;
        }

        protected override bool IsSelected(FileRegistry item, int globalIndex)
            => item.Equals(Selected);
    }
}
