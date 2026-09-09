using Penumbra.Communication;
using Penumbra.Mods.Editor;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public class ModExportManager : IDisposable, Luna.IService
{
    private readonly IoConfig            _config;
    private readonly CommunicatorService _communicator;
    private readonly ModManager          _modManager;
    private readonly FileWatcher         _fileWatcher;

    private DirectoryInfo? _exportDirectory;

    public DirectoryInfo ExportDirectory
        => _exportDirectory ?? _modManager.BasePath;

    public ModExportManager(IoConfig config, CommunicatorService communicator, ModManager modManager, FileWatcher fileWatcher)
    {
        _config                        =  config;
        _communicator                  =  communicator;
        _modManager                    =  modManager;
        _fileWatcher                   =  fileWatcher;
        _config.ExportDirectoryChanged += OnExportDirectoryChanged;
        OnExportDirectoryChanged(_config.ExportDirectory, _config.ExportDirectory);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModExportManager);
    }

    /// <summary>
    ///   Update the export directory to a new directory. Can also reset it to null with empty input.
    ///   If the directory is changed, all existing backups will be moved to the new one.
    /// </summary>
    /// <param name="newPath"> The new directory name. </param>
    /// <param name="oldPath"> The old directory name. </param>
    private void OnExportDirectoryChanged(string newPath, string oldPath)
    {
        if (string.IsNullOrEmpty(newPath))
        {
            _exportDirectory = null;
            return;
        }

        var dir = new DirectoryInfo(newPath);
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

        _exportDirectory = dir;
        if (newPath != oldPath)
            foreach (var mod in _modManager)
                new ModBackup(this, mod).Move(dir.FullName);
    }

    public Task CreateAsync(Mod mod)
    {
        var backup = new ModBackup(this, mod);
        return backup.CreateAsync();
    }

    public void IgnoreExportedFile(string fullPath)
    {
        if (_config.PreventExportLoopback)
            _fileWatcher.IgnoreFile(fullPath);
    }

    public void Dispose()
    {
        _config.ExportDirectoryChanged -= OnExportDirectoryChanged;
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
    }

    /// <summary> Automatically migrate the backup file to the new name if any exists. </summary>
    private void OnModPathChange(in ModPathChanged.Arguments arguments)
    {
        if (arguments.Type is not ModPathChangeType.Moved || arguments.OldDirectory is null || arguments.NewDirectory is null)
            return;

        arguments.Mod.ModPath = arguments.OldDirectory;
        new ModBackup(this, arguments.Mod).Move(null, arguments.NewDirectory.Name);
        arguments.Mod.ModPath = arguments.NewDirectory;
    }
}
