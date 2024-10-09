using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
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
        if (_modPackCount == 0)
        {
            ImGuiUtil.Center("Nothing to extract.");
            return true;
        }

        if (_modPackCount == _currentModPackIdx)
        {
            DrawEndState();
            return true;
        }

        ImGui.NewLine();
        var percentage = (float)_currentModPackIdx / _modPackCount;
        ImGui.ProgressBar(percentage, size, $"Mod {_currentModPackIdx + 1} / {_modPackCount}");
        ImGui.NewLine();
        ImGui.TextUnformatted(State == ImporterState.DeduplicatingFiles
            ? $"Deduplicating {_currentModName}..."
            : $"Extracting {_currentModName}...");

        if (_currentNumOptions > 1)
        {
            ImGui.NewLine();
            ImGui.NewLine();
            if (_currentOptionIdx >= _currentNumOptions)
                ImGui.ProgressBar(1f, size, $"Extracted {_currentNumOptions} Options");
            else
                ImGui.ProgressBar(_currentOptionIdx / (float)_currentNumOptions, size,
                    $"Extracting Option {_currentOptionIdx + 1} / {_currentNumOptions}...");

            ImGui.NewLine();
            if (State != ImporterState.DeduplicatingFiles)
                ImGui.TextUnformatted(
                    $"Extracting Option {(_currentGroupName.Length == 0 ? string.Empty : $"{_currentGroupName} - ")}{_currentOptionName}...");
        }

        ImGui.NewLine();
        ImGui.NewLine();
        if (_currentFileIdx >= _currentNumFiles)
            ImGui.ProgressBar(1f, size, $"Extracted {_currentNumFiles} Files");
        else
            ImGui.ProgressBar(_currentFileIdx / (float)_currentNumFiles, size, $"Extracting File {_currentFileIdx + 1} / {_currentNumFiles}...");

        ImGui.NewLine();
        if (State != ImporterState.DeduplicatingFiles)
            ImGui.TextUnformatted($"Extracting File {_currentFileName}...");
        return false;
    }


    private void DrawEndState()
    {
        var success = ExtractedMods.Count(t => t.Error == null);

        ImGui.TextUnformatted($"Successfully extracted {success} / {ExtractedMods.Count} files.");
        ImGui.NewLine();
        using var table = ImRaii.Table("##files", 2);
        if (!table)
            return;

        foreach (var (file, dir, ex) in ExtractedMods)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(file.Name);
            ImGui.TableNextColumn();
            if (ex == null)
            {
                using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.FolderExpanded.Value());
                ImGui.TextUnformatted(dir?.FullName[(_baseDirectory.FullName.Length + 1)..] ?? "Unknown Directory");
            }
            else
            {
                using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.ConflictingMod.Value());
                ImGui.TextUnformatted(ex.Message);
                ImGuiUtil.HoverTooltip(ex.ToString());
            }
        }
    }

    public bool DrawCancelButton(Vector2 size)
        => ImGuiUtil.DrawDisabledButton("Cancel", size, string.Empty, _token.IsCancellationRequested);
}
