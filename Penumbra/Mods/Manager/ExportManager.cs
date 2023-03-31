using System;
using System.IO;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public class ExportManager : IDisposable
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    private readonly ModManager          _modManager;

    private DirectoryInfo? _exportDirectory;

    public DirectoryInfo ExportDirectory
        => _exportDirectory ?? _modManager.BasePath;

    public ExportManager(Configuration config, CommunicatorService communicator, ModManager modManager)
    {
        _config       = config;
        _communicator = communicator;
        _modManager   = modManager;
        UpdateExportDirectory(_config.ExportDirectory, false);
        _communicator.ModPathChanged.Event += OnModPathChange;
    }

    /// <inheritdoc cref="UpdateExportDirectory(string, bool)"/>
    public void UpdateExportDirectory(string newDirectory)
        => UpdateExportDirectory(newDirectory, true);

    /// <summary>
    /// Update the export directory to a new directory. Can also reset it to null with empty input.
    /// If the directory is changed, all existing backups will be moved to the new one.
    /// </summary>
    /// <param name="newDirectory">The new directory name.</param>
    /// <param name="change">Can be used to stop saving for the initial setting</param>
    private void UpdateExportDirectory(string newDirectory, bool change)
    {
        if (newDirectory.Length == 0)
        {
            if (_exportDirectory == null)
                return;

            _exportDirectory        = null;
            _config.ExportDirectory = string.Empty;
            _config.Save();
            return;
        }

        var dir = new DirectoryInfo(newDirectory);
        if (dir.FullName.Equals(_exportDirectory?.FullName, StringComparison.OrdinalIgnoreCase))
            return;

        if (!dir.Exists)
            try
            {
                Directory.CreateDirectory(dir.FullName);
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not create Export Directory:\n{e}");
                return;
            }

        if (change)
            foreach (var mod in _modManager)
                new ModBackup(this, mod).Move(dir.FullName);

        _exportDirectory = dir;

        if (!change)
            return;

        _config.ExportDirectory = dir.FullName;
        _config.Save();
    }

    public void Dispose()
        => _communicator.ModPathChanged.Event -= OnModPathChange;

    /// <summary> Automatically migrate the backup file to the new name if any exists. </summary>
    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? oldDirectory,
        DirectoryInfo? newDirectory)
    {
        if (type is not ModPathChangeType.Moved || oldDirectory == null || newDirectory == null)
            return;

        mod.ModPath = oldDirectory;
        new ModBackup(this, mod).Move(null, newDirectory.Name);
        mod.ModPath = newDirectory;
    }
}
