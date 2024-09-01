using Dalamud.Interface.ImGuiNotification;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.GameData.Files;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Penumbra.Services;

public class MigrationManager(Configuration config) : IService
{
    public enum TaskType : byte
    {
        None,
        MdlMigration,
        MdlRestoration,
        MdlCleanup,
        MtrlMigration,
        MtrlRestoration,
        MtrlCleanup,
    }

    public class MigrationData(bool hasUnchanged)

    {
        public          int  Changed;
        public          int  Unchanged;
        public          int  Failed;
        public          bool HasData;
        public readonly bool HasUnchanged = hasUnchanged;

        public int Total
            => Changed + Unchanged + Failed;

        public void Init()
        {
            Changed   = 0;
            Unchanged = 0;
            Failed    = 0;
            HasData   = true;
        }
    }

    private Task?                    _currentTask;
    private CancellationTokenSource? _source;

    public TaskType CurrentTask { get; private set; }

    public readonly MigrationData MdlMigration    = new(true);
    public readonly MigrationData MtrlMigration   = new(true);
    public readonly MigrationData MdlCleanup      = new(false);
    public readonly MigrationData MtrlCleanup     = new(false);
    public readonly MigrationData MdlRestoration  = new(false);
    public readonly MigrationData MtrlRestoration = new(false);


    public bool IsRunning
        => _currentTask is { IsCompleted: false };

    public void CleanMdlBackups(string path)
        => CleanBackups(path, "*.mdl.bak", "model", MdlCleanup, TaskType.MdlCleanup);

    public void CleanMtrlBackups(string path)
        => CleanBackups(path, "*.mtrl.bak", "material", MtrlCleanup, TaskType.MtrlCleanup);

    public void Await()
        => _currentTask?.Wait();

    private void CleanBackups(string path, string extension, string fileType, MigrationData data, TaskType type)
    {
        if (IsRunning)
            return;

        _source = new CancellationTokenSource();
        var token = _source.Token;
        _currentTask = Task.Run(() =>
        {
            CurrentTask = type;
            data.Init();
            foreach (var file in Directory.EnumerateFiles(path, extension, SearchOption.AllDirectories))
            {
                if (token.IsCancellationRequested)
                    return;

                try
                {
                    File.Delete(file);
                    ++data.Changed;
                    Penumbra.Log.Debug($"Deleted {fileType} backup file {file}.");
                }
                catch (Exception ex)
                {
                    Penumbra.Messager.NotificationMessage(ex, $"Failed to delete {fileType} backup file {file}", NotificationType.Warning);
                    ++data.Failed;
                }
            }
        }, token);
    }

    public void RestoreMdlBackups(string path)
        => RestoreBackups(path, "*.mdl.bak", "model", MdlRestoration, TaskType.MdlRestoration);

    public void RestoreMtrlBackups(string path)
        => RestoreBackups(path, "*.mtrl.bak", "material", MtrlRestoration, TaskType.MtrlRestoration);

    private void RestoreBackups(string path, string extension, string fileType, MigrationData data, TaskType type)
    {
        if (IsRunning)
            return;

        _source = new CancellationTokenSource();
        var token = _source.Token;
        _currentTask = Task.Run(() =>
        {
            CurrentTask = type;
            data.Init();
            foreach (var file in Directory.EnumerateFiles(path, extension, SearchOption.AllDirectories))
            {
                if (token.IsCancellationRequested)
                    return;

                var target = file[..^4];
                try
                {
                    File.Copy(file, target, true);
                    ++data.Changed;
                    Penumbra.Log.Debug($"Restored {fileType} backup file {file} to {target}.");
                }
                catch (Exception ex)
                {
                    Penumbra.Messager.NotificationMessage(ex, $"Failed to restore {fileType} backup file {file} to {target}",
                        NotificationType.Warning);
                    ++data.Failed;
                }
            }
        }, token);
    }

    public void MigrateMdlDirectory(string path, bool createBackups)
        => MigrateDirectory(path, createBackups, "*.mdl", "model", MdlMigration, TaskType.MdlMigration, "from V5 to V6", "V6",
            (file, fileData, backups) =>
            {
                var mdl = new MdlFile(fileData);
                return MigrateModel(file, mdl, backups);
            });

    public void MigrateMtrlDirectory(string path, bool createBackups)
        => MigrateDirectory(path, createBackups, "*.mtrl", "material", MtrlMigration, TaskType.MtrlMigration, "to Dawntrail", "Dawntrail",
            (file, fileData, backups) =>
            {
                var mtrl = new MtrlFile(fileData);
                return MigrateMaterial(file, mtrl, backups);
            }
        );

    private void MigrateDirectory(string path, bool createBackups, string extension, string fileType, MigrationData data, TaskType type,
        string action, string state, Func<string, byte[], bool, bool> func)
    {
        if (IsRunning)
            return;

        _source = new CancellationTokenSource();
        var token = _source.Token;
        _currentTask = Task.Run(() =>
        {
            CurrentTask = type;
            data.Init();
            foreach (var file in Directory.EnumerateFiles(path, extension, SearchOption.AllDirectories))
            {
                if (token.IsCancellationRequested)
                    return;

                var timer = Stopwatch.StartNew();
                try
                {
                    var fileData = File.ReadAllBytes(file);
                    if (func(file, fileData, createBackups))
                    {
                        ++data.Changed;
                        Penumbra.Log.Debug($"Migrated {fileType} file {file} {action} in {timer.ElapsedMilliseconds} ms.");
                    }
                    else
                    {
                        ++data.Unchanged;
                        Penumbra.Log.Verbose($"Verified that {fileType} file {file} is already {state} in {timer.ElapsedMilliseconds} ms.");
                    }
                }
                catch (Exception ex)
                {
                    ++data.Failed;
                    Penumbra.Messager.NotificationMessage(ex,
                        $"Failed to migrate {fileType} file {file} to {state} in {timer.ElapsedMilliseconds} ms",
                        NotificationType.Warning);
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

    /// <summary> Writes or migrates a .mdl file during extraction from a regular archive. </summary>
    public void MigrateMdlDuringExtraction(IReader reader, string directory, ExtractionOptions options)
    {
        if (!config.MigrateImportedModelsToV6)
        {
            reader.WriteEntryToDirectory(directory, options);
            return;
        }

        var       path = Path.Combine(directory, reader.Entry.Key!);
        using var s    = new MemoryStream();
        using var e    = reader.OpenEntryStream();
        e.CopyTo(s);
        s.Position = 0;
        using var b       = new BinaryReader(s);
        var       version = b.ReadUInt32();
        if (version == MdlFile.V5)
        {
            var data = s.ToArray();
            var mdl  = new MdlFile(data);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            MigrateModel(path, mdl, false);
            Penumbra.Log.Debug($"Migrated model {reader.Entry.Key} from V5 to V6 during import.");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var f = File.Open(path, FileMode.Create, FileAccess.Write);
            s.Seek(0, SeekOrigin.Begin);
            s.WriteTo(f);
        }
    }

    public void MigrateMtrlDuringExtraction(IReader reader, string directory, ExtractionOptions options)
    {
        if (!config.MigrateImportedMaterialsToLegacy || true) // TODO change when this is working
        {
            reader.WriteEntryToDirectory(directory, options);
            return;
        }

        var       path = Path.Combine(directory, reader.Entry.Key);
        using var s    = new MemoryStream();
        using var e    = reader.OpenEntryStream();
        e.CopyTo(s);
        var file = new MtrlFile(s.GetBuffer());

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var f = File.Open(path, FileMode.Create, FileAccess.Write);
        if (file.IsDawntrail)
        {
            file.MigrateToDawntrail();
            Penumbra.Log.Debug($"Migrated material {reader.Entry.Key} to Dawntrail during import.");
            f.Write(file.Write());
        }
        else
        {
            s.Seek(0, SeekOrigin.Begin);
            s.WriteTo(f);
        }
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

        try
        {
            var mdl = new MdlFile(data);
            if (!mdl.ConvertV5ToV6())
                return data;

            data = mdl.Write();
            Penumbra.Log.Debug($"Migrated model {path} from V5 to V6 during import.");
            return data;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"Failed to migrate model {path} from V5 to V6 during import:\n{ex}");
            return data;
        }
    }

    /// <summary> Update the data of a .mtrl file during TTMP extraction. Returns either the existing array or a new one. </summary>
    public byte[] MigrateTtmpMaterial(string path, byte[] data)
    {
        if (!config.MigrateImportedMaterialsToLegacy || true) // TODO fix when this is working
            return data;

        try
        {
            var mtrl = new MtrlFile(data);
            if (mtrl.IsDawntrail)
                return data;

            mtrl.MigrateToDawntrail();
            data = mtrl.Write();
            Penumbra.Log.Debug($"Migrated material {path} to Dawntrail during import.");
            return data;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"Failed to migrate material {path} to Dawntrail during import:\n{ex}");
            return data;
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
}
