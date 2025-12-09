using Dalamud.Interface;
using ImSharp;
using Luna;
using OtterGui;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly HashSet<FileRegistry> _selectedFiles = new(256);
    private readonly HashSet<Utf8GamePath> _cutPaths      = [];
    private          string                _fileFilter    = string.Empty;
    private          bool                  _showGamePaths = true;
    private          string                _gamePathEdit  = string.Empty;
    private          int                   _fileIdx       = -1;
    private          int                   _pathIdx       = -1;
    private          int                   _folderSkip;
    private          bool                  _overviewMode;

    private string _fileOverviewFilter1 = string.Empty;
    private string _fileOverviewFilter2 = string.Empty;
    private string _fileOverviewFilter3 = string.Empty;

    private bool CheckFilter(FileRegistry registry)
        => _fileFilter.Length is 0 || registry.File.FullName.Contains(_fileFilter, StringComparison.OrdinalIgnoreCase);

    private bool CheckFilter((int, FileRegistry) p)
        => CheckFilter(p.Item2);

    private void DrawFileTab()
    {
        using var tab = Im.TabBar.BeginItem("File Redirections"u8);
        if (!tab)
            return;

        DrawOptionSelectHeader();
        DrawButtonHeader();

        if (_overviewMode)
            DrawFileManagementOverview();
        else
            DrawFileManagementNormal();

        using var child = Im.Child.Begin("##files"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        if (_overviewMode)
            DrawFilesOverviewMode();
        else
            DrawFilesNormalMode();
    }

    private void DrawFilesOverviewMode()
    {
        var height = Im.Style.TextHeightWithSpacing + 2 * Im.Style.CellPadding.Y;
        var skips  = ImGuiClip.GetNecessarySkips(height);

        using var table = Im.Table.Begin("##table"u8, 3, TableFlags.RowBackground | TableFlags.BordersInnerVertical, Im.ContentRegion.Available);

        if (!table)
            return;

        var width = Im.ContentRegion.Available.X / 8;

        table.SetupColumn("##file"u8,   TableColumnFlags.WidthFixed, width * 3);
        table.SetupColumn("##path"u8,   TableColumnFlags.WidthFixed, width * 3 + Im.Style.FrameBorderThickness);
        table.SetupColumn("##option"u8, TableColumnFlags.WidthFixed, width * 2);

        var idx = 0;

        var files = _editor.Files.Available.SelectMany(f =>
        {
            var file = f.RelPath.ToString();
            return f.SubModUsage.Count == 0
                ? Enumerable.Repeat((file, "Unused", string.Empty, 0x40000080u), 1)
                : f.SubModUsage.Select(s => (file, s.Item2.ToString(), s.Item1.GetFullName(),
                    _editor.Option! == s.Item1 && Mod!.HasOptions ? 0x40008000u : 0u));
        });

        void DrawLine((string, string, string, uint) data)
        {
            using var id = Im.Id.Push(idx++);
            if (data.Item4 is not 0)
                Im.Table.SetBackgroundColor(TableBackgroundTarget.Cell, data.Item4);

            ImEx.CopyOnClickSelectable(data.Item1);
            Im.Table.NextColumn();
            if (data.Item4 is not 0)
                Im.Table.SetBackgroundColor(TableBackgroundTarget.Cell, data.Item4);

            ImEx.CopyOnClickSelectable(data.Item2);
            Im.Table.NextColumn();
            if (data.Item4 is not 0)
                Im.Table.SetBackgroundColor(TableBackgroundTarget.Cell, data.Item4);

            ImEx.CopyOnClickSelectable(data.Item3);
        }

        bool Filter((string, string, string, uint) data)
            => data.Item1.Contains(_fileOverviewFilter1, StringComparison.OrdinalIgnoreCase)
             && data.Item2.Contains(_fileOverviewFilter2, StringComparison.OrdinalIgnoreCase)
             && data.Item3.Contains(_fileOverviewFilter3, StringComparison.OrdinalIgnoreCase);

        var end = ImGuiClip.FilteredClippedDraw(files, skips, Filter, DrawLine);
        ImGuiClip.DrawEndDummy(end, height);
    }

    private void DrawFilesNormalMode()
    {
        using var table = Im.Table.Begin("##table"u8, 1);
        if (!table)
            return;

        foreach (var (i, registry) in _editor.Files.Available.Index().Where(CheckFilter))
        {
            using var id = Im.Id.Push(i);
            table.NextColumn();

            DrawSelectable(registry, i);

            if (!_showGamePaths)
                continue;

            using var indent = Im.Indent(50f);
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
        var (text, groupCount) = color switch
        {
            ColorId.ConflictingMod => (null, 0),
            ColorId.NewMod         => ([registry.SubModUsage[0].Item1.GetName()], 1),
            ColorId.InheritedMod   => GetMulti(),
            _                      => (null, 0),
        };

        if (text is not null && Im.Item.Hovered())
        {
            using var tt = Im.Tooltip.Begin();
            using var c  = ImGuiColor.Text.PushDefault();
            Im.Text(StringU8.Join((byte) '\n', text));
        }


        return (groupCount, registry.SubModUsage.Count) switch
        {
            (0, 0)   => "(unused)",
            (1, 1)   => "(used 1 time)",
            (1, > 1) => $"(used {registry.SubModUsage.Count} times in 1 group)",
            _        => $"(used {registry.SubModUsage.Count} times over {groupCount} groups)",
        };

        (IEnumerable<string>, int) GetMulti()
        {
            var groups = registry.SubModUsage.GroupBy(s => s.Item1).ToArray();
            return (groups.Select(g => g.Key.GetName()), groups.Length);
        }
    }

    private void DrawSelectable(FileRegistry registry, int i)
    {
        var selected = _selectedFiles.Contains(registry);
        var color = registry.SubModUsage.Count == 0             ? ColorId.ConflictingMod :
            registry.CurrentUsage == registry.SubModUsage.Count ? ColorId.NewMod : ColorId.InheritedMod;
        using (ImGuiColor.Text.Push(color.Value()))
        {
            if (Im.Selectable(registry.RelPath.Path.Span, selected))
            {
                if (selected)
                    _selectedFiles.Remove(registry);
                else
                    _selectedFiles.Add(registry);
            }

            if (Im.Item.RightClicked())
                Im.Popup.Open("context"u8);

            var rightText = DrawFileTooltip(registry, color);

            Im.Line.Same();
            ImEx.TextRightAligned(rightText);
        }

        DrawContextMenu(registry, i);
    }

    private void DrawContextMenu(FileRegistry registry, int i)
    {
        using var context = Im.Popup.Begin("context"u8);
        if (!context)
            return;

        if (Im.Selectable("Copy Full File Path"u8))
            Im.Clipboard.Set(registry.File.FullName);

        using (Im.Disabled(registry.CurrentUsage is 0))
        {
            if (Im.Selectable("Copy Game Paths"u8))
            {
                _cutPaths.Clear();
                for (var j = 0; j < registry.SubModUsage.Count; ++j)
                {
                    if (registry.SubModUsage[j].Item1 != _editor.Option)
                        continue;

                    _cutPaths.Add(registry.SubModUsage[j].Item2);
                }
            }
        }

        using (Im.Disabled(registry.CurrentUsage is 0))
        {
            if (Im.Selectable("Cut Game Paths"u8))
            {
                _cutPaths.Clear();
                for (var j = 0; j < registry.SubModUsage.Count; ++j)
                {
                    if (registry.SubModUsage[j].Item1 != _editor.Option)
                        continue;

                    _cutPaths.Add(registry.SubModUsage[j].Item2);
                    _editor.FileEditor.SetGamePath(_editor.Option, i, j--, Utf8GamePath.Empty);
                }
            }
        }

        using (Im.Disabled(_cutPaths.Count is 0))
        {
            if (Im.Selectable("Paste Game Paths"u8))
                foreach (var path in _cutPaths)
                    _editor.FileEditor.SetGamePath(_editor.Option!, i, -1, path);
        }
    }

    private void PrintGamePath(int i, int j, FileRegistry registry, IModDataContainer _, Utf8GamePath gamePath)
    {
        using var id = Im.Id.Push(j);
        Im.Table.NextColumn();
        var tmp = _fileIdx == i && _pathIdx == j ? _gamePathEdit : gamePath.ToString();
        var pos = Im.Cursor.X - Im.Style.FrameHeight;
        Im.Item.SetNextWidth(-1);
        if (Im.Input.Text(StringU8.Empty, ref tmp, maxLength:Utf8GamePath.MaxGamePathLength))
        {
            _fileIdx      = i;
            _pathIdx      = j;
            _gamePathEdit = tmp;
        }

        Im.Tooltip.OnHover("Clear completely to remove the path from this mod."u8);

        if (Im.Item.DeactivatedAfterEdit)
        {
            if (Utf8GamePath.FromString(_gamePathEdit, out var path))
                _editor.FileEditor.SetGamePath(_editor.Option!, _fileIdx, _pathIdx, path);

            _fileIdx = -1;
            _pathIdx = -1;
        }
        else if (_fileIdx == i
              && _pathIdx == j
              && (!Utf8GamePath.FromString(_gamePathEdit, out var path)
                  || !path.IsEmpty && !path.Equals(gamePath) && !_editor.FileEditor.CanAddGamePath(path)))
        {
            Im.Line.Same();
            Im.Cursor.X = pos;
            ImEx.Icon.Draw(FontAwesomeIcon.TimesCircle.Icon(), Rgba32.Red);
        }
        else if (tmp.Length > 0 && Path.GetExtension(tmp) != registry.File.Extension)
        {
            Im.Line.Same();
            Im.Cursor.X = pos;
            ImEx.Icon.Draw(FontAwesomeIcon.ExclamationCircle.Icon(), new Rgba32(0xFF00B0B0));
            Im.Tooltip.OnHover("The game path and the file do not have the same extension."u8);
        }
    }

    private void PrintNewGamePath(int i, FileRegistry registry, IModDataContainer _)
    {
        var tmp = _fileIdx == i && _pathIdx == -1 ? _gamePathEdit : string.Empty;
        var pos = Im.Cursor.X - Im.Style.FrameHeight;
        Im.Item.SetNextWidth(-1);
        if (Im.Input.Text("##new"u8, ref tmp, "Add New Path..."u8, maxLength: Utf8GamePath.MaxGamePathLength))
        {
            _fileIdx      = i;
            _pathIdx      = -1;
            _gamePathEdit = tmp;
        }

        if (Im.Item.DeactivatedAfterEdit)
        {
            if (Utf8GamePath.FromString(_gamePathEdit, out var path) && !path.IsEmpty)
                _editor.FileEditor.SetGamePath(_editor.Option!, _fileIdx, _pathIdx, path);

            _fileIdx = -1;
            _pathIdx = -1;
        }
        else if (_fileIdx == i
              && _pathIdx == -1
              && (!Utf8GamePath.FromString(_gamePathEdit, out var path)
                  || !path.IsEmpty && !_editor.FileEditor.CanAddGamePath(path)))
        {
            Im.Line.Same();
            Im.Cursor.X = pos;
            ImEx.Icon.Draw(FontAwesomeIcon.TimesCircle.Icon(), Rgba32.Red);
        }
        else if (tmp.Length > 0 && Path.GetExtension(tmp) != registry.File.Extension)
        {
            Im.Line.Same();
            Im.Cursor.X = pos;
            ImEx.Icon.Draw(FontAwesomeIcon.ExclamationCircle.Icon(), new Rgba32(0xFF00B0B0));
            Im.Tooltip.OnHover("The game path and the file do not have the same extension."u8);
        }
    }

    private void DrawButtonHeader()
    {
        Im.Line.New();

        using var spacing = ImStyleDouble.ItemSpacing.Push(new Vector2(3 * Im.Style.GlobalScale, 0));
        Im.Item.SetNextWidthScaled(30);
        Im.Drag("##skippedFolders"u8, ref _folderSkip, 0, 10, 0.01f);
        Im.Tooltip.OnHover("Skip the first N folders when automatically constructing the game path from the file path."u8);
        Im.Line.Same();
        spacing.Pop();
        if (Im.Button("Add Paths"u8))
            _editor.FileEditor.AddPathsToSelected(_editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains), _folderSkip);

        Im.Tooltip.OnHover("Add the file path converted to a game path to all selected files for the current option, optionally skipping the first N folders."u8);


        Im.Line.Same();
        if (Im.Button("Remove Paths"u8))
            _editor.FileEditor.RemovePathsFromSelected(_editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains));

        Im.Tooltip.OnHover("Remove all game paths associated with the selected files in the current option."u8);


        Im.Line.Same();
        var active = _config.DeleteModModifier.IsActive();
        var tt =
            "Delete all selected files entirely from your filesystem, but not their file associations in the mod.\n!!!This can not be reverted!!!";
        if (_selectedFiles.Count is 0)
            tt += "\n\nNo files selected.";
        else if (!active)
            tt += $"\n\nHold {_config.DeleteModModifier} to delete.";

        if (ImEx.Button("Delete Selected Files"u8, Vector2.Zero, tt, _selectedFiles.Count is 0 || !active))
            _editor.FileEditor.DeleteFiles(_editor.Mod!, _editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains));

        Im.Line.Same();
        var changes = _editor.FileEditor.Changes;
        var tt2 = changes ? "Apply the current file setup to the currently selected option."u8 : "No changes made."u8;
        if (ImEx.Button("Apply Changes"u8, Vector2.Zero, tt2, !changes))
        {
            var failedFiles = _editor.FileEditor.Apply(_editor.Mod!, _editor.Option!);
            if (failedFiles > 0)
                Penumbra.Log.Information($"Failed to apply {failedFiles} file redirections to {_editor.Option!.GetFullName()}.");
        }


        Im.Line.Same();
        var label  = changes ? "Revert Changes"u8 : "Reload Files"u8;
        var length = new Vector2(Im.Font.CalculateSize("Revert Changes"u8).X, 0);
        if (Im.Button(label, length))
            _editor.FileEditor.Revert(_editor.Mod!, _editor.Option!);

        Im.Tooltip.OnHover("Revert all revertible changes since the last file or option reload or data refresh."u8);

        Im.Line.Same();
        Im.Checkbox("Overview Mode"u8, ref _overviewMode);
    }

    private void DrawFileManagementNormal()
    {
        Im.Item.SetNextWidthScaled(250);
        Im.Input.Text("##filter"u8, ref _fileFilter, "Filter paths..."u8);
        Im.Line.Same();
        Im.Checkbox("Show Game Paths"u8, ref _showGamePaths);
        Im.Line.Same();
        if (Im.Button("Unselect All"u8))
            _selectedFiles.Clear();

        Im.Line.Same();
        if (Im.Button("Select Visible"u8))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(CheckFilter));

        Im.Line.Same();
        if (Im.Button("Select Unused"u8))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(f => f.SubModUsage.Count == 0));

        Im.Line.Same();
        if (Im.Button("Select Used Here"u8))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(f => f.CurrentUsage > 0));

        Im.Line.Same();

        ImEx.TextRightAligned($"{_selectedFiles.Count} / {_editor.Files.Available.Count} Files Selected");
    }

    private void DrawFileManagementOverview()
    {
        using var style = ImStyleSingle.FrameRounding.Push(0)
            .Push(ImStyleDouble.ItemSpacing,          Vector2.Zero)
            .Push(ImStyleSingle.FrameBorderThickness, Im.Style.ChildBorderThickness);

        var width = Im.ContentRegion.Available.X / 8;

        Im.Item.SetNextWidth(width * 3);
        Im.Input.Text("##fileFilter"u8, ref _fileOverviewFilter1, "Filter file..."u8);
        Im.Line.Same();
        Im.Item.SetNextWidth(width * 3);
        Im.Input.Text("##pathFilter"u8, ref _fileOverviewFilter2, "Filter path..."u8);
        Im.Line.Same();
        Im.Item.SetNextWidth(width * 2);
        Im.Input.Text("##optionFilter"u8, ref _fileOverviewFilter3, "Filter option..."u8);
    }
}
