using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Api;
using Penumbra.Collections.Manager;
using Penumbra.Interop;
using Penumbra.Interop.Hooks;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.Services;

public sealed class PenumbraSupportInfo(IPluginLoader mainLoader)
    : SupportInfoProvider(mainLoader), IService
{
    /// <summary> The longest support button text. </summary>
    public static ReadOnlySpan<byte> SupportInfoButtonText
        => "Copy Support Info to Clipboard"u8;

    /// <summary> Draw a button that copies the support info to clipboards. </summary>
    public void DrawSupportButton()
    {
        if (!Im.Button(SupportInfoButtonText))
            return;

        var text = GatherSupportInformation(IpcProviders.Callers);
        Im.Clipboard.Set(text);
        Penumbra.Messager.NotificationMessage("Copied Support Info to Clipboard.", NotificationType.Success, false);
    }

    protected override void PrepareData(IReadOnlySet<CallerPlugin>? callers)
    {
        base.PrepareData(callers);
        if (!PluginLoader.HasPlugin)
            return;

        AddSettings();
        AddMods();
        AddCollections();
    }

    private void AddSettings()
    {
        var config      = PluginLoader.Services.GetService<Configuration>();
        var exists      = config.Main.ModDirectory.Length > 0 && Directory.Exists(config.Main.ModDirectory);
        var cloudSynced = exists && CloudApi.IsCloudSynced(config.Main.ModDirectory);
        var drive       = exists ? new DriveInfo(new DirectoryInfo(config.Main.ModDirectory).Root.FullName) : null;
        var gameData    = PluginLoader.Services.GetService<IDataManager>();
        var hdrEnabler  = PluginLoader.Services.GetService<RenderTargetHdrEnabler>();
        var dalamudWaiting =
            PluginLoader.Services.GetService<DalamudConfigService>().GetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, out bool v)
                ? v.ToString()
                : "Unknown";

        StartSection("Settings", 1000);
        AddProperty("Enable Mods",     config.Main.EnableMods);
        AddProperty("Enable HTTP API", config.Advanced.EnableHttpApi);
        AddProperty("Root Directory",
            $"`{config.Main.ModDirectory}`, {(exists ? "Exists" : "Not Existing")}{(cloudSynced ? ", Cloud-Synced" : "")}");
        AddProperty("Free Drive Space",      drive is not null ? FormattingFunctions.HumanReadableSize(drive.AvailableFreeSpace) : "Unknown");
        AddProperty("Game Data Files",       gameData.HasModifiedGameDataFiles ? "Modified" : "Pristine");
        AddProperty("Auto-Deduplication",    config.Advanced.AutoDeduplicateOnImport);
        AddProperty("Auto-UI-Reduplication", config.Advanced.AutoReduplicateUiOnImport);
        AddProperty("Debug Mode",            config.Advanced.DebugMode);
        AddProperty("Penumbra Reloads",      hdrEnabler.PenumbraReloadCount);
        AddProperty("HDR Enabled (from Start)",
            $"{config.Advanced.HdrRenderTargets} ({hdrEnabler is { FirstLaunchHdrState: true, FirstLaunchHdrHookOverrideState: true }}){(hdrEnabler.HdrEnabledSuccess ? ", Detour Called" : ", **NEVER CALLED**")}");
        AddProperty("Custom Shapes Enabled", config.Advanced.EnableCustomShapes);
        AddProperty("Hook Overrides",        HookOverrides.Instance.IsCustomLoaded);
        AddProperty("Synchronous Load (Dalamud)",
            $"{dalamudWaiting} (first Start: {hdrEnabler.FirstLaunchWaitForPluginsState?.ToString() ?? "Unknown"}");
        AddProperty("Logging",
            $"Log: {config.Filters.ResourceLoggerWriteToLog}, Watcher: {config.Filters.ResourceLoggerEnabled} ({config.Filters.ResourceLoggerMaxEntries})");
        AddProperty("Use Ownership", $"{config.Behavior.UseOwnerNameForCharacterCollection} (Hostiles: {config.Behavior.UseOwnerForHostiles})");
    }

    private void AddMods()
    {
        var modManager = PluginLoader.Services.GetService<ModManager>();
        var temporaryModManager = PluginLoader.Services.GetService<TempModManager>();
        StartSection("Mods", 50);
        AddProperty("Installed Mods", modManager.Count);
        AddProperty("Mods with Config", modManager.Count(m => m.HasOptions));
        AddProperty("Mods with File Redirections", CountMods(modManager, static m => m.TotalFileCount));
        AddProperty("Mods with File Swaps", CountMods(modManager, static m => m.TotalSwapCount));
        AddProperty("Mods with Meta Manipulations", CountMods(modManager, static m => m.TotalManipulations));
        AddProperty("#Temporary Mods", temporaryModManager.Mods.Sum(kvp => kvp.Value.Count) + temporaryModManager.ModsForAllCollections.Count);
    }

    private void AddCollections()
    {
        var collectionManager          = PluginLoader.Services.GetService<CollectionManager>();
        var temporaryCollectionManager = PluginLoader.Services.GetService<TempCollectionManager>();
        StartSection("Collections", 10);
        AddProperty("#Collections",           collectionManager.Storage.Count - 1);
        AddProperty("#Temporary Collections", temporaryCollectionManager.Count);
        AddProperty("#Active Collections",    collectionManager.Caches.Count);
        AddProperty("Base Collection",        collectionManager.Active.Default.Identity.AnonymizedName);
        AddProperty("Interface Collection",   collectionManager.Active.Interface.Identity.AnonymizedName);
        AddProperty("Selected Collection",    collectionManager.Active.Current.Identity.AnonymizedName);
        foreach (var (type, _, _) in CollectionTypeExtensions.Special)
        {
            if (collectionManager.Active.ByType(type) is { } collection)
                AddProperty(type.ToName(), collection.Identity.AnonymizedName, false);
        }

        foreach (var (name, id, collection) in collectionManager.Active.Individuals.Assignments)
            AddProperty(id[0].Incognito(name), collection.Identity.AnonymizedName, false);

        foreach (var collection in collectionManager.Caches.Active)
        {
            var inheritsFrom = collection.Inheritance.DirectlyInheritsFrom.Count;
            var enabledMods  = collection.ActualSettings.Count(s => s is { Enabled: true });
            var conflicts = collection.AllConflicts.SelectMany(x => x)
                .Sum(x => x is { HasPriority: true, Solved: true } ? x.Conflicts.Count : 0);
            var allConflicts = collection.AllConflicts.SelectMany(x => x).Sum(x => x.HasPriority ? x.Conflicts.Count : 0);
            var data =
                $"Inheritances: `{inheritsFrom,3}`, Enabled Mods: `{enabledMods,4}`, Conflicts: `{conflicts,5}/{allConflicts,5}`";
            AddProperty($"Collection {collection.Identity.AnonymizedName}", data, false);
        }
    }

    private static string CountMods(ModManager mods, Func<Mod, int> selector)
    {
        var count       = 0;
        var objectCount = 0;
        foreach (var mod in mods)
        {
            var localCount = selector(mod);
            if (localCount is 0)
                continue;

            ++count;
            objectCount += localCount;
        }

        return $"{count}, Total {objectCount}";
    }
}
