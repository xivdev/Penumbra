using Dalamud.Plugin;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Log;
using OtterGui.Services;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Cache;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;
using Penumbra.Collections.Manager;
using Penumbra.UI.Tabs;
using ChangedItemClick = Penumbra.Communication.ChangedItemClick;
using ChangedItemHover = Penumbra.Communication.ChangedItemHover;
using OtterGui.Tasks;
using Penumbra.UI;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;
using Dalamud.Plugin.Services;
using Penumbra.GameData.Data;
using Penumbra.Interop.Hooks;
using Penumbra.Interop.Hooks.ResourceLoading;

namespace Penumbra;

public class Penumbra : IDalamudPlugin
{
    public string Name
        => "Penumbra";

    public static readonly Logger         Log = new();
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
    private readonly IDataManager            _gameData;
    private          PenumbraWindowSystem?   _windowSystem;
    private          bool                    _disposed;

    private readonly ServiceManager _services;

    public Penumbra(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            HookOverrides.Instance = HookOverrides.LoadFile(pluginInterface);
            _services              = StaticServiceManager.CreateProvider(this, pluginInterface, Log);
            Messager               = _services.GetService<MessageService>();
            _validityChecker       = _services.GetService<ValidityChecker>();
            _services.EnsureRequiredServices();

            var startup = _services.GetService<DalamudConfigService>()
                .GetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, out bool s)
                ? s.ToString()
                : "Unknown";
            Log.Information(
                $"Loading Penumbra Version {_validityChecker.Version}, Commit #{_validityChecker.CommitHash} with Waiting For Plugins: {startup}...");
            _services.GetService<BackupService>(); // Initialize because not required anywhere else.
            _config            = _services.GetService<Configuration>();
            _characterUtility  = _services.GetService<CharacterUtility>();
            _tempMods          = _services.GetService<TempModManager>();
            _residentResources = _services.GetService<ResidentResourceManager>();
            _services.GetService<ResourceManagerService>(); // Initialize because not required anywhere else.
            _modManager          = _services.GetService<ModManager>();
            _collectionManager   = _services.GetService<CollectionManager>();
            _tempCollections     = _services.GetService<TempCollectionManager>();
            _redrawService       = _services.GetService<RedrawService>();
            _communicatorService = _services.GetService<CommunicatorService>();
            _gameData            = _services.GetService<IDataManager>();
            _services.GetService<ResourceService>(); // Initialize because not required anywhere else.
            _services.GetService<ModCacheManager>(); // Initialize because not required anywhere else.
            _collectionManager.Caches.CreateNecessaryCaches();
            _services.GetService<PathResolver>();

            _services.GetService<DalamudSubstitutionProvider>(); // Initialize before Interface.

            foreach (var service in _services.GetServicesImplementing<IAwaitedService>())
                service.Awaiter.Wait();

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
        _services.GetService<IpcProviders>();
        var itemSheet = _services.GetService<IDataManager>().GetExcelSheet<Item>()!;
        _communicatorService.ChangedItemHover.Subscribe(it =>
        {
            if (it is IdentifiedItem { Item.Id.IsItem: true })
                ImGui.TextUnformatted("Left Click to create an item link in chat.");
        }, ChangedItemHover.Priority.Link);

        _communicatorService.ChangedItemClick.Subscribe((button, it) =>
        {
            if (button == MouseButton.Left && it is IdentifiedItem item && itemSheet.GetRow(item.Item.ItemId.Id) is { } i)
                Messager.LinkItem(i);
        }, ChangedItemClick.Priority.Link);
    }

    private void SetupInterface()
    {
        AsyncTask.Run(() =>
            {
                var system = _services.GetService<PenumbraWindowSystem>();
                system.Window.Setup(this, _services.GetService<ConfigTabBar>());
                _services.GetService<CommandHandler>();
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
                _residentResources.Reload();
                _redrawService.RedrawAll(RedrawType.Redraw);
            }
        }
        else
        {
            if (_characterUtility.Ready)
            {
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

    private void GatherRelevantPlugins(StringBuilder sb)
    {
        ReadOnlySpan<string> relevantPlugins =
        [
            "Glamourer", "MareSynchronos", "CustomizePlus", "SimpleHeels", "VfxEditor", "heliosphere-plugin", "Ktisis", "Brio", "DynamicBridge",
            "IllusioVitae", "Aetherment",
        ];
        var plugins = _services.GetService<IDalamudPluginInterface>().InstalledPlugins
            .GroupBy(p => p.InternalName)
            .ToDictionary(g => g.Key, g =>
            {
                var item = g.OrderByDescending(p => p.IsLoaded).ThenByDescending(p => p.Version).First();
                return (item.IsLoaded, item.Version, item.Name);
            });
        foreach (var plugin in relevantPlugins)
        {
            if (plugins.TryGetValue(plugin, out var data))
                sb.Append($"> **`{data.Name + ':',-29}`** {data.Version}{(data.IsLoaded ? string.Empty : " (Disabled)")}\n");
        }
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
        sb.Append($"> **`Operating System:            `** {(Dalamud.Utility.Util.IsWine() ? "Mac/Linux (Wine)" : "Windows")}\n");
        if (Dalamud.Utility.Util.IsWine())
            sb.Append($"> **`Locale Environment Variables:`** {CollectLocaleEnvironmentVariables()}\n");
        sb.Append($"> **`Root Directory:              `** `{_config.ModDirectory}`, {(exists ? "Exists" : "Not Existing")}\n");
        sb.Append(
            $"> **`Free Drive Space:            `** {(drive != null ? Functions.HumanReadableSize(drive.AvailableFreeSpace) : "Unknown")}\n");
        sb.Append($"> **`Game Data Files:             `** {(_gameData.HasModifiedGameDataFiles ? "Modified" : "Pristine")}\n");
        sb.Append($"> **`Auto-Deduplication:          `** {_config.AutoDeduplicateOnImport}\n");
        sb.Append($"> **`Auto-UI-Reduplication:       `** {_config.AutoReduplicateUiOnImport}\n");
        sb.Append($"> **`Debug Mode:                  `** {_config.DebugMode}\n");
        sb.Append($"> **`Hook Overrides:              `** {HookOverrides.Instance.IsCustomLoaded}\n");
        sb.Append(
            $"> **`Synchronous Load (Dalamud):  `** {(_services.GetService<DalamudConfigService>().GetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, out bool v) ? v.ToString() : "Unknown")}\n");
        sb.Append(
            $"> **`Logging:                     `** Log: {_config.Ephemeral.EnableResourceLogging}, Watcher: {_config.Ephemeral.EnableResourceWatcher} ({_config.MaxResourceWatcherRecords})\n");
        sb.Append($"> **`Use Ownership:               `** {_config.UseOwnerNameForCharacterCollection}\n");
        GatherRelevantPlugins(sb);
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
            => sb.Append(
                $"> **`Collection {c.AnonymizedName + ':',-18}`** Inheritances: `{c.DirectlyInheritsFrom.Count,3}`, Enabled Mods: `{c.ActualSettings.Count(s => s is { Enabled: true }),4}`, Conflicts: `{c.AllConflicts.SelectMany(x => x).Sum(x => x is { HasPriority: true, Solved: true } ? x.Conflicts.Count : 0),5}/{c.AllConflicts.SelectMany(x => x).Sum(x => x.HasPriority ? x.Conflicts.Count : 0),5}`\n");

        sb.AppendLine("**Collections**");
        sb.Append($"> **`#Collections:                `** {_collectionManager.Storage.Count - 1}\n");
        sb.Append($"> **`#Temp Collections:           `** {_tempCollections.Count}\n");
        sb.Append($"> **`Active Collections:          `** {_collectionManager.Caches.Count}\n");
        sb.Append($"> **`Base Collection:             `** {_collectionManager.Active.Default.AnonymizedName}\n");
        sb.Append($"> **`Interface Collection:        `** {_collectionManager.Active.Interface.AnonymizedName}\n");
        sb.Append($"> **`Selected Collection:         `** {_collectionManager.Active.Current.AnonymizedName}\n");
        foreach (var (type, name, _) in CollectionTypeExtensions.Special)
        {
            var collection = _collectionManager.Active.ByType(type);
            if (collection != null)
                sb.Append($"> **`{name,-29}`** {collection.AnonymizedName}\n");
        }

        foreach (var (name, id, collection) in _collectionManager.Active.Individuals.Assignments)
            sb.Append($"> **`{id[0].Incognito(name) + ':',-29}`** {collection.AnonymizedName}\n");

        foreach (var collection in _collectionManager.Caches.Active)
            PrintCollection(collection, collection._cache!);

        return sb.ToString();
    }

    private static string CollectLocaleEnvironmentVariables()
    {
        var variableNames = new List<string>();
        var variables     = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            var key = (string)variable.Key;
            if (key.Equals("LANG", StringComparison.Ordinal) || key.StartsWith("LC_", StringComparison.Ordinal))
            {
                variableNames.Add(key);
                variables.Add(key, (string?)variable.Value ?? string.Empty);
            }
        }

        variableNames.Sort();

        var pos = variableNames.IndexOf("LC_ALL");
        if (pos > 0) // If it's == 0, we're going to do a no-op.
        {
            variableNames.RemoveAt(pos);
            variableNames.Insert(0, "LC_ALL");
        }

        pos = variableNames.IndexOf("LANG");
        if (pos >= 0 && pos < variableNames.Count - 1)
        {
            variableNames.RemoveAt(pos);
            variableNames.Add("LANG");
        }

        return variableNames.Count == 0
            ? "None"
            : string.Join(", ", variableNames.Select(name => $"`{name}={variables[name]}`"));
    }
}
