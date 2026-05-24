using Dalamud.Interface.ImGuiNotification;
using Luna;
using Penumbra.Import;
using Penumbra.Import.Structs;
using Penumbra.Mods.Editor;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public class ModImportManager(
    ModManager modManager,
    Configuration config,
    DuplicateManager duplicates,
    ModNormalizer modNormalizer,
    MigrationManager migrationManager,
    FileCompactor compactor) : IDisposable, IService
{
    private readonly  Dictionary<string, DateTime> _uniqueModsToUnpack = new(StringComparer.OrdinalIgnoreCase);
    internal readonly Queue<UnpackRequest>         ModsToUnpack        = new();

    /// <summary> Mods need to be added thread-safely outside of iteration. </summary>
    private readonly ConcurrentQueue<DirectoryInfo> _modsToAdd = new();

    private TexToolsImporter? _import;

    internal IEnumerable<DirectoryInfo> AddableMods
        => _modsToAdd;

    public void TryUnpacking()
    {
        if (Importing && _import!.State is not ImporterState.Done)
            return;

        UnpackRequest newMods;
        lock (ModsToUnpack)
        {
            if (!ModsToUnpack.TryDequeue(out newMods))
                return;
        }

        var files = newMods.Paths.Where(s =>
        {
            if (File.Exists(s))
                return true;

            Penumbra.Messager.NotificationMessage($"Failed to import queued mod at {s}, the file does not exist.", NotificationType.Warning,
                false);
            return false;
        }).Select(s => new FileInfo(s)).ToArray();

        Penumbra.Log.Debug($"Unpacking mods: {string.Join("\n\t", files.Select(f => f.FullName))}.");
        if (files.Length == 0)
        {
            newMods.TaskCompletionSource.SetResult([]);
            return;
        }

        _import = new TexToolsImporter(files.Length, files, AddNewMod, newMods.TaskCompletionSource, config, duplicates, modNormalizer,
            modManager, compactor, migrationManager, _import);
    }

    public bool Importing
        => _import != null;

    public bool IsImporting([NotNullWhen(true)] out TexToolsImporter? importer)
    {
        importer = _import;
        return _import != null;
    }

    public Task<ModImportResult[]> AddUnpack(params List<string> paths)
    {
        lock (ModsToUnpack)
        {
            var now       = DateTime.UtcNow;
            var nowOffset = now.AddSeconds(-2);
            for (var i = 0; i < paths.Count; ++i)
            {
                var path = paths[i];
                if (_uniqueModsToUnpack.TryGetValue(path, out var lastInstallTime))
                {
                    _uniqueModsToUnpack[path] = now;
                    if (lastInstallTime >= nowOffset)
                    {
                        paths.RemoveAt(i--);
                        Penumbra.Log.Debug($"Skipped installing mod {path} since it was last installed {(lastInstallTime - now).TotalSeconds} seconds ago.");
                    }
                }
                else
                {
                    _uniqueModsToUnpack.Add(path, now);
                }
            }

            if (paths.Count > 0)
            {
                Penumbra.Log.Debug($"Adding mods to install: {string.Join("\n\t", paths)}");
                var tcs = new TaskCompletionSource<ModImportResult[]>();
                ModsToUnpack.Enqueue(new UnpackRequest(paths, tcs));
                return tcs.Task;
            }
        }

        return Task.FromResult(Array.Empty<ModImportResult>());
    }

    public void ClearImport()
    {
        _import?.Dispose();
        _import = null;
    }

    public bool AddUnpackedMod([NotNullWhen(true)] out Mod? mod)
    {
        if (!_modsToAdd.TryDequeue(out var directory))
        {
            mod = null;
            return false;
        }

        mod = modManager.AddMod(directory, true);
        return mod is not null && mod.ModPath == directory;
    }

    public void Dispose()
    {
        ClearImport();
        _modsToAdd.Clear();
        lock (ModsToUnpack)
        {
            ModsToUnpack.Clear();
            _uniqueModsToUnpack.Clear();
        }
    }

    /// <summary>
    /// Clean up invalid directory if necessary.
    /// Add successfully extracted mods.
    /// </summary>
    private void AddNewMod(FileInfo file, DirectoryInfo? dir, Exception? error)
    {
        if (error != null)
        {
            if (dir != null && Directory.Exists(dir.FullName))
                try
                {
                    Directory.Delete(dir.FullName, true);
                }
                catch (Exception e)
                {
                    Penumbra.Log.Error($"Error cleaning up failed mod extraction of {file.FullName} to {dir.FullName}:\n{e}");
                }

            if (error is not OperationCanceledException)
                Penumbra.Log.Error($"Error extracting {file.FullName}, mod skipped:\n{error}");
        }
        else if (dir != null)
        {
            Penumbra.Log.Debug($"Adding newly installed mod to queue: {dir.FullName}");
            _modsToAdd.Enqueue(dir);
        }
    }

    internal readonly record struct UnpackRequest(List<string> Paths, TaskCompletionSource<ModImportResult[]> TaskCompletionSource);
}
