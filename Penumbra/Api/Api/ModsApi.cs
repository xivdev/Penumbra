using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.Api.Enums;
using Penumbra.Communication;
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
    private readonly Logger              _log;

    public ModsApi(ModManager modManager, ModImportManager modImportManager, Configuration config, ModFileSystem modFileSystem,
        CommunicatorService communicator, MigrationManager migrationManager, Logger log)
    {
        _modManager       = modManager;
        _modImportManager = modImportManager;
        _config           = config;
        _modFileSystem    = modFileSystem;
        _communicator     = communicator;
        _migrationManager = migrationManager;
        _log              = log;
        _communicator.ModPathChanged.Subscribe(OnModPathChanged, ModPathChanged.Priority.ApiMods);
        _communicator.PcpCreation.Subscribe(OnPcpCreation, PcpCreation.Priority.ApiMods);
        _communicator.PcpParsing.Subscribe(OnPcpParsing, PcpParsing.Priority.ApiMods);
    }

    private void OnPcpParsing(in PcpParsing.Arguments arguments)
        => ParsingPcp?.Invoke(arguments.JObject, arguments.Mod.Identifier, arguments.Collection?.Identity.Id ?? Guid.Empty);

    private void OnPcpCreation(in PcpCreation.Arguments arguments)
        => CreatingPcp?.Invoke(arguments.JObject, arguments.ObjectIndex, arguments.DirectoryPath);

    private void OnModPathChanged(in ModPathChanged.Arguments arguments)
    {
        switch (arguments.Type)
        {
            case ModPathChangeType.Deleted when arguments.OldDirectory is not null: ModDeleted?.Invoke(arguments.OldDirectory.Name); break;
            case ModPathChangeType.Added when arguments.NewDirectory is not null:   ModAdded?.Invoke(arguments.NewDirectory.Name); break;
            case ModPathChangeType.Moved when arguments is { NewDirectory: not null, OldDirectory: not null }:
                ModMoved?.Invoke(arguments.OldDirectory.Name, arguments.NewDirectory.Name);
                break;
        }
    }

    public void Dispose()
    {
        _communicator.ModPathChanged.Unsubscribe(OnModPathChanged);
        _communicator.PcpCreation.Unsubscribe(OnPcpCreation);
        _communicator.PcpParsing.Unsubscribe(OnPcpParsing);
    }

    public Dictionary<string, string> GetModList()
        => _modManager.ToDictionary(m => m.ModPath.Name, m => m.Name);

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
            new FileCompactor(_log).StartMassCompact(dir.EnumerateFiles("*.*", SearchOption.AllDirectories),
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

    public event Action<string>?                  ModDeleted;
    public event Action<string>?                  ModAdded;
    public event Action<string, string>?          ModMoved;
    public event Action<JObject, ushort, string>? CreatingPcp;
    public event Action<JObject, string, Guid>?   ParsingPcp;

    public (PenumbraApiEc, string, bool, bool) GetModPath(string modDirectory, string modName)
    {
        if (!_modManager.TryGetMod(modDirectory, modName, out var mod)
         || !_modFileSystem.TryGetValue(mod, out var leaf))
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
         || !_modFileSystem.TryGetValue(mod, out var leaf))
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
            ? mod.ChangedItems.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToInternalObject())
            : [];

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> GetChangedItemAdapterDictionary()
        => new ModChangedItemAdapter(new WeakReference<ModStorage>(_modManager));

    public IReadOnlyList<(string ModDirectory, IReadOnlyDictionary<string, object?> ChangedItems)> GetChangedItemAdapterList()
        => new ModChangedItemAdapter(new WeakReference<ModStorage>(_modManager));
}
