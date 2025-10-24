using ImSharp;
using Penumbra.Import.Structs;
using Penumbra.UI.Classes;

namespace Penumbra.Import;

public partial class TexToolsImporter
{
    // Progress Data
    private int _currentModPackIdx;
    private int _currentOptionIdx;
    private int _currentFileIdx;

    private int    _currentNumOptions;
    private int    _currentNumFiles;
    private string _currentModName    = string.Empty;
    private string _currentGroupName  = string.Empty;
    private string _currentOptionName = string.Empty;
    private string _currentFileName   = string.Empty;

    public bool DrawProgressInfo(Vector2 size)
    {
        if (_modPackCount is 0)
        {
            ImEx.TextCentered("Nothing to extract."u8);
            return true;
        }

        if (_modPackCount == _currentModPackIdx)
        {
            DrawEndState();
            return true;
        }

        Im.Line.New();
        var percentage = (float)_currentModPackIdx / _modPackCount;
        Im.ProgressBar(percentage, size, $"Mod {_currentModPackIdx + 1} / {_modPackCount}");
        Im.Line.New();
        Im.Text(State is ImporterState.DeduplicatingFiles
            ? $"Deduplicating {_currentModName}..."
            : $"Extracting {_currentModName}...");

        if (_currentNumOptions > 1)
        {
            Im.Line.New();
            Im.Line.New();
            if (_currentOptionIdx >= _currentNumOptions)
                Im.ProgressBar(1f, size, $"Extracted {_currentNumOptions} Options");
            else
                Im.ProgressBar(_currentOptionIdx / (float)_currentNumOptions, size,
                    $"Extracting Option {_currentOptionIdx + 1} / {_currentNumOptions}...");

            Im.Line.New();
            if (State is not ImporterState.DeduplicatingFiles)
                Im.Text(
                    $"Extracting Option {(_currentGroupName.Length == 0 ? string.Empty : $"{_currentGroupName} - ")}{_currentOptionName}...");
        }

        Im.Line.New();
        Im.Line.New();
        if (_currentFileIdx >= _currentNumFiles)
            Im.ProgressBar(1f, size, $"Extracted {_currentNumFiles} Files");
        else
            Im.ProgressBar(_currentFileIdx / (float)_currentNumFiles, size, $"Extracting File {_currentFileIdx + 1} / {_currentNumFiles}...");

        Im.Line.New();
        if (State is not ImporterState.DeduplicatingFiles)
            Im.Text($"Extracting File {_currentFileName}...");
        return false;
    }


    private void DrawEndState()
    {
        var success = ExtractedMods.Count(t => t.Error == null);

        Im.Text($"Successfully extracted {success} / {ExtractedMods.Count} files.");
        Im.Line.New();
        using var table = Im.Table.Begin("##files"u8, 2);
        if (!table)
            return;

        foreach (var (file, dir, ex) in ExtractedMods)
        {
            table.DrawColumn(file.Name);
            table.NextColumn();
            if (ex is null)
            {
                using var color = ImGuiColor.Text.Push(ColorId.FolderExpanded.Value());
                if (dir is null)
                    Im.Text("Unknown Directory"u8);
                else
                    Im.Text(dir.FullName.AsSpan(_baseDirectory.FullName.Length + 1));
            }
            else
            {
                using var color = ImGuiColor.Text.Push(ColorId.ConflictingMod.Value());
                Im.Text(ex.Message);
                Im.Tooltip.OnHover($"{ex}");
            }
        }
    }

    public bool DrawCancelButton(Vector2 size)
        => ImEx.Button("Cancel"u8, size, StringU8.Empty, _token.IsCancellationRequested);
}
