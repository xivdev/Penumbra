using Dalamud.Plugin;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.DependencyInjection;
using OtterGui;
using OtterGui.Log;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Util;
using Penumbra.Collections;
using Penumbra.Collections.Cache;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;
using Penumbra.Collections.Manager;
using Penumbra.UI.Tabs;
using ChangedItemClick = Penumbra.Communication.ChangedItemClick;
using ChangedItemHover = Penumbra.Communication.ChangedItemHover;
using OtterGui.Tasks;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.UI;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;

namespace Penumbra;

public class Penumbra : IDalamudPlugin
{
    public string Name
        => "Penumbra";

    public static readonly Logger      Log = new();
    public static          MessageService Messager { get; private set; } = null!;

    private readonly ValidityChecker         _validityChecker;
    private readonly ResidentResourceManager _residentResources;
    private readonly TempModManager          _tempMods;
    private readonly TempCollectionManager   _tempCollections;
    private readonly ModManager              _modManager;
    private readonly CollectionManager       _collectionManager;
    private readonly Configuration           _config;
    private readonly CharacterUtility        _characterUtility;
    private readonly RedrawService           _redrawService;
    private readonly CommunicatorService     _communicatorService;
    private          PenumbraWindowSystem?   _windowSystem;
    private          bool                    _disposed;

    private readonly ServiceProvider _services;

    public Penumbra(DalamudPluginInterface pluginInterface)
    {
        try
        {
            var       startTimer = new StartTracker();
            using var timer      = startTimer.Measure(StartTimeType.Total);
            _services        = ServiceManager.CreateProvider(this, pluginInterface, Log, startTimer);
            Messager             = _services.GetRequiredService<MessageService>();
            _validityChecker = _services.GetRequiredService<ValidityChecker>();
            var startup = _services.GetRequiredService<DalamudServices>().GetDalamudConfig(DalamudServices.WaitingForPluginsOption, out bool s)
                ? s.ToString()
                : "Unknown";
            Log.Information(
                $"Loading Penumbra Version {_validityChecker.Version}, Commit #{_validityChecker.CommitHash} with Waiting For Plugins: {startup}...");
            _services.GetRequiredService<BackupService>(); // Initialize because not required anywhere else.
            _config            = _services.GetRequiredService<Configuration>();
            _characterUtility  = _services.GetRequiredService<CharacterUtility>();
            _tempMods          = _services.GetRequiredService<TempModManager>();
            _residentResources = _services.GetRequiredService<ResidentResourceManager>();
            _services.GetRequiredService<ResourceManagerService>(); // Initialize because not required anywhere else.
            _modManager          = _services.GetRequiredService<ModManager>();
            _collectionManager   = _services.GetRequiredService<CollectionManager>();
            _tempCollections     = _services.GetRequiredService<TempCollectionManager>();
            _redrawService       = _services.GetRequiredService<RedrawService>();
            _communicatorService = _services.GetRequiredService<CommunicatorService>();
            _services.GetRequiredService<ResourceService>(); // Initialize because not required anywhere else.
            _services.GetRequiredService<ModCacheManager>(); // Initialize because not required anywhere else.
            _services.GetRequiredService<ModelResourceHandleUtility>(); // Initialize because not required anywhere else.
            _collectionManager.Caches.CreateNecessaryCaches();
            using (var t = _services.GetRequiredService<StartTracker>().Measure(StartTimeType.PathResolver))
            {
                _services.GetRequiredService<PathResolver>();
            }

            _services.GetRequiredService<SkinFixer>();

            _services.GetRequiredService<DalamudSubstitutionProvider>(); // Initialize before Interface.
            SetupInterface();
            SetupApi();

            _validityChecker.LogExceptions();
            Log.Information(
                $"Penumbra Version {_validityChecker.Version}, Commit #{_validityChecker.CommitHash} successfully Loaded from {pluginInterface.SourceRepository}.");
            OtterTex.NativeDll.Initialize(pluginInterface.AssemblyLocation.DirectoryName);
            Log.Information($"Loading native OtterTex assembly from {OtterTex.NativeDll.Directory}.");

            if (_characterUtility.Ready)
                _residentResources.Reload();
        }
        catch (Exception ex)
        {
            Log.Error($"Error constructing Penumbra, Disposing again:\n{ex}");
            Dispose();
            throw;
        }
    }

    private void SetupApi()
    {
        using var timer = _services.GetRequiredService<StartTracker>().Measure(StartTimeType.Api);
        var       api   = _services.GetRequiredService<IPenumbraApi>();
        _services.GetRequiredService<PenumbraIpcProviders>();
        _communicatorService.ChangedItemHover.Subscribe(it =>
        {
            if (it is (Item, FullEquipType))
                ImGui.TextUnformatted("Left Click to create an item link in chat.");
        }, ChangedItemHover.Priority.Link);

        _communicatorService.ChangedItemClick.Subscribe((button, it) =>
        {
            if (button == MouseButton.Left && it is (Item item, FullEquipType type))
                Messager.LinkItem(item);
        }, ChangedItemClick.Priority.Link);
    }

    private void SetupInterface()
    {
        AsyncTask.Run(() =>
            {
                using var tInterface = _services.GetRequiredService<StartTracker>().Measure(StartTimeType.Interface);
                var       system     = _services.GetRequiredService<PenumbraWindowSystem>();
                system.Window.Setup(this, _services.GetRequiredService<ConfigTabBar>());
                _services.GetRequiredService<CommandHandler>();
                if (!_disposed)
                    _windowSystem = system;
                else
                    system.Dispose();
            }
        );
    }

    public bool SetEnabled(bool enabled)
    {
        if (enabled == _config.EnableMods)
            return false;

        _config.EnableMods = enabled;
        if (enabled)
        {
            if (_characterUtility.Ready)
            {
                _collectionManager.Active.Default.SetFiles(_characterUtility);
                _residentResources.Reload();
                _redrawService.RedrawAll(RedrawType.Redraw);
            }
        }
        else
        {
            if (_characterUtility.Ready)
            {
                _characterUtility.ResetAll();
                _residentResources.Reload();
                _redrawService.RedrawAll(RedrawType.Redraw);
            }
        }

        _config.Save();
        _communicatorService.EnabledChanged.Invoke(enabled);

        return true;
    }

    public void ForceChangelogOpen()
        => _windowSystem?.ForceChangelogOpen();

    public void Dispose()
    {
        if (_disposed)
            return;

        _services?.Dispose();
        _disposed = true;
    }

    public string GatherSupportInformation()
    {
        var sb     = new StringBuilder(10240);
        var exists = _config.ModDirectory.Length > 0 && Directory.Exists(_config.ModDirectory);
        var drive  = exists ? new DriveInfo(new DirectoryInfo(_config.ModDirectory).Root.FullName) : null;
        sb.AppendLine("**Settings**");
        sb.Append($"> **`Plugin Version:              `** {_validityChecker.Version}\n");
        sb.Append($"> **`Commit Hash:                 `** {_validityChecker.CommitHash}\n");
        sb.Append($"> **`Enable Mods:                 `** {_config.EnableMods}\n");
        sb.Append($"> **`Enable HTTP API:             `** {_config.EnableHttpApi}\n");
        var isWine = Dalamud.Utility.Util.IsWine();
        sb.Append($"> **`Operating System:            `** {(isWine ? "Mac/Linux (Wine)" : "Windows")}\n");
        if (isWine)
            sb.Append($"> **`Character Type Locale:       `** {GatherFileSystemSanityCheckVariables()}\n");
        sb.Append($"> **`Root Directory:              `** `{_config.ModDirectory}`, {(exists ? "Exists" : "Not Existing")}\n");
        sb.Append(
            $"> **`Free Drive Space:            `** {(drive != null ? Functions.HumanReadableSize(drive.AvailableFreeSpace) : "Unknown")}\n");
        sb.Append($"> **`Auto-Deduplication:          `** {_config.AutoDeduplicateOnImport}\n");
        sb.Append($"> **`Debug Mode:                  `** {_config.DebugMode}\n");
        sb.Append(
            $"> **`Synchronous Load (Dalamud):  `** {(_services.GetRequiredService<DalamudServices>().GetDalamudConfig(DalamudServices.WaitingForPluginsOption, out bool v) ? v.ToString() : "Unknown")}\n");
        sb.Append(
            $"> **`Logging:                     `** Log: {_config.Ephemeral.EnableResourceLogging}, Watcher: {_config.Ephemeral.EnableResourceWatcher} ({_config.MaxResourceWatcherRecords})\n");
        sb.Append($"> **`Use Ownership:               `** {_config.UseOwnerNameForCharacterCollection}\n");
        sb.AppendLine("**Mods**");
        sb.Append($"> **`Installed Mods:              `** {_modManager.Count}\n");
        sb.Append($"> **`Mods with Config:            `** {_modManager.Count(m => m.HasOptions)}\n");
        sb.Append(
            $"> **`Mods with File Redirections: `** {_modManager.Count(m => m.TotalFileCount > 0)}, Total: {_modManager.Sum(m => m.TotalFileCount)}\n");
        sb.Append(
            $"> **`Mods with FileSwaps:         `** {_modManager.Count(m => m.TotalSwapCount > 0)}, Total: {_modManager.Sum(m => m.TotalSwapCount)}\n");
        sb.Append(
            $"> **`Mods with Meta Manipulations:`** {_modManager.Count(m => m.TotalManipulations > 0)}, Total {_modManager.Sum(m => m.TotalManipulations)}\n");
        sb.Append($"> **`IMC Exceptions Thrown:       `** {_validityChecker.ImcExceptions.Count}\n");
        sb.Append(
            $"> **`#Temp Mods:                  `** {_tempMods.Mods.Sum(kvp => kvp.Value.Count) + _tempMods.ModsForAllCollections.Count}\n");

        void PrintCollection(ModCollection c, CollectionCache _)
            => sb.Append($"**Collection {c.AnonymizedName}**\n"
              + $"> **`Inheritances:                 `** {c.DirectlyInheritsFrom.Count}\n"
              + $"> **`Enabled Mods:                 `** {c.ActualSettings.Count(s => s is { Enabled: true })}\n"
              + $"> **`Conflicts (Solved/Total):     `** {c.AllConflicts.SelectMany(x => x).Sum(x => x.HasPriority && x.Solved ? x.Conflicts.Count : 0)}/{c.AllConflicts.SelectMany(x => x).Sum(x => x.HasPriority ? x.Conflicts.Count : 0)}\n");

        sb.AppendLine("**Collections**");
        sb.Append($"> **`#Collections:                 `** {_collectionManager.Storage.Count - 1}\n");
        sb.Append($"> **`#Temp Collections:            `** {_tempCollections.Count}\n");
        sb.Append($"> **`Active Collections:           `** {_collectionManager.Caches.Count}\n");
        sb.Append($"> **`Base Collection:              `** {_collectionManager.Active.Default.AnonymizedName}\n");
        sb.Append($"> **`Interface Collection:         `** {_collectionManager.Active.Interface.AnonymizedName}\n");
        sb.Append($"> **`Selected Collection:          `** {_collectionManager.Active.Current.AnonymizedName}\n");
        foreach (var (type, name, _) in CollectionTypeExtensions.Special)
        {
            var collection = _collectionManager.Active.ByType(type);
            if (collection != null)
                sb.Append($"> **`{name,-30}`** {collection.AnonymizedName}\n");
        }

        foreach (var (name, id, collection) in _collectionManager.Active.Individuals.Assignments)
            sb.Append($"> **`{id[0].Incognito(name) + ':',-30}`** {collection.AnonymizedName}\n");

        foreach (var collection in _collectionManager.Caches.Active)
            PrintCollection(collection, collection._cache!);

        return sb.ToString();
    }

    // Relevant variables, sorted by priority. LC_CTYPE is the most specific (but not the highest priority), so it will be preferred for advising the user unless LC_ALL is involved.
    private static readonly (string VariableToCheck, string VariableToSet)[] FileSystemSanityCheckVariables =
    {
        ("LC_ALL",   "LC_ALL"),
        ("LC_CTYPE", "LC_CTYPE"),
        ("LANG",     "LC_CTYPE"),
    };

    private static string GatherFileSystemSanityCheckVariables()
    {
        var sb = new StringBuilder();
        foreach (var (variableToCheck, _) in FileSystemSanityCheckVariables)
        {
            var value = Environment.GetEnvironmentVariable(variableToCheck);
            if (!string.IsNullOrEmpty(value))
                sb.Append($"`{variableToCheck}={value}`, ");
        }
        return sb.Length > 0
            ? sb.ToString(0, sb.Length - 2)
            : "Unset";
    }

    public static (bool IsSane, string VariableToSet) IsFileSystemSane()
    {
        // This is a Wine-only issue.
        if (!Dalamud.Utility.Util.IsWine())
            return (true, string.Empty);

        foreach (var (variableToCheck, variableToSet) in FileSystemSanityCheckVariables)
        {
            var value = Environment.GetEnvironmentVariable(variableToCheck);
            if (!string.IsNullOrEmpty(value))
            {
                return value.EndsWith(".UTF-8", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".utf8", StringComparison.OrdinalIgnoreCase)
                    ? (true,  string.Empty)
                    : (false, variableToSet);
            }
        }

        return (false, "LC_CTYPE");
    }
}
