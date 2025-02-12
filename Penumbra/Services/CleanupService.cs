using OtterGui.Services;
using Penumbra.Collections.Manager;
using Penumbra.Mods.Manager;

namespace Penumbra.Services;

public class CleanupService(SaveService saveService, ModManager mods, CollectionManager collections) : IService
{
    private CancellationTokenSource _cancel = new();
    private Task?                   _task;

    public double Progress { get; private set; }

    public bool IsRunning
        => _task is { IsCompleted: false };

    public void Cancel()
        => _cancel.Cancel();

    public void CleanUnusedLocalData()
    {
        if (IsRunning)
            return;

        var usedFiles = mods.Select(saveService.FileNames.LocalDataFile).ToHashSet();
        Progress = 0;
        var deleted = 0;
        _cancel = new CancellationTokenSource();
        _task = Task.Run(() =>
        {
            var localFiles = saveService.FileNames.LocalDataFiles.ToList();
            var step       = 0.9 / localFiles.Count;
            Progress = 0.1;
            foreach (var file in localFiles)
            {
                if (_cancel.IsCancellationRequested)
                    break;

                try
                {
                    if (!file.Exists || usedFiles.Contains(file.FullName))
                        continue;

                    file.Delete();
                    Penumbra.Log.Debug($"[CleanupService] Deleted unused local data file {file.Name}.");
                    ++deleted;
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[CleanupService] Failed to delete unused local data file {file.Name}:\n{ex}");
                }

                Progress += step;
            }

            Penumbra.Log.Information($"[CleanupService] Deleted {deleted} unused local data files.");
            Progress = 1;
        });
    }

    public void CleanBackupFiles()
    {
        if (IsRunning)
            return;

        Progress = 0;
        var deleted = 0;
        _cancel = new CancellationTokenSource();
        _task = Task.Run(() =>
        {
            var configFiles = Directory.EnumerateFiles(saveService.FileNames.ConfigDirectory, "*.json.bak", SearchOption.AllDirectories)
                .ToList();
            Progress = 0.1;
            if (_cancel.IsCancellationRequested)
                return;

            var groupFiles = mods.BasePath.EnumerateFiles("group_*.json.bak", SearchOption.AllDirectories).ToList();
            Progress = 0.5;
            var step = 0.4 / (groupFiles.Count + configFiles.Count);
            foreach (var file in groupFiles)
            {
                if (_cancel.IsCancellationRequested)
                    break;

                try
                {
                    if (!file.Exists)
                        continue;

                    file.Delete();
                    ++deleted;
                    Penumbra.Log.Debug($"[CleanupService] Deleted group backup file {file.FullName}.");
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[CleanupService] Failed to delete group backup file {file.FullName}:\n{ex}");
                }

                Progress += step;
            }

            Penumbra.Log.Information($"[CleanupService] Deleted {deleted} group backup files.");

            deleted = 0;
            foreach (var file in configFiles)
            {
                if (_cancel.IsCancellationRequested)
                    break;

                try
                {
                    if (!File.Exists(file))
                        continue;

                    File.Delete(file);
                    ++deleted;
                    Penumbra.Log.Debug($"[CleanupService] Deleted config backup file {file}.");
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[CleanupService] Failed to delete config backup file {file}:\n{ex}");
                }

                Progress += step;
            }

            Penumbra.Log.Information($"[CleanupService] Deleted {deleted} config backup files.");
            Progress = 1;
        });
    }

    public void CleanupAllUnusedSettings()
    {
        if (IsRunning)
            return;

        Progress = 0;
        var totalRemoved    = 0;
        var diffCollections = 0;
        _cancel = new CancellationTokenSource();
        _task = Task.Run(() =>
        {
            var step = 1.0 / collections.Storage.Count;
            foreach (var collection in collections.Storage)
            {
                if (_cancel.IsCancellationRequested)
                    break;

                var count = collections.Storage.CleanUnavailableSettings(collection);
                if (count > 0)
                {
                    Penumbra.Log.Debug(
                        $"[CleanupService] Removed {count} unused settings from collection {collection.Identity.AnonymizedName}.");
                    totalRemoved += count;
                    ++diffCollections;
                }

                Progress += step;
            }

            Penumbra.Log.Information($"[CleanupService] Removed {totalRemoved} unused settings from {diffCollections} separate collections.");
            Progress = 1;
        });
    }
}
