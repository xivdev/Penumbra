using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class FileEditor<T>(
    ModEditWindow owner,
    CommunicatorService communicator,
    IDataManager gameData,
    Configuration config,
    FileCompactor compactor,
    FileDialogService fileDialog,
    string tabName,
    string fileType,
    Func<IEnumerable<FileRegistry>> getFiles,
    Func<T, bool, bool> drawEdit,
    Func<string> getInitialPath,
    Func<byte[], string, bool, T?> parseFile)
    : IDisposable
    where T : class, IWritable
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
        (_currentFile as IDisposable)?.Dispose();
        _currentFile = null;
        (_defaultFile as IDisposable)?.Dispose();
        _defaultFile = null;
    }

    private FileRegistry? CurrentPath
    {
        get => _combo.Selected;
        set => _combo.Selected = value;
    }

    private T?         _currentFile;
    private Exception? _currentException;
    private bool       _changed;

    private string       _defaultPath = typeof(T) == typeof(ModEditWindow.PbdTab) ? GamePaths.Pbd.Path : string.Empty;
    private bool         _inInput;
    private Utf8GamePath _defaultPathUtf8;
    private bool         _isDefaultPathUtf8Valid;
    private T?           _defaultFile;
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
            try
            {
                var file = gameData.GetFile(_defaultPath);
                if (file is not null)
                {
                    _defaultException = null;
                    (_defaultFile as IDisposable)?.Dispose();
                    _defaultFile = null; // Avoid double disposal if an exception occurs during the parsing of the new file.
                    _defaultFile = parseFile(file.Data, _defaultPath, false);
                }
                else
                {
                    _defaultFile      = null;
                    _defaultException = new Exception("File does not exist.");
                }
            }
            catch (Exception e)
            {
                _defaultFile      = null;
                _defaultException = e;
            }
        }

        Im.Line.Same();
        if (ImEx.Icon.Button(LunaStyle.SaveIcon, "Export this file."u8, _defaultFile is null))
            fileDialog.OpenSavePicker($"Export {_defaultPath} to...", fileType, Path.GetFileNameWithoutExtension(_defaultPath), fileType,
                (success, name) =>
                {
                    if (!success)
                        return;

                    try
                    {
                        compactor.WriteAllBytes(name, _defaultFile?.Write() ?? throw new Exception("File invalid."));
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
        (_currentFile as IDisposable)?.Dispose();
        _currentFile = null;
        _changed     = false;
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
        try
        {
            var bytes = File.ReadAllBytes(CurrentPath.File.FullName);
            (_currentFile as IDisposable)?.Dispose();
            _currentFile = null; // Avoid double disposal if an exception occurs during the parsing of the new file.
            _currentFile = parseFile(bytes, CurrentPath.File.FullName, true);
        }
        catch (Exception e)
        {
            (_currentFile as IDisposable)?.Dispose();
            _currentFile      = null;
            _currentException = e;
        }
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
        compactor.WriteAllBytes(CurrentPath!.File.FullName, _currentFile!.Write());
        if (owner.Mod is not null)
            communicator.ModFileChanged.Invoke(new ModFileChanged.Arguments(owner.Mod, CurrentPath));
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
                _changed |= drawEdit(_currentFile, false);
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
                drawEdit(_defaultFile, true);
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
            _getFiles = getFiles;
            Filter    = new FileFilter();
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
