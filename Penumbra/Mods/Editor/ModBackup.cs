using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Penumbra.Mods;

/// <summary> Utility to create and apply a zipped backup of a mod. </summary>
public class ModBackup
{
    public static bool CreatingBackup { get; private set; }

    private readonly Mod           _mod;
    public readonly  string        Name;
    public readonly  bool          Exists;

    public ModBackup(ExportManager exportManager, Mod mod)
    {
        _mod           = mod;
        Name           = Path.Combine(exportManager.ExportDirectory.FullName, _mod.ModPath.Name) + ".pmp";
        Exists         = File.Exists(Name);
    }

    /// <summary> Migrate file extensions. </summary>
    public static void MigrateZipToPmp(ModManager modManager)
    {
        foreach (var mod in modManager)
        {
            var pmpName = mod.ModPath + ".pmp";
            var zipName = mod.ModPath + ".zip";
            if (!File.Exists(zipName))
                continue;

            try
            {
                if (!File.Exists(pmpName))
                    File.Move(zipName, pmpName);
                else
                    File.Delete(zipName);

                Penumbra.Log.Information($"Migrated mod export from {zipName} to {pmpName}.");
            }
            catch (Exception e)
            {
                Penumbra.Log.Warning($"Could not migrate mod export of {mod.ModPath} from .pmp to .zip:\n{e}");
            }
        }
    }

    /// <summary>
    /// Move and/or rename an exported mod.
    /// This object is unusable afterwards.
    /// </summary>
    public void Move(string? newBasePath = null, string? newName = null)
    {
        if (CreatingBackup || !Exists)
            return;

        try
        {
            newBasePath ??= Path.GetDirectoryName(Name) ?? string.Empty;
            newName     =   newName == null ? Path.GetFileName(Name) : newName + ".pmp";
            var newPath = Path.Combine(newBasePath, newName);
            File.Move(Name, newPath);
        }
        catch (Exception e)
        {
            Penumbra.Log.Warning($"Could not move mod export file {Name}:\n{e}");
        }
    }

    /// <summary> Create a backup zip without blocking the main thread. </summary>
    public async void CreateAsync()
    {
        if (CreatingBackup)
            return;

        CreatingBackup = true;
        await Task.Run(Create);
        CreatingBackup = false;
    }

    /// <summary> Create a backup. Overwrites pre-existing backups. </summary>
    private void Create()
    {
        try
        {
            Delete();
            ZipFile.CreateFromDirectory(_mod.ModPath.FullName, Name, CompressionLevel.Optimal, false);
            Penumbra.Log.Debug($"Created export file {Name} from {_mod.ModPath.FullName}.");
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not export mod {_mod.Name} to \"{Name}\":\n{e}");
        }
    }

    /// <summary> Delete a pre-existing backup. </summary>
    public void Delete()
    {
        if (!Exists)
            return;

        try
        {
            File.Delete(Name);
            Penumbra.Log.Debug($"Deleted export file {Name}.");
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not delete file \"{Name}\":\n{e}");
        }
    }

    /// <summary>
    /// Restore a mod from a pre-existing backup. Does not check if the mod contained in the backup is even similar.
    /// Does an automatic reload after extraction.
    /// </summary>
    public void Restore(ModManager modManager)
    {
        try
        {
            if (Directory.Exists(_mod.ModPath.FullName))
            {
                Directory.Delete(_mod.ModPath.FullName, true);
                Penumbra.Log.Debug($"Deleted mod folder {_mod.ModPath.FullName}.");
            }

            ZipFile.ExtractToDirectory(Name, _mod.ModPath.FullName);
            Penumbra.Log.Debug($"Extracted exported file {Name} to {_mod.ModPath.FullName}.");
            modManager.ReloadMod(_mod.Index);
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not restore {_mod.Name} from export \"{Name}\":\n{e}");
        }
    }
}
