using OtterGui.Compression;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Api.Api;

public class ModsApi : IPenumbraApiMods, IApiService, IDisposable
{
    private readonly CommunicatorService _communicator;
    private readonly ModManager          _modManager;
    private readonly ModImportManager    _modImportManager;
    private readonly Configuration       _config;
    private readonly ModFileSystem       _modFileSystem;
    private readonly MigrationManager    _migrationManager;

    public ModsApi(ModManager modManager, ModImportManager modImportManager, Configuration config, ModFileSystem modFileSystem,
        CommunicatorService communicator, MigrationManager migrationManager)
    {
        _modManager       = modManager;
        _modImportManager = modImportManager;
        _config           = config;
        _modFileSystem    = modFileSystem;
        _communicator     = communicator;
        _migrationManager = migrationManager;
        _communicator.ModPathChanged.Subscribe(OnModPathChanged, ModPathChanged.Priority.ApiMods);
    }

    private void OnModPathChanged(ModPathChangeType type, Mod mod, DirectoryInfo? oldDirectory, DirectoryInfo? newDirectory)
    {
        switch (type)
        {
            case ModPathChangeType.Deleted when oldDirectory != null:
                ModDeleted?.Invoke(oldDirectory.Name);
                break;
            case ModPathChangeType.Added when newDirectory != null:
                ModAdded?.Invoke(newDirectory.Name);
                break;
            case ModPathChangeType.Moved when newDirectory != null && oldDirectory != null:
                ModMoved?.Invoke(oldDirectory.Name, newDirectory.Name);
                break;
        }
    }

    public void Dispose()
        => _communicator.ModPathChanged.Unsubscribe(OnModPathChanged);

    public Dictionary<string, string> GetModList()
        => _modManager.ToDictionary(m => m.ModPath.Name, m => m.Name.Text);

    public PenumbraApiEc InstallMod(string modFilePackagePath)
    {
        if (!File.Exists(modFilePackagePath))
            return ApiHelpers.Return(PenumbraApiEc.FileMissing, ApiHelpers.Args("ModFilePackagePath", modFilePackagePath));

        _modImportManager.AddUnpack(modFilePackagePath);
        return ApiHelpers.Return(PenumbraApiEc.Success, ApiHelpers.Args("ModFilePackagePath", modFilePackagePath));
    }

    public PenumbraApiEc ReloadMod(string modDirectory, string modName)
    {
        if (!_modManager.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.ModMissing, ApiHelpers.Args("ModDirectory", modDirectory, "ModName", modName));

        _modManager.ReloadMod(mod);
        return ApiHelpers.Return(PenumbraApiEc.Success, ApiHelpers.Args("ModDirectory", modDirectory, "ModName", modName));
    }

    public PenumbraApiEc AddMod(string modDirectory)
    {
        var args = ApiHelpers.Args("ModDirectory", modDirectory);

        var dir = new DirectoryInfo(Path.Join(_modManager.BasePath.FullName, Path.GetFileName(modDirectory)));
        if (!dir.Exists)
            return ApiHelpers.Return(PenumbraApiEc.FileMissing, args);

        if (dir.Parent == null
         || Path.TrimEndingDirectorySeparator(Path.GetFullPath(_modManager.BasePath.FullName))
         != Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir.Parent.FullName)))
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        _modManager.AddMod(dir, true);
        if (_config.MigrateImportedModelsToV6)
        {
            _migrationManager.MigrateMdlDirectory(dir.FullName, false);
            _migrationManager.Await();
        }

        if (_config.UseFileSystemCompression)
            new FileCompactor(Penumbra.Log).StartMassCompact(dir.EnumerateFiles("*.*", SearchOption.AllDirectories),
                CompressionAlgorithm.Xpress8K, false);

        return ApiHelpers.Return(PenumbraApiEc.Success, args);
    }

    public PenumbraApiEc DeleteMod(string modDirectory, string modName)
    {
        if (!_modManager.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.NothingChanged, ApiHelpers.Args("ModDirectory", modDirectory, "ModName", modName));

        _modManager.DeleteMod(mod);
        return ApiHelpers.Return(PenumbraApiEc.Success, ApiHelpers.Args("ModDirectory", modDirectory, "ModName", modName));
    }

    public event Action<string>?         ModDeleted;
    public event Action<string>?         ModAdded;
    public event Action<string, string>? ModMoved;

    public (PenumbraApiEc, string, bool, bool) GetModPath(string modDirectory, string modName)
    {
        if (!_modManager.TryGetMod(modDirectory, modName, out var mod)
         || !_modFileSystem.FindLeaf(mod, out var leaf))
            return (PenumbraApiEc.ModMissing, string.Empty, false, false);

        var fullPath      = leaf.FullName();
        var isDefault     = ModFileSystem.ModHasDefaultPath(mod, fullPath);
        var isNameDefault = isDefault || ModFileSystem.ModHasDefaultPath(mod, leaf.Name);
        return (PenumbraApiEc.Success, fullPath, !isDefault, !isNameDefault);
    }

    public PenumbraApiEc SetModPath(string modDirectory, string modName, string newPath)
    {
        if (newPath.Length == 0)
            return PenumbraApiEc.InvalidArgument;

        if (!_modManager.TryGetMod(modDirectory, modName, out var mod)
         || !_modFileSystem.FindLeaf(mod, out var leaf))
            return PenumbraApiEc.ModMissing;

        try
        {
            _modFileSystem.RenameAndMove(leaf, newPath);
            return PenumbraApiEc.Success;
        }
        catch
        {
            return PenumbraApiEc.PathRenameFailed;
        }
    }

    public Dictionary<string, object?> GetChangedItems(string modDirectory, string modName)
        => _modManager.TryGetMod(modDirectory, modName, out var mod)
            ? mod.ChangedItems.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToInternalObject())
            : [];
}
