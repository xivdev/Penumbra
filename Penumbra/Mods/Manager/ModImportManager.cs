using Dalamud.Interface.ImGuiNotification;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Import;
using Penumbra.Mods.Editor;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public class ModImportManager(ModManager modManager, Configuration config, ModEditor modEditor, MigrationManager migrationManager) : IDisposable, IService
{
    private readonly ConcurrentQueue<string[]> _modsToUnpack = new();

    /// <summary> Mods need to be added thread-safely outside of iteration. </summary>
    private readonly ConcurrentQueue<DirectoryInfo> _modsToAdd = new();

    private TexToolsImporter? _import;


    internal IEnumerable<string[]> ModBatches
        => _modsToUnpack;

    internal IEnumerable<DirectoryInfo> AddableMods
        => _modsToAdd;


    public void TryUnpacking()
    {
        if (Importing || !_modsToUnpack.TryDequeue(out var newMods))
            return;

        var files = newMods.Where(s =>
        {
            if (File.Exists(s))
                return true;

            Penumbra.Messager.NotificationMessage($"Failed to import queued mod at {s}, the file does not exist.", NotificationType.Warning,
                false);
            return false;
        }).Select(s => new FileInfo(s)).ToArray();

        Penumbra.Log.Debug($"Unpacking mods: {string.Join("\n\t", files.Select(f => f.FullName))}.");
        if (files.Length == 0)
            return;

        _import = new TexToolsImporter(files.Length, files, AddNewMod, config, modEditor, modManager, modEditor.Compactor, migrationManager);
    }

    public bool Importing
        => _import != null;

    public bool IsImporting([NotNullWhen(true)] out TexToolsImporter? importer)
    {
        importer = _import;
        return _import != null;
    }

    public void AddUnpack(IEnumerable<string> paths)
        => AddUnpack(paths.ToArray());

    public void AddUnpack(params string[] paths)
    {
        Penumbra.Log.Debug($"Adding mods to install: {string.Join("\n\t", paths)}");
        _modsToUnpack.Enqueue(paths);
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

        modManager.AddMod(directory, true);
        mod = modManager.LastOrDefault();
        return mod != null && mod.ModPath == directory;
    }

    public void Dispose()
    {
        ClearImport();
        _modsToAdd.Clear();
        _modsToUnpack.Clear();
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
}
