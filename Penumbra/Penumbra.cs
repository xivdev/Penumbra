using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.DependencyInjection;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Log;
using OtterGui.Widgets;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Interop;
using Penumbra.UI;
using Penumbra.Util;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.PathResolving;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;
using DalamudUtil = Dalamud.Utility.Util;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;
using Penumbra.Services;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;

namespace Penumbra;

public class Penumbra : IDalamudPlugin
{
    public string Name
        => "Penumbra";

    public static Logger          Log         { get; private set; } = null!;
    public static ChatService     ChatService { get; private set; } = null!;
    public static FilenameService Filenames   { get; private set; } = null!;
    public static SaveService     SaveService { get; private set; } = null!;
    public static Configuration   Config      { get; private set; } = null!;

    public static ResidentResourceManager ResidentResources { get; private set; } = null!;
    public static CharacterUtility        CharacterUtility  { get; private set; } = null!;
    public static GameEventManager        GameEvents        { get; private set; } = null!;
    public static MetaFileManager         MetaFileManager   { get; private set; } = null!;
    public static ModManager              ModManager        { get; private set; } = null!;
    public static ModCacheManager         ModCaches         { get; private set; } = null!;
    public static CollectionManager       CollectionManager { get; private set; } = null!;
    public static TempCollectionManager   TempCollections   { get; private set; } = null!;
    public static TempModManager          TempMods          { get; private set; } = null!;
    public static ResourceLoader          ResourceLoader    { get; private set; } = null!;
    public static FrameworkManager        Framework         { get; private set; } = null!;
    public static ActorManager            Actors            { get; private set; } = null!;
    public static IObjectIdentifier       Identifier        { get; private set; } = null!;
    public static IGamePathParser         GamePathParser    { get; private set; } = null!;
    public static StainService            StainService      { get; private set; } = null!;

    // TODO
    public static ValidityChecker ValidityChecker { get; private set; } = null!;

    public static PerformanceTracker Performance { get; private set; } = null!;

    public readonly PathResolver          PathResolver;
    public readonly RedrawService         RedrawService;
    public readonly ModFileSystem         ModFileSystem;
    public          HttpApi               HttpApi = null!;
    internal        ConfigWindow?         ConfigWindow { get; private set; }
    private         PenumbraWindowSystem? _windowSystem;
    private         bool                  _disposed;

    private readonly PenumbraNew _tmp;

    public Penumbra(DalamudPluginInterface pluginInterface)
    {
        Log = PenumbraNew.Log;
        try
        {
            _tmp            = new PenumbraNew(this, pluginInterface);
            ChatService     = _tmp.Services.GetRequiredService<ChatService>();
            Filenames       = _tmp.Services.GetRequiredService<FilenameService>();
            SaveService     = _tmp.Services.GetRequiredService<SaveService>();
            Performance     = _tmp.Services.GetRequiredService<PerformanceTracker>();
            ValidityChecker = _tmp.Services.GetRequiredService<ValidityChecker>();
            _tmp.Services.GetRequiredService<BackupService>();
            Config            = _tmp.Services.GetRequiredService<Configuration>();
            CharacterUtility  = _tmp.Services.GetRequiredService<CharacterUtility>();
            GameEvents        = _tmp.Services.GetRequiredService<GameEventManager>();
            MetaFileManager   = _tmp.Services.GetRequiredService<MetaFileManager>();
            Framework         = _tmp.Services.GetRequiredService<FrameworkManager>();
            Actors            = _tmp.Services.GetRequiredService<ActorService>().AwaitedService;
            Identifier        = _tmp.Services.GetRequiredService<IdentifierService>().AwaitedService;
            GamePathParser    = _tmp.Services.GetRequiredService<IGamePathParser>();
            StainService      = _tmp.Services.GetRequiredService<StainService>();
            TempMods          = _tmp.Services.GetRequiredService<TempModManager>();
            ResidentResources = _tmp.Services.GetRequiredService<ResidentResourceManager>();
            _tmp.Services.GetRequiredService<ResourceManagerService>();
            ModManager        = _tmp.Services.GetRequiredService<ModManager>();
            CollectionManager = _tmp.Services.GetRequiredService<CollectionManager>();
            TempCollections   = _tmp.Services.GetRequiredService<TempCollectionManager>();
            ModFileSystem     = _tmp.Services.GetRequiredService<ModFileSystem>();
            RedrawService     = _tmp.Services.GetRequiredService<RedrawService>();
            _tmp.Services.GetRequiredService<ResourceService>();
            ResourceLoader = _tmp.Services.GetRequiredService<ResourceLoader>();
            ModCaches      = _tmp.Services.GetRequiredService<ModCacheManager>();
            using (var t = _tmp.Services.GetRequiredService<StartTracker>().Measure(StartTimeType.PathResolver))
            {
                PathResolver = _tmp.Services.GetRequiredService<PathResolver>();
            }

            SetupInterface();
            SetupApi();

            ValidityChecker.LogExceptions();
            Log.Information(
                $"Penumbra Version {ValidityChecker.Version}, Commit #{ValidityChecker.CommitHash} successfully Loaded from {pluginInterface.SourceRepository}.");
            OtterTex.NativeDll.Initialize(pluginInterface.AssemblyLocation.DirectoryName);
            Log.Information($"Loading native OtterTex assembly from {OtterTex.NativeDll.Directory}.");

            if (CharacterUtility.Ready)
                ResidentResources.Reload();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void SetupApi()
    {
        using var timer = _tmp.Services.GetRequiredService<StartTracker>().Measure(StartTimeType.Api);
        var       api   = _tmp.Services.GetRequiredService<IPenumbraApi>();
        HttpApi = _tmp.Services.GetRequiredService<HttpApi>();
        _tmp.Services.GetRequiredService<PenumbraIpcProviders>();
        if (Config.EnableHttpApi)
            HttpApi.CreateWebServer();
        api.ChangedItemTooltip += it =>
        {
            if (it is Item)
                ImGui.TextUnformatted("Left Click to create an item link in chat.");
        };
        api.ChangedItemClicked += (button, it) =>
        {
            if (button == MouseButton.Left && it is Item item)
                ChatService.LinkItem(item);
        };
    }

    private void SetupInterface()
    {
        Task.Run(() =>
            {
                using var tInterface = _tmp.Services.GetRequiredService<StartTracker>().Measure(StartTimeType.Interface);
                var       system     = _tmp.Services.GetRequiredService<PenumbraWindowSystem>();
                _tmp.Services.GetRequiredService<CommandHandler>();
                if (!_disposed)
                {
                    _windowSystem = system;
                    ConfigWindow  = system.Window;
                }
                else
                {
                    system.Dispose();
                }
            }
        );
    }

    public event Action<bool>? EnabledChange;

    public bool SetEnabled(bool enabled)
    {
        if (enabled == Config.EnableMods)
            return false;

        Config.EnableMods = enabled;
        if (enabled)
        {
            if (CharacterUtility.Ready)
            {
                CollectionManager.Default.SetFiles();
                ResidentResources.Reload();
                RedrawService.RedrawAll(RedrawType.Redraw);
            }
        }
        else
        {
            if (CharacterUtility.Ready)
            {
                CharacterUtility.ResetAll();
                ResidentResources.Reload();
                RedrawService.RedrawAll(RedrawType.Redraw);
            }
        }

        Config.Save();
        EnabledChange?.Invoke(enabled);

        return true;
    }

    public void ForceChangelogOpen()
        => _windowSystem?.ForceChangelogOpen();

    public void Dispose()
    {
        if (_disposed)
            return;

        _tmp?.Dispose();
        _disposed = true;
    }

    public string GatherSupportInformation()
    {
        var sb     = new StringBuilder(10240);
        var exists = Config.ModDirectory.Length > 0 && Directory.Exists(Config.ModDirectory);
        var drive  = exists ? new DriveInfo(new DirectoryInfo(Config.ModDirectory).Root.FullName) : null;
        sb.AppendLine("**Settings**");
        sb.Append($"> **`Plugin Version:              `** {ValidityChecker.Version}\n");
        sb.Append($"> **`Commit Hash:                 `** {ValidityChecker.CommitHash}\n");
        sb.Append($"> **`Enable Mods:                 `** {Config.EnableMods}\n");
        sb.Append($"> **`Enable HTTP API:             `** {Config.EnableHttpApi}\n");
        sb.Append($"> **`Operating System:            `** {(DalamudUtil.IsLinux() ? "Mac/Linux (Wine)" : "Windows")}\n");
        sb.Append($"> **`Root Directory:              `** `{Config.ModDirectory}`, {(exists ? "Exists" : "Not Existing")}\n");
        sb.Append(
            $"> **`Free Drive Space:            `** {(drive != null ? Functions.HumanReadableSize(drive.AvailableFreeSpace) : "Unknown")}\n");
        sb.Append($"> **`Auto-Deduplication:          `** {Config.AutoDeduplicateOnImport}\n");
        sb.Append($"> **`Debug Mode:                  `** {Config.DebugMode}\n");
        sb.Append(
            $"> **`Synchronous Load (Dalamud):  `** {(_tmp.Services.GetRequiredService<DalamudServices>().GetDalamudConfig(DalamudServices.WaitingForPluginsOption, out bool v) ? v.ToString() : "Unknown")}\n");
        sb.Append(
            $"> **`Logging:                     `** Log: {Config.EnableResourceLogging}, Watcher: {Config.EnableResourceWatcher} ({Config.MaxResourceWatcherRecords})\n");
        sb.Append($"> **`Use Ownership:               `** {Config.UseOwnerNameForCharacterCollection}\n");
        sb.AppendLine("**Mods**");
        sb.Append($"> **`Installed Mods:              `** {ModManager.Count}\n");
        sb.Append($"> **`Mods with Config:            `** {ModCaches.Count(m => m.HasOptions)}\n");
        sb.Append(
            $"> **`Mods with File Redirections: `** {ModCaches.Count(m => m.TotalFileCount > 0)}, Total: {ModCaches.Sum(m => m.TotalFileCount)}\n");
        sb.Append(
            $"> **`Mods with FileSwaps:         `** {ModCaches.Count(m => m.TotalSwapCount > 0)}, Total: {ModCaches.Sum(m => m.TotalSwapCount)}\n");
        sb.Append(
            $"> **`Mods with Meta Manipulations:`** {ModCaches.Count(m => m.TotalManipulations > 0)}, Total {ModCaches.Sum(m => m.TotalManipulations)}\n");
        sb.Append($"> **`IMC Exceptions Thrown:       `** {ValidityChecker.ImcExceptions.Count}\n");
        sb.Append(
            $"> **`#Temp Mods:                  `** {TempMods.Mods.Sum(kvp => kvp.Value.Count) + TempMods.ModsForAllCollections.Count}\n");

        string CharacterName(ActorIdentifier id, string name)
        {
            if (id.Type is IdentifierType.Player or IdentifierType.Owned)
            {
                var parts = name.Split(' ', 3);
                return string.Join(" ",
                    parts.Length != 3 ? parts.Select(n => $"{n[0]}.") : parts[..2].Select(n => $"{n[0]}.").Append(parts[2]));
            }

            return name + ':';
        }

        void PrintCollection(ModCollection c)
            => sb.Append($"**Collection {c.AnonymizedName}**\n"
              + $"> **`Inheritances:                 `** {c.Inheritance.Count}\n"
              + $"> **`Enabled Mods:                 `** {c.ActualSettings.Count(s => s is { Enabled: true })}\n"
              + $"> **`Conflicts (Solved/Total):     `** {c.AllConflicts.SelectMany(x => x).Sum(x => x.HasPriority && x.Solved ? x.Conflicts.Count : 0)}/{c.AllConflicts.SelectMany(x => x).Sum(x => x.HasPriority ? x.Conflicts.Count : 0)}\n");

        sb.AppendLine("**Collections**");
        sb.Append($"> **`#Collections:                 `** {CollectionManager.Count - 1}\n");
        sb.Append($"> **`#Temp Collections:            `** {TempCollections.Count}\n");
        sb.Append($"> **`Active Collections:           `** {CollectionManager.Count(c => c.HasCache)}\n");
        sb.Append($"> **`Base Collection:              `** {CollectionManager.Default.AnonymizedName}\n");
        sb.Append($"> **`Interface Collection:         `** {CollectionManager.Interface.AnonymizedName}\n");
        sb.Append($"> **`Selected Collection:          `** {CollectionManager.Current.AnonymizedName}\n");
        foreach (var (type, name, _) in CollectionTypeExtensions.Special)
        {
            var collection = CollectionManager.ByType(type);
            if (collection != null)
                sb.Append($"> **`{name,-30}`** {collection.AnonymizedName}\n");
        }

        foreach (var (name, id, collection) in CollectionManager.Individuals.Assignments)
            sb.Append($"> **`{CharacterName(id[0], name),-30}`** {collection.AnonymizedName}\n");

        foreach (var collection in CollectionManager.Where(c => c.HasCache))
            PrintCollection(collection);

        return sb.ToString();
    }
}
