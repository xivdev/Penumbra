using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.UI.Tabs.Debug;

public class ModMigratorDebug(ModManager modManager, ModMigrator migrator) : IUiService
{
    private string _inputPath  = string.Empty;
    private string _outputPath = string.Empty;
    private Task?  _indexTask;
    private Task?  _mdlTask;

    private string _selectedBackup = string.Empty;

    private void DrawJsonBackup()
    {
        if (Im.Button("Backup JSON Files"u8))
            DebugUtilities.BackupJsonFiles(modManager.BasePath.FullName);

        var enabled = _selectedBackup.Length > 0 && File.Exists(_selectedBackup);
        if (ImEx.Button("Restore JSON Files from selected Backup"u8, default, !enabled))
            DebugUtilities.RestoreBackupJsonFiles(modManager.BasePath.FullName, _selectedBackup);

        var backupFiles = Directory.EnumerateFiles(modManager.BasePath.FullName, "json_backup_*.zip", SearchOption.TopDirectoryOnly).Reverse().ToList();
        if (backupFiles.Count <= 0)
            return;

        using var list = Im.ListBox.Begin("Backups"u8,
            Im.ContentRegion.Available with { Y = 6 * Im.Style.TextHeightWithSpacing - Im.Style.ItemSpacing.Y + 2 * Im.Style.FramePadding.Y });
        if (!list)
            return;

        using var clip = new Im.ListClipper(backupFiles.Count, Im.Style.TextHeightWithSpacing);
        foreach (var backup in clip.Iterate(backupFiles))
        {
            var dateString = Path.GetFileNameWithoutExtension(backup).AsSpan("json_backup_".Length);
            if (DateTime.TryParseExact(dateString, "yyyyMMddHHmmss", null, DateTimeStyles.AssumeUniversal, out var time))
            {
                if (Im.Selectable($"{time:u}", backup == _selectedBackup))
                    _selectedBackup = backup;
            }
            else
            {
                if (Im.Selectable(dateString, backup == _selectedBackup))
                    _selectedBackup = backup;
            }

            using var context = Im.Popup.BeginContextItem();
            if (!context)
                continue;

            if (!Im.Menu.Item("Delete"u8))
                continue;

            try
            {
                File.Delete(backup);
            }
            catch
            {
                // Nothing
            }
        }
    }

    public void Draw()
    {
        using var header = Im.Tree.HeaderId("Mod Migrator"u8);
        if (!header)
            return;

        DrawJsonBackup();
        LunaStyle.DrawSeparator();

        if (Im.Button("Test Mod Serializing/Deserializing"u8))
            DebugUtilities.CompareModSerDeser(modManager);

        Im.Input.Text("##input"u8,  ref _inputPath,  "Input Path..."u8);
        Im.Input.Text("##output"u8, ref _outputPath, "Output Path..."u8);

        if (ImEx.Button("Create Index Texture"u8, default, "Requires input to be a path to a normal texture."u8, _inputPath.Length is 0
             || _outputPath.Length is 0
             || _indexTask is
                {
                    IsCompleted: false,
                }))
            _indexTask = migrator.CreateIndexFile(_inputPath, _outputPath);

        if (_indexTask is not null)
        {
            Im.Line.Same();
            ImEx.TextFrameAligned($"{_indexTask.Status}");
        }

        if (ImEx.Button("Update Model File"u8, default, "Requires input to be a path to a mdl."u8, _inputPath.Length is 0
             || _outputPath.Length is 0
             || _mdlTask is
                {
                    IsCompleted: false,
                }))
            _mdlTask = Task.Run(() =>
            {
                File.Copy(_inputPath, _outputPath, true);
                MigrationManager.TryMigrateSingleModel(_outputPath, false);
            });

        if (_mdlTask is not null)
        {
            Im.Line.Same();
            ImEx.TextFrameAligned($"{_mdlTask.Status}");
        }

        DrawMods();
    }

    private void DrawMods()
    {
        using var tree = Im.Tree.Node("Mods"u8);
        if (!tree)
            return;

        foreach (var mod in modManager)
        {
            using var modTree = Im.Tree.Node($"{mod.Name} ({mod.Index})");
            if (!modTree)
                continue;

            Im.Tree.Leaf($"Directory: {mod.ModPath}");
            Im.Tree.Leaf($"GUID: {mod.StableIdentifier}");
            Im.Tree.Leaf($"Version: {mod.LoadedVersion}");
            Im.Tree.Leaf($"Path: {mod.Node?.FullPath ?? "<NULL>"}");
            Im.Tree.Leaf($"Redirections: {mod.TotalFileCount}");
            Im.Tree.Leaf($"Swaps: {mod.TotalSwapCount}");
            Im.Tree.Leaf($"Manipulations: {mod.TotalManipulations}");
            Im.Tree.Leaf($"Default:  {mod.Default.Files.Count} | {mod.Default.FileSwaps.Count} | {mod.Default.Manipulations.Count}");
            using var groups = Im.Tree.Node($"Option Groups ({mod.Groups.Count})");
            if (!groups)
                continue;

            foreach (var group in mod.Groups)
            {
                using var groupNode = Im.Tree.Node($"{group.Name} ({group.Index})");
                if (!groupNode)
                    continue;

                Im.Tree.Leaf($"Type: {group.Type}");
                Im.Tree.Leaf($"ID: {group.Id}");

                using (var options = Im.Tree.Node($"Options ({group.Options.Count})"))
                {
                    if (options)
                        foreach (var option in group.Options)
                            Im.Tree.Leaf($"{option.Name} ({option.Index}): {option.Id}");
                }

                using (var containers = Im.Tree.Node($"Containers ({group.DataContainers.Count})"))
                {
                    if (containers)
                        foreach (var container in group.DataContainers)
                        {
                            Im.Tree.Leaf(
                                $"{container.GetName()} ({container.Index}): {container.Files.Count} | {container.FileSwaps.Count} | {container.Manipulations.Count}");
                        }
                }
            }
        }
    }
}
