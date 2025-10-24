using Luna;
using Penumbra.Mods.Manager;

namespace Penumbra.Services;

public class FileWatcher : IDisposable, IService
{
    private readonly ConcurrentSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ModImportManager      _modImportManager;
    private readonly MessageService        _messageService;
    private readonly Configuration         _config;

    private bool                     _pausedConsumer;
    private FileSystemWatcher?       _fsw;
    private CancellationTokenSource? _cts = new();
    private Task?                    _consumer;

    public FileWatcher(ModImportManager modImportManager, MessageService messageService, Configuration config)
    {
        _modImportManager = modImportManager;
        _messageService   = messageService;
        _config           = config;

        if (_config.EnableDirectoryWatch)
        {
            SetupFileWatcher(_config.WatchDirectory);
            SetupConsumerTask();
        }
    }

    public void Toggle(bool value)
    {
        if (_config.EnableDirectoryWatch == value)
            return;

        _config.EnableDirectoryWatch = value;
        _config.Save();
        if (value)
        {
            SetupFileWatcher(_config.WatchDirectory);
            SetupConsumerTask();
        }
        else
        {
            EndFileWatcher();
            EndConsumerTask();
        }
    }

    internal void PauseConsumer(bool pause)
        => _pausedConsumer = pause;

    private void EndFileWatcher()
    {
        if (_fsw is null)
            return;

        _fsw.Dispose();
        _fsw = null;
    }

    private void SetupFileWatcher(string directory)
    {
        EndFileWatcher();
        _fsw = new FileSystemWatcher
        {
            IncludeSubdirectories = false,
            NotifyFilter          = NotifyFilters.FileName | NotifyFilters.CreationTime,
            InternalBufferSize    = 32 * 1024,
        };

        // Only wake us for the exact patterns we care about
        _fsw.Filters.Add("*.pmp");
        _fsw.Filters.Add("*.pcp");
        _fsw.Filters.Add("*.ttmp");
        _fsw.Filters.Add("*.ttmp2");

        _fsw.Created += OnPath;
        _fsw.Renamed += OnPath;
        UpdateDirectory(directory);
    }


    private void EndConsumerTask()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts = null;
        }

        _consumer = null;
    }

    private void SetupConsumerTask()
    {
        EndConsumerTask();
        _cts = new CancellationTokenSource();
        _consumer = Task.Factory.StartNew(
            () => ConsumerLoopAsync(_cts.Token),
            _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    public void UpdateDirectory(string newPath)
    {
        if (_config.WatchDirectory != newPath)
        {
            _config.WatchDirectory = newPath;
            _config.Save();
        }

        if (_fsw is null)
            return;

        _fsw.EnableRaisingEvents = false;
        if (!Directory.Exists(newPath) || newPath.Length is 0)
        {
            _fsw.Path = string.Empty;
        }
        else
        {
            _fsw.Path                = newPath;
            _fsw.EnableRaisingEvents = true;
        }
    }

    private void OnPath(object? sender, FileSystemEventArgs e)
        => _pending.TryAdd(e.FullPath);

    private async Task ConsumerLoopAsync(CancellationToken token)
    {
        while (true)
        {
            var path = _pending.FirstOrDefault<string>();
            if (path is null || _pausedConsumer)
            {
                await Task.Delay(500, token).ConfigureAwait(false);
                continue;
            }

            try
            {
                await ProcessOneAsync(path, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Penumbra.Log.Debug("[FileWatcher] Canceled via Token.");
            }
            catch (Exception ex)
            {
                Penumbra.Log.Warning($"[FileWatcher] Error during Processing: {ex}");
            }
            finally
            {
                _pending.TryRemove(path);
            }
        }
    }

    private async Task ProcessOneAsync(string path, CancellationToken token)
    {
        // Downloads often finish via rename; file may be locked briefly.
        // Wait until it exists and is readable; also require two stable size checks.
        const int maxTries = 40;
        long      lastLen  = -1;

        for (var i = 0; i < maxTries && !token.IsCancellationRequested; i++)
        {
            if (!File.Exists(path))
            {
                await Task.Delay(100, token);
                continue;
            }

            try
            {
                var fi  = new FileInfo(path);
                var len = fi.Length;
                if (len > 0 && len == lastLen)
                {
                    if (_config.EnableAutomaticModImport)
                        _modImportManager.AddUnpack(path);
                    else
                        _messageService.AddMessage(new InstallNotification(_modImportManager, path), false);
                    return;
                }

                lastLen = len;
            }
            catch (IOException)
            {
                Penumbra.Log.Debug($"[FileWatcher] File is still being written to.");
            }
            catch (UnauthorizedAccessException)
            {
                Penumbra.Log.Debug($"[FileWatcher] File is locked.");
            }

            await Task.Delay(150, token);
        }
    }


    public void Dispose()
    {
        EndConsumerTask();
        EndFileWatcher();
    }
}
