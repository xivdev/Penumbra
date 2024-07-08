using Dalamud.Interface.ImGuiNotification;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.GameData.Files;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Penumbra.Services;

public class MigrationManager(Configuration config) : IService
{
    private Task?                    _currentTask;
    private CancellationTokenSource? _source;

    public bool HasCleanUpTask   { get; private set; }
    public bool HasMigrationTask { get; private set; }
    public bool HasRestoreTask   { get; private set; }

    public bool IsMigrationTask   { get; private set; }
    public bool IsRestorationTask { get; private set; }
    public bool IsCleanupTask     { get; private set; }


    public int Restored     { get; private set; }
    public int RestoreFails { get; private set; }

    public int CleanedUp { get; private set; }

    public int CleanupFails { get; private set; }

    public int Migrated { get; private set; }

    public int Unchanged { get; private set; }

    public int Failed { get; private set; }

    public bool IsRunning
        => _currentTask is { IsCompleted: false };

    /// <summary> Writes or migrates a .mdl file during extraction from a regular archive. </summary>
    public void MigrateMdlDuringExtraction(IReader reader, string directory, ExtractionOptions options)
    {
        if (!config.MigrateImportedModelsToV6)
        {
            reader.WriteEntryToDirectory(directory, options);
            return;
        }

        var       path = Path.Combine(directory, reader.Entry.Key);
        using var s    = new MemoryStream();
        using var e    = reader.OpenEntryStream();
        e.CopyTo(s);
        using var b       = new BinaryReader(s);
        var       version = b.ReadUInt32();
        if (version == MdlFile.V5)
        {
            var data = s.ToArray();
            var mdl  = new MdlFile(data);
            MigrateModel(path, mdl, false);
            Penumbra.Log.Debug($"Migrated model {reader.Entry.Key} from V5 to V6 during import.");
        }
        else
        {
            using var f = File.Open(path, FileMode.Create, FileAccess.Write);
            s.Seek(0, SeekOrigin.Begin);
            s.WriteTo(f);
        }
    }

    public void CleanBackups(string path)
    {
        if (IsRunning)
            return;

        _source = new CancellationTokenSource();
        var token = _source.Token;
        _currentTask = Task.Run(() =>
        {
            HasCleanUpTask    = true;
            IsCleanupTask     = true;
            IsMigrationTask   = false;
            IsRestorationTask = false;
            CleanedUp         = 0;
            CleanupFails      = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*.mdl.bak", SearchOption.AllDirectories))
            {
                if (token.IsCancellationRequested)
                    return;

                try
                {
                    File.Delete(file);
                    ++CleanedUp;
                    Penumbra.Log.Debug($"Deleted model backup file {file}.");
                }
                catch (Exception ex)
                {
                    Penumbra.Messager.NotificationMessage(ex, $"Failed to delete model backup file {file}", NotificationType.Warning);
                    ++CleanupFails;
                }
            }
        }, token);
    }

    public void RestoreBackups(string path)
    {
        if (IsRunning)
            return;

        _source = new CancellationTokenSource();
        var token = _source.Token;
        _currentTask = Task.Run(() =>
        {
            HasRestoreTask    = true;
            IsCleanupTask     = false;
            IsMigrationTask   = false;
            IsRestorationTask = true;
            CleanedUp         = 0;
            CleanupFails      = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*.mdl.bak", SearchOption.AllDirectories))
            {
                if (token.IsCancellationRequested)
                    return;

                var target = file[..^4];
                try
                {
                    File.Copy(file, target, true);
                    ++Restored;
                    Penumbra.Log.Debug($"Restored model backup file {file} to {target}.");
                }
                catch (Exception ex)
                {
                    Penumbra.Messager.NotificationMessage(ex, $"Failed to restore model backup file {file} to {target}",
                        NotificationType.Warning);
                    ++RestoreFails;
                }
            }
        }, token);
    }

    /// <summary> Update the data of a .mdl file during TTMP extraction. Returns either the existing array or a new one. </summary>
    public byte[] MigrateTtmpModel(string path, byte[] data)
    {
        FixLodNum(data);
        if (!config.MigrateImportedModelsToV6)
            return data;

        var version = BitConverter.ToUInt32(data);
        if (version != 5)
            return data;

        var mdl = new MdlFile(data);
        if (!mdl.ConvertV5ToV6())
            return data;

        data = mdl.Write();
        Penumbra.Log.Debug($"Migrated model {path} from V5 to V6 during import.");
        return data;
    }

    public void MigrateDirectory(string path, bool createBackups)
    {
        if (IsRunning)
            return;

        _source = new CancellationTokenSource();
        var token = _source.Token;
        _currentTask = Task.Run(() =>
        {
            HasMigrationTask  = true;
            IsCleanupTask     = false;
            IsMigrationTask   = true;
            IsRestorationTask = false;
            Unchanged         = 0;
            Migrated          = 0;
            Failed            = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*.mdl", SearchOption.AllDirectories))
            {
                if (token.IsCancellationRequested)
                    return;

                var timer = Stopwatch.StartNew();
                try
                {
                    var data = File.ReadAllBytes(file);
                    var mdl  = new MdlFile(data);
                    if (MigrateModel(file, mdl, createBackups))
                    {
                        ++Migrated;
                        Penumbra.Log.Debug($"Migrated model file {file} from V5 to V6 in {timer.ElapsedMilliseconds} ms.");
                    }
                    else
                    {
                        ++Unchanged;
                        Penumbra.Log.Verbose($"Verified that model file {file} is already V6 in {timer.ElapsedMilliseconds} ms.");
                    }
                }
                catch (Exception ex)
                {
                    Penumbra.Messager.NotificationMessage(ex, $"Failed to migrate model file {file} to V6 in {timer.ElapsedMilliseconds} ms",
                        NotificationType.Warning);
                    ++Failed;
                }
            }
        }, token);
    }

    public void Cancel()
    {
        _source?.Cancel();
        _source      = null;
        _currentTask = null;
    }

    private static void FixLodNum(byte[] data)
    {
        const int modelHeaderLodOffset = 22;

        // Model file header LOD num
        data[64] = 1;

        // Model header LOD num
        var stackSize           = BitConverter.ToUInt32(data, 4);
        var runtimeBegin        = stackSize + 0x44;
        var stringsLengthOffset = runtimeBegin + 4;
        var stringsLength       = BitConverter.ToUInt32(data, (int)stringsLengthOffset);
        var modelHeaderStart    = stringsLengthOffset + stringsLength + 4;
        data[modelHeaderStart + modelHeaderLodOffset] = 1;
    }

    public static bool TryMigrateSingleModel(string path, bool createBackup)
    {
        try
        {
            var data = File.ReadAllBytes(path);
            var mdl  = new MdlFile(data);
            return MigrateModel(path, mdl, createBackup);
        }
        catch (Exception ex)
        {
            Penumbra.Messager.NotificationMessage(ex, $"Failed to migrate the model {path} to V6", NotificationType.Warning);
            return false;
        }
    }

    public static bool TryMigrateSingleMaterial(string path, bool createBackup)
    {
        try
        {
            var data = File.ReadAllBytes(path);
            var mtrl = new MtrlFile(data);
            return MigrateMaterial(path, mtrl, createBackup);
        }
        catch (Exception ex)
        {
            Penumbra.Messager.NotificationMessage(ex, $"Failed to migrate the material {path} to Dawntrail", NotificationType.Warning);
            return false;
        }
    }

    private static bool MigrateModel(string path, MdlFile mdl, bool createBackup)
    {
        if (!mdl.ConvertV5ToV6())
            return false;

        var data = mdl.Write();
        if (createBackup)
            File.Copy(path, Path.ChangeExtension(path, ".mdl.bak"));
        File.WriteAllBytes(path, data);
        return true;
    }

    private static bool MigrateMaterial(string path, MtrlFile mtrl, bool createBackup)
    {
        if (!mtrl.MigrateToDawntrail())
            return false;

        var data = mtrl.Write();

        mtrl.Write();
        if (createBackup)
            File.Copy(path, Path.ChangeExtension(path, ".mtrl.bak"));
        File.WriteAllBytes(path, data);
        return true;
    }
}
