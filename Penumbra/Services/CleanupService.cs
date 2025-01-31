using OtterGui.Services;
using Penumbra.Collections.Manager;
using Penumbra.Mods.Manager;

namespace Penumbra.Services;

public class CleanupService(SaveService saveService, ModManager mods, CollectionManager collections) : IService
{
    public void CleanUnusedLocalData()
    {
        var usedFiles = mods.Select(saveService.FileNames.LocalDataFile).ToHashSet();
        foreach (var file in saveService.FileNames.LocalDataFiles.ToList())
        {
            try
            {
                if (!file.Exists || usedFiles.Contains(file.FullName))
                    continue;

                file.Delete();
                Penumbra.Log.Information($"[CleanupService] Deleted unused local data file {file.Name}.");
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error($"[CleanupService] Failed to delete unused local data file {file.Name}:\n{ex}");
            }
        }
    }

    public void CleanBackupFiles()
    {
        foreach (var file in mods.BasePath.EnumerateFiles("group_*.json.bak", SearchOption.AllDirectories))
        {
            try
            {
                if (!file.Exists)
                    continue;

                file.Delete();
                Penumbra.Log.Information($"[CleanupService] Deleted group backup file {file.FullName}.");
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error($"[CleanupService] Failed to delete group backup file {file.FullName}:\n{ex}");
            }
        }

        foreach (var file in Directory.EnumerateFiles(saveService.FileNames.ConfigDirectory, "*.json.bak", SearchOption.AllDirectories))
        {
            try
            {
                if (!File.Exists(file))
                    continue;

                File.Delete(file);
                Penumbra.Log.Information($"[CleanupService] Deleted config backup file {file}.");
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error($"[CleanupService] Failed to delete config backup file {file}:\n{ex}");
            }
        }
    }

    public void CleanupAllUnusedSettings()
    {
        foreach (var collection in collections.Storage)
        {
            var count = collections.Storage.CleanUnavailableSettings(collection);
            if (count > 0)
                Penumbra.Log.Information(
                    $"[CleanupService] Removed {count} unused settings from collection {collection.Identity.AnonymizedName}.");
        }
    }
}
