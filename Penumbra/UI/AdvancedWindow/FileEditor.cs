using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Text;
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
        using var tab = ImRaii.TabItem(tabName);
        if (!tab)
        {
            _quickImport = null;
            return;
        }

        ImGui.NewLine();
        DrawFileSelectCombo();
        SaveButton();
        Im.Line.Same();
        ResetButton();
        Im.Line.Same();
        RedrawOnSaveBox();
        Im.Line.Same();
        DefaultInput();
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));

        DrawFilePanel();
    }

    private void RedrawOnSaveBox()
    {
        var redraw = config.Ephemeral.ForceRedrawOnFileChange;
        if (ImGui.Checkbox("Redraw on Save", ref redraw))
        {
            config.Ephemeral.ForceRedrawOnFileChange = redraw;
            config.Ephemeral.Save();
        }

        ImGuiUtil.HoverTooltip("Force a redraw of your player character whenever you save a file here.");
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

    private readonly Combo _combo = new(config, getFiles);

    private ModEditWindow.QuickImportAction? _quickImport;

    private void DefaultInput()
    {
        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { X = UiHelpers.ScaleX3 });
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 2 * (UiHelpers.ScaleX3 + ImGui.GetFrameHeight()));
        ImGui.InputTextWithHint("##defaultInput", "Input game path to compare...", ref _defaultPath, Utf8GamePath.MaxGamePathLength);
        _inInput = ImGui.IsItemActive();
        if (ImGui.IsItemDeactivatedAfterEdit() && _defaultPath.Length > 0)
        {
            _isDefaultPathUtf8Valid = Utf8GamePath.FromString(_defaultPath, out _defaultPathUtf8);
            _quickImport            = null;
            fileDialog.Reset();
            try
            {
                var file = gameData.GetFile(_defaultPath);
                if (file != null)
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
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Save.ToIconString(), new Vector2(ImGui.GetFrameHeight()), "Export this file.",
                _defaultFile == null, true))
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
        if (ImGuiUtil.DrawDisabledButton("Save to File", Vector2.Zero,
                $"Save the selected {fileType} file with all changes applied. This is not revertible.", !canSave))
            SaveFile();
    }

    public void SaveFile()
    {
        compactor.WriteAllBytes(_currentPath!.File.FullName, _currentFile!.Write());
        if (owner.Mod != null)
            communicator.ModFileChanged.Invoke(new ModFileChanged.Arguments(owner.Mod, _currentPath));
        _changed = false;
    }

    private void ResetButton()
    {
        if (ImGuiUtil.DrawDisabledButton("Reset Changes", Vector2.Zero,
                $"Reset all changes made to the {fileType} file.", !_changed))
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
                ImGui.TextUnformatted($"Could not parse selected {fileType} file.");
                if (_currentException != null)
                {
                    using var tab = ImRaii.PushIndent();
                    ImGuiUtil.TextWrapped(_currentException.ToString());
                }
            }
            else
            {
                using var id = ImRaii.PushId(0);
                _changed |= drawEdit(_currentFile, false);
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
                ImGui.TextUnformatted($"Could not parse provided {fileType} game file:\n");
                if (_defaultException != null)
                {
                    using var tab = ImRaii.PushIndent();
                    ImGuiUtil.TextWrapped(_defaultException.ToString());
                }
            }
            else
            {
                using var id = ImRaii.PushId(1);
                drawEdit(_defaultFile, true);
            }
        }
    }

    private class Combo : FilterComboCache<FileRegistry>
    {
        private readonly Configuration _config;

        public Combo(Configuration config, Func<IReadOnlyList<FileRegistry>> generator)
            : base(generator, MouseWheelType.None, Penumbra.Log)
            => _config = config;

        protected override bool DrawSelectable(int globalIdx, bool selected)
        {
            var  file = Items[globalIdx];
            bool ret;
            using (var c = ImGuiColor.Text.Push(ColorId.HandledConflictMod.Value(), file.IsOnPlayer))
            {
                ret = ImGui.Selectable(file.RelPath.ToString(), selected);
            }

            if (ImGui.IsItemHovered())
            {
                using var tt = ImRaii.Tooltip();
                ImGui.TextUnformatted("All Game Paths");
                ImGui.Separator();
                using var t = ImRaii.Table("##Tooltip", 2, ImGuiTableFlags.SizingFixedFit);
                foreach (var (option, gamePath) in file.SubModUsage)
                {
                    ImGui.TableNextColumn();
                    ImUtf8.Text(gamePath.Path.Span);
                    ImGui.TableNextColumn();
                    using var color = ImGuiColor.Text.Push(ColorId.ItemId.Value());
                    ImGui.TextUnformatted(option.GetFullName());
                }
            }

            if (file.SubModUsage.Count > 0)
            {
                Im.Line.Same();
                using var color = ImGuiColor.Text.Push(ColorId.ItemId.Value());
                ImGuiUtil.RightAlign(file.SubModUsage[0].Item2.Path.ToString());
            }

            return ret;
        }

        protected override bool IsVisible(int globalIndex, LowerString filter)
            => filter.IsContained(Items[globalIndex].File.FullName)
             || Items[globalIndex].SubModUsage.Any(f => filter.IsContained(f.Item2.ToString()));
    }
}
