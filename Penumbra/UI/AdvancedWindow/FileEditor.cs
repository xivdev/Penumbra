using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Data;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.GameData.Files;
using Penumbra.Mods;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class FileEditor<T> where T : class, IWritable
{
    private readonly FileDialogService _fileDialog;
    private readonly DataManager       _gameData;
    private readonly ModEditWindow     _owner;

    public FileEditor(ModEditWindow owner, DataManager gameData, Configuration config, FileDialogService fileDialog, string tabName,
        string fileType, Func<IReadOnlyList<FileRegistry>> getFiles, Func<T, bool, bool> drawEdit, Func<string> getInitialPath,
        Func<byte[], T?> parseFile)
    {
        _owner          = owner;
        _gameData       = gameData;
        _fileDialog     = fileDialog;
        _tabName        = tabName;
        _fileType       = fileType;
        _drawEdit       = drawEdit;
        _getInitialPath = getInitialPath;
        _parseFile      = parseFile;
        _combo          = new Combo(config, getFiles);
    }

    public void Draw()
    {
        using var tab = ImRaii.TabItem(_tabName);
        if (!tab)
        {
            _quickImport = null;
            return;
        }

        ImGui.NewLine();
        DrawFileSelectCombo();
        SaveButton();
        ImGui.SameLine();
        ResetButton();
        ImGui.SameLine();
        DefaultInput();
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));

        DrawFilePanel();
    }

    private readonly string              _tabName;
    private readonly string              _fileType;
    private readonly Func<T, bool, bool> _drawEdit;
    private readonly Func<string>        _getInitialPath;
    private readonly Func<byte[], T?>    _parseFile;

    private FileRegistry? _currentPath;
    private T?            _currentFile;
    private Exception?    _currentException;
    private bool          _changed;

    private string       _defaultPath = string.Empty;
    private bool         _inInput;
    private Utf8GamePath _defaultPathUtf8;
    private bool         _isDefaultPathUtf8Valid;
    private T?           _defaultFile;
    private Exception?   _defaultException;

    private readonly Combo _combo;

    private ModEditWindow.QuickImportAction? _quickImport;

    private void DefaultInput()
    {
        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { X = UiHelpers.ScaleX3 });
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 2 * (UiHelpers.ScaleX3 + ImGui.GetFrameHeight()));
        ImGui.InputTextWithHint("##defaultInput", "Input game path to compare...", ref _defaultPath, Utf8GamePath.MaxGamePathLength);
        _inInput = ImGui.IsItemActive();
        if (ImGui.IsItemDeactivatedAfterEdit() && _defaultPath.Length > 0)
        {
            _isDefaultPathUtf8Valid = Utf8GamePath.FromString(_defaultPath, out _defaultPathUtf8, true);
            _quickImport            = null;
            _fileDialog.Reset();
            try
            {
                var file = _gameData.GetFile(_defaultPath);
                if (file != null)
                {
                    _defaultException = null;
                    _defaultFile      = _parseFile(file.Data);
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

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Save.ToIconString(), new Vector2(ImGui.GetFrameHeight()), "Export this file.",
                _defaultFile == null, true))
            _fileDialog.OpenSavePicker($"Export {_defaultPath} to...", _fileType, Path.GetFileNameWithoutExtension(_defaultPath), _fileType,
                (success, name) =>
                {
                    if (!success)
                        return;

                    try
                    {
                        File.WriteAllBytes(name, _defaultFile?.Write() ?? throw new Exception("File invalid."));
                    }
                    catch (Exception e)
                    {
                        Penumbra.Chat.NotificationMessage($"Could not export {_defaultPath}:\n{e}", "Error", NotificationType.Error);
                    }
                }, _getInitialPath(), false);

        _quickImport ??=
            ModEditWindow.QuickImportAction.Prepare(_owner, _isDefaultPathUtf8Valid ? _defaultPathUtf8 : Utf8GamePath.Empty, _defaultFile);
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileImport.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
                $"Add a copy of this file to {_quickImport.OptionName}.", !_quickImport.CanExecute, true))
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

        _fileDialog.Draw();
    }

    public void Reset()
    {
        _currentException = null;
        _currentPath      = null;
        _currentFile      = null;
        _changed          = false;
    }

    private void DrawFileSelectCombo()
    {
        if (_combo.Draw("##fileSelect", _currentPath?.RelPath.ToString() ?? $"Select {_fileType} File...", string.Empty,
                ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight())
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
            _currentFile = _parseFile(bytes);
        }
        catch (Exception e)
        {
            _currentFile      = null;
            _currentException = e;
        }
    }

    private void SaveButton()
    {
        if (ImGuiUtil.DrawDisabledButton("Save to File", Vector2.Zero,
                $"Save the selected {_fileType} file with all changes applied. This is not revertible.", !_changed))
        {
            File.WriteAllBytes(_currentPath!.File.FullName, _currentFile!.Write());
            _changed = false;
        }
    }

    private void ResetButton()
    {
        if (ImGuiUtil.DrawDisabledButton("Reset Changes", Vector2.Zero,
                $"Reset all changes made to the {_fileType} file.", !_changed))
        {
            var tmp = _currentPath;
            _currentPath = null;
            UpdateCurrentFile(tmp!);
        }
    }

    private void DrawFilePanel()
    {
        using var child = ImRaii.Child("##filePanel", -Vector2.One, true);
        if (!child)
            return;

        if (_currentPath != null)
        {
            if (_currentFile == null)
            {
                ImGui.TextUnformatted($"Could not parse selected {_fileType} file.");
                if (_currentException != null)
                {
                    using var tab = ImRaii.PushIndent();
                    ImGuiUtil.TextWrapped(_currentException.ToString());
                }
            }
            else
            {
                using var id = ImRaii.PushId(0);
                _changed |= _drawEdit(_currentFile, false);
            }
        }

        if (!_inInput && _defaultPath.Length > 0)
        {
            if (_currentPath != null)
            {
                ImGui.NewLine();
                ImGui.NewLine();
                ImGui.TextUnformatted($"Preview of {_defaultPath}:");
                ImGui.Separator();
            }

            if (_defaultFile == null)
            {
                ImGui.TextUnformatted($"Could not parse provided {_fileType} game file:\n");
                if (_defaultException != null)
                {
                    using var tab = ImRaii.PushIndent();
                    ImGuiUtil.TextWrapped(_defaultException.ToString());
                }
            }
            else
            {
                using var id = ImRaii.PushId(1);
                _drawEdit(_defaultFile, true);
            }
        }
    }

    private class Combo : FilterComboCache<FileRegistry>
    {
        private readonly Configuration _config;

        public Combo(Configuration config, Func<IReadOnlyList<FileRegistry>> generator)
            : base(generator)
            => _config = config;

        protected override bool DrawSelectable(int globalIdx, bool selected)
        {
            var file = Items[globalIdx];
            var ret  = ImGui.Selectable(file.RelPath.ToString(), selected);

            if (ImGui.IsItemHovered())
            {
                using var tt = ImRaii.Tooltip();
                ImGui.TextUnformatted("All Game Paths");
                ImGui.Separator();
                using var t = ImRaii.Table("##Tooltip", 2, ImGuiTableFlags.SizingFixedFit);
                foreach (var (option, gamePath) in file.SubModUsage)
                {
                    ImGui.TableNextColumn();
                    UiHelpers.Text(gamePath.Path);
                    ImGui.TableNextColumn();
                    using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.ItemId.Value());
                    ImGui.TextUnformatted(option.FullName);
                }
            }

            if (file.SubModUsage.Count > 0)
            {
                ImGui.SameLine();
                using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.ItemId.Value());
                ImGuiUtil.RightAlign(file.SubModUsage[0].Item2.Path.ToString());
            }

            return ret;
        }

        protected override bool IsVisible(int globalIndex, LowerString filter)
            => filter.IsContained(Items[globalIndex].File.FullName)
             || Items[globalIndex].SubModUsage.Any(f => filter.IsContained(f.Item2.ToString()));
    }
}
