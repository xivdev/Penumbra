using System.Threading.Channels;
using OtterGui.Services;
using Penumbra.Mods.Manager;

namespace Penumbra.Services;

public class FileWatcher : IDisposable, IService
{
    private readonly FileSystemWatcher                  _fsw;
    private readonly Channel<string>                    _queue;
    private readonly CancellationTokenSource            _cts = new();
    private readonly Task                               _consumer;
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ModImportManager                   _modImportManager;
    private readonly MessageService                     _messageService;
    private readonly Configuration                      _config;

    public FileWatcher(ModImportManager modImportManager, MessageService messageService, Configuration config)
    {
        _modImportManager = modImportManager;
        _messageService   = messageService;
        _config           = config;

        if (!_config.EnableDirectoryWatch)
            return;

        _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode     = BoundedChannelFullMode.DropOldest,
        });

        _fsw = new FileSystemWatcher(_config.WatchDirectory)
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

        _consumer = Task.Factory.StartNew(
            () => ConsumerLoopAsync(_cts.Token),
            _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

        _fsw.EnableRaisingEvents = true;
    }

    private void OnPath(object? sender, FileSystemEventArgs e)
    {
        // Cheap de-dupe: only queue once per filename until processed
        if (!_config.EnableDirectoryWatch || !_pending.TryAdd(e.FullPath, 0))
            return;

        _ = _queue.Writer.TryWrite(e.FullPath);
    }

    private async Task ConsumerLoopAsync(CancellationToken token)
    {
        if (!_config.EnableDirectoryWatch)
            return;

        var reader = _queue.Reader;
        while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
        {
            while (reader.TryRead(out var path))
            {
                try
                {
                    await ProcessOneAsync(path, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Penumbra.Log.Debug($"[FileWatcher] Canceled via Token.");
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Debug($"[FileWatcher] Error during Processing: {ex}");
                }
                finally
                {
                    _pending.TryRemove(path, out _);
                }
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
                    {
                        _modImportManager.AddUnpack(path);
                        return;
                    }
                    else
                    {
                        var invoked = false;
                        Action<bool> installRequest = args =>
                        {
                            if (invoked)
                                return;

                            invoked = true;
                            _modImportManager.AddUnpack(path);
                        };

                        _messageService.PrintModFoundInfo(
                            Path.GetFileNameWithoutExtension(path),
                            installRequest);

                        return;
                    }
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

    public void UpdateDirectory(string newPath)
    {
        if (!_config.EnableDirectoryWatch || _fsw is null || !Directory.Exists(newPath) || string.IsNullOrWhiteSpace(newPath))
            return;

        _fsw.EnableRaisingEvents = false;
        _fsw.Path                = newPath;
        _fsw.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        if (!_config.EnableDirectoryWatch)
            return;

        _fsw.EnableRaisingEvents = false;
        _cts.Cancel();
        _fsw.Dispose();
        _queue.Writer.TryComplete();
        try
        {
            _consumer.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            /* swallow */
        }

        _cts.Dispose();
    }
}
