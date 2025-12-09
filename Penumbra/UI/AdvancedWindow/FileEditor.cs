using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using OtterGui.Classes;
using OtterGui.Widgets;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;
using MouseWheelType = OtterGui.Widgets.MouseWheelType;

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
    Func<IReadOnlyList<FileRegistry>> getFiles,
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

    private FileRegistry? _currentPath;
    private T?            _currentFile;
    private Exception?    _currentException;
    private bool          _changed;

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
        _currentPath      = null;
        (_currentFile as IDisposable)?.Dispose();
        _currentFile = null;
        _changed     = false;
    }

    private void DrawFileSelectCombo()
    {
        if (_combo.Draw("##fileSelect", _currentPath?.RelPath.ToString() ?? $"Select {fileType} File...", string.Empty,
                Im.ContentRegion.Available.X, Im.Style.TextHeight)
         && _combo.CurrentSelection != null)
            UpdateCurrentFile(_combo.CurrentSelection);
    }

    private void UpdateCurrentFile(FileRegistry path)
    {
        if (ReferenceEquals(_currentPath, path))
            return;

        _changed          = false;
        _currentPath      = path;
        _currentException = null;
        try
        {
            var bytes = File.ReadAllBytes(_currentPath.File.FullName);
            (_currentFile as IDisposable)?.Dispose();
            _currentFile = null; // Avoid double disposal if an exception occurs during the parsing of the new file.
            _currentFile = parseFile(bytes, _currentPath.File.FullName, true);
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
        compactor.WriteAllBytes(_currentPath!.File.FullName, _currentFile!.Write());
        if (owner.Mod is not null)
            communicator.ModFileChanged.Invoke(new ModFileChanged.Arguments(owner.Mod, _currentPath));
        _changed = false;
    }

    private void ResetButton()
    {
        if (ImEx.Button("Reset Changes"u8, Vector2.Zero,
                $"Reset all changes made to the {fileType} file.", !_changed))
        {
            var tmp = _currentPath;
            _currentPath = null;
            UpdateCurrentFile(tmp!);
        }
    }

    private void DrawFilePanel()
    {
        using var child = Im.Child.Begin("##filePanel"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        if (_currentPath is not null)
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
            if (_currentPath is not null)
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

    private class Combo(Func<IReadOnlyList<FileRegistry>> generator)
        : FilterComboCache<FileRegistry>(generator, MouseWheelType.None, Penumbra.Log)
    {
        protected override bool DrawSelectable(int globalIdx, bool selected)
        {
            var  file = Items[globalIdx];
            bool ret;
            using (ImGuiColor.Text.Push(ColorId.HandledConflictMod.Value(), file.IsOnPlayer))
            {
                ret = Im.Selectable(file.RelPath.ToString(), selected);
            }

            if (Im.Item.Hovered())
            {
                using var tt = Im.Tooltip.Begin();
                Im.Text("All Game Paths"u8);
                Im.Separator();
                using var t = Im.Table.Begin("##Tooltip"u8, 2, TableFlags.SizingFixedFit);
                if (t)
                {
                    foreach (var (option, gamePath) in file.SubModUsage)
                    {
                        t.DrawColumn(gamePath.Path.Span);
                        using var color = ImGuiColor.Text.Push(ColorId.ItemId.Value());
                        t.DrawColumn(option.GetFullName());
                    }
                }
            }

            if (file.SubModUsage.Count > 0)
            {
                Im.Line.Same();
                using var color = ImGuiColor.Text.Push(ColorId.ItemId.Value());
                ImEx.TextRightAligned($"{file.SubModUsage[0].Item2.Path}");
            }

            return ret;
        }

        protected override bool IsVisible(int globalIndex, LowerString filter)
            => filter.IsContained(Items[globalIndex].File.FullName)
             || Items[globalIndex].SubModUsage.Any(f => filter.IsContained(f.Item2.ToString()));
    }
}
