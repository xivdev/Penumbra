using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Penumbra.Mods;

public sealed partial class ModManager
{
    public  DirectoryInfo  BasePath { get; private set; } = null!;
    public bool Valid { get; private set; }

    public event Action?              ModDiscoveryStarted;
    public event Action?              ModDiscoveryFinished;
    public event Action<string, bool> ModDirectoryChanged;

    // Change the mod base directory and discover available mods.
    public void DiscoverMods(string newDir)
    {
        SetBaseDirectory(newDir, false);
        DiscoverMods();
    }

    // Set the mod base directory.
    // If its not the first time, check if it is the same directory as before.
    // Also checks if the directory is available and tries to create it if it is not.
    private void SetBaseDirectory(string newPath, bool firstTime)
    {
        if (!firstTime && string.Equals(newPath, Penumbra.Config.ModDirectory, StringComparison.OrdinalIgnoreCase))
            return;

        if (newPath.Length == 0)
        {
            Valid    = false;
            BasePath = new DirectoryInfo(".");
            if (Penumbra.Config.ModDirectory != BasePath.FullName)
                ModDirectoryChanged.Invoke(string.Empty, false);
        }
        else
        {
            var newDir = new DirectoryInfo(newPath);
            if (!newDir.Exists)
                try
                {
                    Directory.CreateDirectory(newDir.FullName);
                    newDir.Refresh();
                }
                catch (Exception e)
                {
                    Penumbra.Log.Error($"Could not create specified mod directory {newDir.FullName}:\n{e}");
                }

            BasePath = newDir;
            Valid    = Directory.Exists(newDir.FullName);
            if (Penumbra.Config.ModDirectory != BasePath.FullName)
                ModDirectoryChanged.Invoke(BasePath.FullName, Valid);
        }
    }

    private static void OnModDirectoryChange(string newPath, bool _)
    {
        Penumbra.Log.Information($"Set new mod base directory from {Penumbra.Config.ModDirectory} to {newPath}.");
        Penumbra.Config.ModDirectory = newPath;
        Penumbra.Config.Save();
    }

    // Discover new mods.
    public void DiscoverMods()
    {
        NewMods.Clear();
        ModDiscoveryStarted?.Invoke();
        _mods.Clear();
        BasePath.Refresh();

        if (Valid && BasePath.Exists)
        {
            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount / 2,
            };
            var queue = new ConcurrentQueue<Mod>();
            Parallel.ForEach(BasePath.EnumerateDirectories(), options, dir =>
            {
                var mod = Mod.LoadMod(this, dir, false);
                if (mod != null)
                    queue.Enqueue(mod);
            });

            foreach (var mod in queue)
            {
                mod.Index = _mods.Count;
                _mods.Add(mod);
            }
        }

        ModDiscoveryFinished?.Invoke();
        Penumbra.Log.Information("Rediscovered mods.");

        if (MigrateModBackups)
            ModBackup.MigrateZipToPmp(this);
    }
}
