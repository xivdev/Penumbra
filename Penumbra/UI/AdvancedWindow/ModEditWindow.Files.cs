using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.Mods;
using Penumbra.Mods.Subclasses;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly HashSet<FileRegistry> _selectedFiles = new(256);
    private          LowerString           _fileFilter    = LowerString.Empty;
    private          bool                  _showGamePaths = true;
    private          string                _gamePathEdit  = string.Empty;
    private          int                   _fileIdx       = -1;
    private          int                   _pathIdx       = -1;
    private          int                   _folderSkip;
    private          bool                  _overviewMode;

    private LowerString _fileOverviewFilter1 = LowerString.Empty;
    private LowerString _fileOverviewFilter2 = LowerString.Empty;
    private LowerString _fileOverviewFilter3 = LowerString.Empty;

    private bool CheckFilter(FileRegistry registry)
        => _fileFilter.IsEmpty || registry.File.FullName.Contains(_fileFilter.Lower, StringComparison.OrdinalIgnoreCase);

    private bool CheckFilter((FileRegistry, int) p)
        => CheckFilter(p.Item1);

    private void DrawFileTab()
    {
        using var tab = ImRaii.TabItem("File Redirections");
        if (!tab)
            return;

        DrawOptionSelectHeader();
        DrawButtonHeader();

        if (_overviewMode)
            DrawFileManagementOverview();
        else
            DrawFileManagementNormal();

        using var child = ImRaii.Child("##files", -Vector2.One, true);
        if (!child)
            return;

        if (_overviewMode)
            DrawFilesOverviewMode();
        else
            DrawFilesNormalMode();
    }

    private void DrawFilesOverviewMode()
    {
        var height = ImGui.GetTextLineHeightWithSpacing() + 2 * ImGui.GetStyle().CellPadding.Y;
        var skips  = ImGuiClip.GetNecessarySkips(height);

        using var list = ImRaii.Table("##table", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV, -Vector2.One);

        if (!list)
            return;

        var width = ImGui.GetContentRegionAvail().X / 8;

        ImGui.TableSetupColumn("##file",   ImGuiTableColumnFlags.WidthFixed, width * 3);
        ImGui.TableSetupColumn("##path",   ImGuiTableColumnFlags.WidthFixed, width * 3 + ImGui.GetStyle().FrameBorderSize);
        ImGui.TableSetupColumn("##option", ImGuiTableColumnFlags.WidthFixed, width * 2);

        var idx = 0;

        var files = _editor.Files.Available.SelectMany(f =>
        {
            var file = f.RelPath.ToString();
            return f.SubModUsage.Count == 0
                ? Enumerable.Repeat((file, "Unused", string.Empty, 0x40000080u), 1)
                : f.SubModUsage.Select(s => (file, s.Item2.ToString(), s.Item1.FullName,
                    _editor.Option! == s.Item1 && _mod!.HasOptions ? 0x40008000u : 0u));
        });

        void DrawLine((string, string, string, uint) data)
        {
            using var id = ImRaii.PushId(idx++);
            ImGui.TableNextColumn();
            if (data.Item4 != 0)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, data.Item4);

            ImGuiUtil.CopyOnClickSelectable(data.Item1);
            ImGui.TableNextColumn();
            if (data.Item4 != 0)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, data.Item4);

            ImGuiUtil.CopyOnClickSelectable(data.Item2);
            ImGui.TableNextColumn();
            if (data.Item4 != 0)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, data.Item4);

            ImGuiUtil.CopyOnClickSelectable(data.Item3);
        }

        bool Filter((string, string, string, uint) data)
            => _fileOverviewFilter1.IsContained(data.Item1)
             && _fileOverviewFilter2.IsContained(data.Item2)
             && _fileOverviewFilter3.IsContained(data.Item3);

        var end = ImGuiClip.FilteredClippedDraw(files, skips, Filter, DrawLine);
        ImGuiClip.DrawEndDummy(end, height);
    }

    private void DrawFilesNormalMode()
    {
        using var list = ImRaii.Table("##table", 1);

        if (!list)
            return;

        foreach (var (registry, i) in _editor.Files.Available.WithIndex().Where(CheckFilter))
        {
            using var id = ImRaii.PushId(i);
            ImGui.TableNextColumn();

            DrawSelectable(registry);

            if (!_showGamePaths)
                continue;

            using var indent = ImRaii.PushIndent(50f);
            for (var j = 0; j < registry.SubModUsage.Count; ++j)
            {
                var (subMod, gamePath) = registry.SubModUsage[j];
                if (subMod != _editor.Option)
                    continue;

                PrintGamePath(i, j, registry, subMod, gamePath);
            }

            PrintNewGamePath(i, registry, _editor.Option!);
        }
    }

    private static string DrawFileTooltip(FileRegistry registry, ColorId color)
    {
        (string, int) GetMulti()
        {
            var groups = registry.SubModUsage.GroupBy(s => s.Item1).ToArray();
            return (string.Join("\n", groups.Select(g => g.Key.Name)), groups.Length);
        }

        var (text, groupCount) = color switch
        {
            ColorId.ConflictingMod => (string.Empty, 0),
            ColorId.NewMod         => (registry.SubModUsage[0].Item1.Name, 1),
            ColorId.InheritedMod   => GetMulti(),
            _                      => (string.Empty, 0),
        };

        if (text.Length > 0 && ImGui.IsItemHovered())
            ImGui.SetTooltip(text);


        return (groupCount, registry.SubModUsage.Count) switch
        {
            (0, 0)   => "(unused)",
            (1, 1)   => "(used 1 time)",
            (1, > 1) => $"(used {registry.SubModUsage.Count} times in 1 group)",
            _        => $"(used {registry.SubModUsage.Count} times over {groupCount} groups)",
        };
    }

    private void DrawSelectable(FileRegistry registry)
    {
        var selected = _selectedFiles.Contains(registry);
        var color = registry.SubModUsage.Count == 0             ? ColorId.ConflictingMod :
            registry.CurrentUsage == registry.SubModUsage.Count ? ColorId.NewMod : ColorId.InheritedMod;
        using var c = ImRaii.PushColor(ImGuiCol.Text, color.Value());
        if (UiHelpers.Selectable(registry.RelPath.Path, selected))
        {
            if (selected)
                _selectedFiles.Remove(registry);
            else
                _selectedFiles.Add(registry);
        }

        var rightText = DrawFileTooltip(registry, color);

        ImGui.SameLine();
        ImGuiUtil.RightAlign(rightText);
    }

    private void PrintGamePath(int i, int j, FileRegistry registry, ISubMod subMod, Utf8GamePath gamePath)
    {
        using var id = ImRaii.PushId(j);
        ImGui.TableNextColumn();
        var tmp = _fileIdx == i && _pathIdx == j ? _gamePathEdit : gamePath.ToString();
        var pos = ImGui.GetCursorPosX() - ImGui.GetFrameHeight();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText(string.Empty, ref tmp, Utf8GamePath.MaxGamePathLength))
        {
            _fileIdx      = i;
            _pathIdx      = j;
            _gamePathEdit = tmp;
        }

        ImGuiUtil.HoverTooltip("Clear completely to remove the path from this mod.");

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (Utf8GamePath.FromString(_gamePathEdit, out var path, false))
                _editor.FileEditor.SetGamePath(_editor.Option!, _fileIdx, _pathIdx, path);

            _fileIdx = -1;
            _pathIdx = -1;
        }
        else if (_fileIdx == i
              && _pathIdx == j
              && (!Utf8GamePath.FromString(_gamePathEdit, out var path, false)
                  || !path.IsEmpty && !path.Equals(gamePath) && !_editor.FileEditor.CanAddGamePath(path)))
        {
            ImGui.SameLine();
            ImGui.SetCursorPosX(pos);
            using var font = ImRaii.PushFont(UiBuilder.IconFont);
            ImGuiUtil.TextColored(0xFF0000FF, FontAwesomeIcon.TimesCircle.ToIconString());
        }
    }

    private void PrintNewGamePath(int i, FileRegistry registry, ISubMod subMod)
    {
        var tmp = _fileIdx == i && _pathIdx == -1 ? _gamePathEdit : string.Empty;
        var pos = ImGui.GetCursorPosX() - ImGui.GetFrameHeight();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##new", "Add New Path...", ref tmp, Utf8GamePath.MaxGamePathLength))
        {
            _fileIdx      = i;
            _pathIdx      = -1;
            _gamePathEdit = tmp;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (Utf8GamePath.FromString(_gamePathEdit, out var path, false) && !path.IsEmpty)
                _editor.FileEditor.SetGamePath(_editor.Option!, _fileIdx, _pathIdx, path);

            _fileIdx = -1;
            _pathIdx = -1;
        }
        else if (_fileIdx == i
              && _pathIdx == -1
              && (!Utf8GamePath.FromString(_gamePathEdit, out var path, false)
                  || !path.IsEmpty && !_editor.FileEditor.CanAddGamePath(path)))
        {
            ImGui.SameLine();
            ImGui.SetCursorPosX(pos);
            using var font = ImRaii.PushFont(UiBuilder.IconFont);
            ImGuiUtil.TextColored(0xFF0000FF, FontAwesomeIcon.TimesCircle.ToIconString());
        }
    }

    private void DrawButtonHeader()
    {
        ImGui.NewLine();

        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3 * UiHelpers.Scale, 0));
        ImGui.SetNextItemWidth(30 * UiHelpers.Scale);
        ImGui.DragInt("##skippedFolders", ref _folderSkip, 0.01f, 0, 10);
        ImGuiUtil.HoverTooltip("Skip the first N folders when automatically constructing the game path from the file path.");
        ImGui.SameLine();
        spacing.Pop();
        if (ImGui.Button("Add Paths"))
            _editor.FileEditor.AddPathsToSelected(_editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains), _folderSkip);

        ImGuiUtil.HoverTooltip(
            "Add the file path converted to a game path to all selected files for the current option, optionally skipping the first N folders.");


        ImGui.SameLine();
        if (ImGui.Button("Remove Paths"))
            _editor.FileEditor.RemovePathsFromSelected(_editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains));

        ImGuiUtil.HoverTooltip("Remove all game paths associated with the selected files in the current option.");


        ImGui.SameLine();
        var active = _config.DeleteModModifier.IsActive();
        var tt =
            "Delete all selected files entirely from your filesystem, but not their file associations in the mod.\n!!!This can not be reverted!!!";
        if (_selectedFiles.Count == 0)
            tt += "\n\nNo files selected.";
        else if (!active)
            tt += $"\n\nHold {_config.DeleteModModifier} to delete.";

        if (ImGuiUtil.DrawDisabledButton("Delete Selected Files", Vector2.Zero, tt, _selectedFiles.Count == 0 || !active))
            _editor.FileEditor.DeleteFiles(_editor.Mod!, _editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains));

        ImGui.SameLine();
        var changes = _editor.FileEditor.Changes;
        tt = changes ? "Apply the current file setup to the currently selected option." : "No changes made.";
        if (ImGuiUtil.DrawDisabledButton("Apply Changes", Vector2.Zero, tt, !changes))
        {
            var failedFiles = _editor.FileEditor.Apply(_editor.Mod!, (SubMod)_editor.Option!);
            if (failedFiles > 0)
                Penumbra.Log.Information($"Failed to apply {failedFiles} file redirections to {_editor.Option!.FullName}.");
        }


        ImGui.SameLine();
        var label  = changes ? "Revert Changes" : "Reload Files";
        var length = new Vector2(ImGui.CalcTextSize("Revert Changes").X, 0);
        if (ImGui.Button(label, length))
            _editor.FileEditor.Revert(_editor.Mod!, _editor.Option!);

        ImGuiUtil.HoverTooltip("Revert all revertible changes since the last file or option reload or data refresh.");

        ImGui.SameLine();
        ImGui.Checkbox("Overview Mode", ref _overviewMode);
    }

    private void DrawFileManagementNormal()
    {
        ImGui.SetNextItemWidth(250 * UiHelpers.Scale);
        LowerString.InputWithHint("##filter", "Filter paths...", ref _fileFilter, Utf8GamePath.MaxGamePathLength);
        ImGui.SameLine();
        ImGui.Checkbox("Show Game Paths", ref _showGamePaths);
        ImGui.SameLine();
        if (ImGui.Button("Unselect All"))
            _selectedFiles.Clear();

        ImGui.SameLine();
        if (ImGui.Button("Select Visible"))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(CheckFilter));

        ImGui.SameLine();
        if (ImGui.Button("Select Unused"))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(f => f.SubModUsage.Count == 0));

        ImGui.SameLine();
        if (ImGui.Button("Select Used Here"))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(f => f.CurrentUsage > 0));

        ImGui.SameLine();

        ImGuiUtil.RightAlign($"{_selectedFiles.Count} / {_editor.Files.Available.Count} Files Selected");
    }

    private void DrawFileManagementOverview()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0)
            .Push(ImGuiStyleVar.ItemSpacing,     Vector2.Zero)
            .Push(ImGuiStyleVar.FrameBorderSize, ImGui.GetStyle().ChildBorderSize);

        var width = ImGui.GetContentRegionAvail().X / 8;

        ImGui.SetNextItemWidth(width * 3);
        LowerString.InputWithHint("##fileFilter", "Filter file...", ref _fileOverviewFilter1, Utf8GamePath.MaxGamePathLength);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(width * 3);
        LowerString.InputWithHint("##pathFilter", "Filter path...", ref _fileOverviewFilter2, Utf8GamePath.MaxGamePathLength);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(width * 2);
        LowerString.InputWithHint("##optionFilter", "Filter option...", ref _fileOverviewFilter3, Utf8GamePath.MaxGamePathLength);
    }
}
