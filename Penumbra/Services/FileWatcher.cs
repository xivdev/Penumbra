using ImSharp;
using Luna;
using Penumbra.Mods.Manager;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace Penumbra.Services;

public sealed class FileWatcher : IDisposable, IService
{
    private readonly ConcurrentSet<string>              _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _ignored = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _extractedArchives = new(StringComparer.OrdinalIgnoreCase);
    private readonly ModImportManager                   _modImportManager;
    private readonly MessageService                     _messageService;
    private readonly Configuration                      _config;

    private bool                     _pausedConsumer;
    private FileSystemWatcher?       _fsw;
    private CancellationTokenSource? _cts = new();
    private Task?                    _consumer;

    /// <summary> The time-to-live of ignore entries, in the same unit as <see cref="Environment.TickCount64"/>, namely milliseconds. </summary>
    private const long IgnoreTimeToLive = 60000L;

    private static readonly HashSet<string> ModExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pmp", ".pcp", ".ttmp", ".ttmp2" };

    private static readonly HashSet<string> ContainerExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".zip", ".rar", ".7z" };

    /// <summary>
    /// Subdirectory under the system temp directory used for extracted archive entries.
    /// </summary>
    private static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "Penumbra-FileWatcher");

    public FileWatcher(ModImportManager modImportManager, MessageService messageService, Configuration config)
    {
        _modImportManager = modImportManager;
        _messageService   = messageService;
        _config           = config;

        WipeTempRoot();

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

    public void ToggleContainerPeeking(bool value)
    {
        if (_config.EnableContainerPeeking == value)
            return;

        _config.EnableContainerPeeking = value;
        _config.Save();

        // Re-create the FSW so its filter list reflects the new state.
        if (_config.EnableDirectoryWatch && _fsw is not null)
            SetupFileWatcher(_config.WatchDirectory);
    }

    public void IgnoreFile(string fullPath)
    {
        if (_config.EnableDirectoryWatch)
            _ignored[fullPath] = Environment.TickCount64 + IgnoreTimeToLive;
    }

    /// <summary>
    /// Deletes every extracted archive directory tracked by this instance. Called from the debug drawer.
    /// </summary>
    public void CleanExtracted()
    {
        foreach (var dir in _extractedArchives.Keys.ToList())
        {
            if (TryDeleteDirectory(dir))
                _extractedArchives.TryRemove(dir, out _);
        }
        Penumbra.Log.Verbose("[FileWatcher] Manual cleanup of extracted archives requested.");
    }

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

        // Only wake us for the exact patterns we care about.
        _fsw.Filters.Add("*.pmp");
        _fsw.Filters.Add("*.pcp");
        _fsw.Filters.Add("*.ttmp");
        _fsw.Filters.Add("*.ttmp2");

        if (_config.EnableContainerPeeking)
        {
            _fsw.Filters.Add("*.zip");
            _fsw.Filters.Add("*.rar");
            _fsw.Filters.Add("*.7z");
        }

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
    {
        if (_ignored.TryRemove(e.FullPath, out var expiresAtTickCount) && expiresAtTickCount > Environment.TickCount64)
        {
            Penumbra.Log.Verbose($"[FileWatcher] FSW event for '{e.FullPath}' suppressed by ignore list.");
            return;
        }

        if (_pending.TryAdd(e.FullPath))
            Penumbra.Log.Verbose($"[FileWatcher] FSW event for '{e.FullPath}' enqueued.");
    }

    private async Task ConsumerLoopAsync(CancellationToken token)
    {
        while (true)
        {
            GarbageCollectIgnored();

            var path = _pending.FirstOrDefault<string>();
            if (path is null || _pausedConsumer)
            {
                await Task.Delay(500, token).ConfigureAwait(false);
                continue;
            }

            var totalSw = Stopwatch.StartNew();
            Penumbra.Log.Verbose($"[FileWatcher] Picked up '{path}' from queue.");

            try
            {
                var ext = Path.GetExtension(path);
                if (ContainerExtensions.Contains(ext))
                {
                    if (_config.EnableContainerPeeking)
                        await ProcessContainerAsync(path, token).ConfigureAwait(false);
                    // else: peeking was toggled off after the event was queued; drop silently.
                }
                else if (ModExtensions.Contains(ext))
                {
                    await ProcessOneAsync(path, token).ConfigureAwait(false);
                }
                // else: extension we don't recognise (shouldn't happen given filters); drop silently.
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
                Penumbra.Log.Verbose($"[FileWatcher] Finished '{path}' in {totalSw.ElapsedMilliseconds}ms.");
            }
        }
    }

    private void GarbageCollectIgnored()
    {
        foreach (var entry in _ignored)
        {
            if (Environment.TickCount64 >= entry.Value)
                _ignored.TryRemove(entry);
        }
    }

    private async Task ProcessOneAsync(string path, CancellationToken token)
    {
        if (!await WaitForStableAsync(path, token).ConfigureAwait(false))
            return;

        Penumbra.Log.Verbose($"[FileWatcher] Triggering import for '{path}'.");
        TriggerImport(path);
    }

    /// <summary>
    /// Polls the file until two consecutive size readings match, indicating the writer is done.
    /// Returns false if the file never settled within the retry budget or the token was canceled.
    /// </summary>
    private static async Task<bool> WaitForStableAsync(string path, CancellationToken token)
    {
        const int maxTries = 40;
        long lastLen       = -1;
        var sw             = Stopwatch.StartNew();

        for (var i = 0; i < maxTries && !token.IsCancellationRequested; i++)
        {
            if (!File.Exists(path))
            {
                await Task.Delay(100, token).ConfigureAwait(false);
                continue;
            }

            try
            {
                var len = new FileInfo(path).Length;
                if (len > 0 && len == lastLen)
                {
                    Penumbra.Log.Verbose(
                        $"[FileWatcher] '{path}' stable at {len} bytes after {sw.ElapsedMilliseconds}ms ({i + 1} polls).");
                    return true;
                }

                lastLen = len;
            }
            catch (IOException)
            {
                Penumbra.Log.Debug("[FileWatcher] File is still being written to.");
            }
            catch (UnauthorizedAccessException)
            {
                Penumbra.Log.Debug("[FileWatcher] File is locked.");
            }

            await Task.Delay(150, token).ConfigureAwait(false);
        }

        Penumbra.Log.Verbose($"[FileWatcher] '{path}' did not stabilize within {sw.ElapsedMilliseconds}ms.");
        return false;
    }

    private Task<ModImportResult[]> TriggerImport(string path)
    {
        if (_config.EnableAutomaticModImport)
            return _modImportManager.AddUnpack(path);
        else
        {
            var tcs = new TaskCompletionSource<ModImportResult[]>();
            _messageService.AddMessage(new InstallNotification(_modImportManager, path, tcs), false);
            return tcs.Task;
        }
    }

    /// <summary>
    /// Opens an archive, scans entries for mod files (by entry-name extension only), extracts matches
    /// into a per-archive subdirectory of <see cref="TempRoot"/>, then queues each extracted file via
    /// <see cref="TriggerImport"/>. Per-archive subdirectory keeps the original filename intact for the UI.
    /// </summary>
    private async Task ProcessContainerAsync(string path, CancellationToken token)
    {
        if (!await WaitForStableAsync(path, token).ConfigureAwait(false))
            return;

        var ext            = Path.GetExtension(path);
        string? archiveDir = null;
        var extractedNow   = new List<string>();

        try
        {
            var openSw = Stopwatch.StartNew();
            Penumbra.Log.Verbose($"[FileWatcher] Opening container '{path}'.");
            using var archive = OpenArchive(path, ext);
            if (archive is null)
                return;
            Penumbra.Log.Verbose(
                $"[FileWatcher] Opened container '{path}' in {openSw.ElapsedMilliseconds}ms.");

            var enumSw = Stopwatch.StartNew();
            var candidates = archive.Entries
                .Where(e => !e.IsDirectory
                         && e.Key is { Length: > 0 } key
                         && ModExtensions.Contains(Path.GetExtension(key)))
                .ToList();
            Penumbra.Log.Verbose(
                $"[FileWatcher] Enumerated entries of '{path}' in {enumSw.ElapsedMilliseconds}ms; {candidates.Count} mod entries found.");

            // Silent ignore for archives that contain nothing relevant
            if (candidates.Count == 0)
                return;

            Directory.CreateDirectory(TempRoot);
            archiveDir = Path.Combine(TempRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(archiveDir);

            foreach (var entry in candidates)
            {
                token.ThrowIfCancellationRequested();

                if (entry.IsEncrypted)
                {
                    Penumbra.Log.Warning(
                        $"[FileWatcher] Skipping encrypted entry '{entry.Key}' in container '{path}'.");
                    continue;
                }

                var safeName = Path.GetFileName(entry.Key!);
                var tempPath = Path.Combine(archiveDir, safeName);

                if (File.Exists(tempPath))
                {
                    Penumbra.Log.Warning(
                        $"[FileWatcher] Duplicate entry name '{safeName}' in container '{path}'; skipping later occurrence.");
                    continue;
                }

                try
                {
                    var extractSw = Stopwatch.StartNew();
                    long bytesWritten;
                    await using (var input = entry.OpenEntryStream())
                    await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                                     FileShare.None, 81920, useAsync: true))
                    {
                        await input.CopyToAsync(output, 81920, token).ConfigureAwait(false);
                        bytesWritten = output.Position;
                    }

                    Penumbra.Log.Verbose(
                        $"[FileWatcher] Extracted '{safeName}' ({bytesWritten} bytes) in {extractSw.ElapsedMilliseconds}ms.");
                    extractedNow.Add(tempPath);
                }
                catch (OperationCanceledException)
                {
                    TryDelete(tempPath);
                    throw;
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Warning(
                        $"[FileWatcher] Failed to extract '{entry.Key}' from '{path}': {ex.Message}");
                    TryDelete(tempPath);
                }

                // Yield between entries so a large archive doesn't monopolise the worker thread
                // while the game is rendering.
                await Task.Yield();
            }

            if (extractedNow.Count > 0)
                _extractedArchives[archiveDir] = Environment.TickCount64;
            else
                TryDeleteDirectory(archiveDir);
        }
        catch (OperationCanceledException)
        {
            if (archiveDir is not null)
                TryDeleteDirectory(archiveDir);
            throw;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"[FileWatcher] Failed to read container '{path}': {ex.Message}");
            if (archiveDir is not null)
                TryDeleteDirectory(archiveDir);
            return;
        }

        // Hand each extracted file off as if it were a fresh drop. The freshly-closed stream means
        // we can skip WaitForStableAsync here and call TriggerImport directly.
        foreach (var tempPath in extractedNow)
        {
            try
            {
                Penumbra.Log.Verbose($"[FileWatcher] Triggering import for extracted '{tempPath}'.");
                TriggerImport(tempPath);
            }
            catch (Exception ex)
            {
                Penumbra.Log.Warning(
                    $"[FileWatcher] Failed to trigger import for extracted file '{tempPath}': {ex.Message}");
            }
        }
    }

    private static IArchive? OpenArchive(string path, string extension)
        => extension.ToLowerInvariant() switch
        {
            ".zip" => ZipArchive.Open(path),
            ".rar" => RarArchive.Open(path),
            ".7z"  => SevenZipArchive.Open(path),
            _ => null,
        };

    private static void WipeTempRoot()
    {
        try
        {
            if (Directory.Exists(TempRoot))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(TempRoot))
                {
                    if (Directory.Exists(entry))
                        TryDeleteDirectory(entry);
                    else
                        TryDelete(entry);
                }
            }
            else
            {
                Directory.CreateDirectory(TempRoot);
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"[FileWatcher] Could not prepare temp root '{TempRoot}': {ex.Message}");
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            return true;
        }
        catch
        {
            return false;
        }
    }


    public void Dispose()
    {
        EndConsumerTask();
        EndFileWatcher();
        // Cleanup of extracted files is intentionally skipped here. WipeTempRoot() on the next
        // construction handles leftovers without blocking shutdown.
    }

    public sealed class FileWatcherDrawer(Configuration config, FileWatcher fileWatcher) : IUiService
    {
        public void Draw()
        {
            using var tree = Im.Tree.Node("File Watcher"u8);
            if (!tree)
                return;

            using var table = Im.Table.Begin("table"u8, 2);
            if (!table)
                return;

            table.DrawColumn("Enabled"u8);
            table.DrawColumn($"{config.EnableDirectoryWatch}");

            table.DrawColumn("Automatic Import"u8);
            table.DrawColumn($"{config.EnableAutomaticModImport}");

            table.DrawColumn("Container Peeking"u8);
            table.DrawColumn($"{config.EnableContainerPeeking}");

            table.DrawColumn("Watched Directory"u8);
            table.DrawColumn(config.WatchDirectory);

            table.DrawColumn("Temp Root"u8);
            table.DrawColumn(TempRoot);

            table.DrawColumn("File Watcher Path"u8);
            table.DrawColumn(fileWatcher._fsw?.Path ?? "<NULL>");

            table.DrawColumn("Raising Events"u8);
            table.DrawColumn($"{fileWatcher._fsw?.EnableRaisingEvents ?? false}");

            table.DrawColumn("File Filters"u8);
            table.DrawColumn(StringU8.Join(", ", fileWatcher._fsw?.Filters ?? []));

            table.DrawColumn("Consumer Task State"u8);
            table.DrawColumn($"{fileWatcher._consumer?.Status.ToString() ?? "<NULL>"}");

            table.DrawColumn("Debug Pause Consumer"u8);
            table.NextColumn();
            if (Im.SmallButton(fileWatcher._pausedConsumer ? "Unpause"u8 : "Pause"u8))
                fileWatcher._pausedConsumer = !fileWatcher._pausedConsumer;

            table.DrawColumn("Pending Files"u8);
            table.DrawColumn(StringU8.Join('\n', fileWatcher._pending));

            table.DrawColumn("Ignored Files"u8);
            // FIXME .ToList() forces the use of an IReadOnlyCollection overload because, at the time of writing, IEnumerable ones don't handle empty enumerables correctly.
            table.DrawColumn(StringU8.Join((byte)'\n', fileWatcher._ignored.Select(entry =>
                (entry.Value - Environment.TickCount64) switch
                {
                    <= 0    => $"<EXPIRED> {entry.Key}",
                    var ttl => $"<{ttl}ms> {entry.Key}",
                }).ToList()));

            table.DrawColumn("Extracted Archives"u8);
            table.DrawColumn(StringU8.Join((byte)'\n', fileWatcher._extractedArchives.Select(entry =>
            {
                var ageSec    = (Environment.TickCount64 - entry.Value) / 1000;
                var fileCount = TryCountFiles(entry.Key);
                return $"<{ageSec}s, {fileCount} files> {entry.Key}";
            }).ToList()));

            table.DrawColumn("Clean Extracted"u8);
            table.NextColumn();
            if (Im.SmallButton("Delete All Extracted"u8))
                fileWatcher.CleanExtracted();
        }

        private static int TryCountFiles(string dir)
        {
            try { return Directory.Exists(dir) ? Directory.EnumerateFiles(dir).Count() : 0; }
            catch { return 0; }
        }
    }
}
