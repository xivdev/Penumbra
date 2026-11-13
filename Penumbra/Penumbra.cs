using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImSharp;
using Lumina.Excel.Sheets;
using Luna;
using Microsoft.Extensions.DependencyInjection;
using Penumbra.Api;
using Penumbra.Api.Api;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Hooks;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Services;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI;
using Penumbra.UI.AdvancedWindow;
using Penumbra.UI.ManagementTab;
using MouseButton = Penumbra.Api.Enums.MouseButton;

namespace Penumbra;

public sealed class Penumbra : IPluginDefinition<Penumbra>, IAsyncDisposable
{
    private readonly PluginLoader<Penumbra> _loader;
    public static    MainLogger             Log      { get; private set; } = null!;
    public static    PenumbraMessager       Messager { get; private set; } = null!;
    public static    DynamisIpc             Dynamis  { get; private set; } = null!;

    public Penumbra(PluginLoader<Penumbra> loader)
    {
        _loader = loader;
        _loader.Services.EnterScope();
    }

    public static ServiceManager CreateServiceManager(IDalamudPluginInterface pluginInterface, MainLogger log)
    {
        var services = new ServiceManager(log, log.PluginName)
            .AddDalamudServices(pluginInterface)
            .AddExistingService(log)
            .AddGenericSingleton(typeof(ManagementLog<>));

        services.AddSingleton(MessageService (p) => p.GetRequiredService<PenumbraMessager>());
        services.AddIServices(typeof(EquipItem).Assembly);
        services.AddIServices(typeof(Penumbra).Assembly);
        services.AddIServices(typeof(IService).Assembly);

        services.AddScoped(p => p.GetRequiredService<UiConfig>().ColorCache);
        services.AddScoped(p =>
            {
                var cutsceneService = p.GetRequiredService<CutsceneService>();
                return new CutsceneResolver(cutsceneService.GetParentIndex);
            })
            .AddScoped(p => p.GetRequiredService<MetaFileManager>().ImcChecker)
            .AddScoped(s => (ModStorage)s.GetRequiredService<ModManager>())
            .AddScoped<IPenumbraApi>(x => x.GetRequiredService<PenumbraApi>());
        return services;
    }

    public static Task PreInitializeAsync(PluginLoader<Penumbra> loader, CancellationToken cancel)
    {
        HookOverrides.Instance = HookOverrides.LoadFile(loader.PluginInterface);
        loader.Services.GetService<IpcLaunchingProvider>().Invoke();
        return Task.CompletedTask;
    }

    public static Task InitializeLoggingAsync(PluginLoader<Penumbra> loader, CancellationToken cancel)
    {
        Log      = loader.Log;
        Messager = loader.Services.GetService<PenumbraMessager>();
        Dynamis  = loader.Services.GetService<DynamisIpc>();
        return Task.CompletedTask;
    }

    public static Task<IIntermediaryPlugin<Penumbra>> LaunchIntermediaryAsync(PluginLoader<Penumbra> loader,
        CancellationToken cancel)
        => Task.FromResult<IIntermediaryPlugin<Penumbra>>(new PenumbraIntermediary((PenumbraLoader)loader));

    public static Task<Penumbra> LaunchPluginAsync(PluginLoader<Penumbra> loader, CancellationToken cancel)
        => Task.FromResult(new Penumbra(loader));

    public Task LoadGameDataAsync(PluginLoader<Penumbra> loader, CancellationToken cancel)
        => Task.CompletedTask;

    public Task CreateBackupsAsync(PluginLoader<Penumbra> loader, CancellationToken cancel)
    {
        var backup = loader.Services.GetService<BackupService>();
        return backup.Awaiter;
    }

    public Task LoadAndMigrateConfigurationAsync(PluginLoader<Penumbra> loader, CancellationToken cancel)
    {
        return Task.Run(() =>
        {
            loader.Services.GetService<ConfigMigrationService>().MigrateOldConfigStyle();
            cancel.ThrowIfCancellationRequested();
            loader.Services.GetService<Configuration>();
        }, cancel);
    }

    public Task LoadPluginObjectsAsync(PluginLoader<Penumbra> loader, CancellationToken cancel)
    {
        return Task.Run(() =>
        {
            _ = loader.Services.GetService<TempModManager>();
            cancel.ThrowIfCancellationRequested();
            _ = loader.Services.GetService<ModManager>();
            cancel.ThrowIfCancellationRequested();
            var collectionManager = loader.Services.GetService<CollectionManager>();
            cancel.ThrowIfCancellationRequested();
            _ = loader.Services.GetService<TempCollectionManager>();
            cancel.ThrowIfCancellationRequested();
            collectionManager.Caches.CreateNecessaryCaches();
        }, cancel);
    }

    public Task CreateGameInteropAsync(PluginLoader<Penumbra> loader, CancellationToken cancel)
    {
        return Task.Run(() =>
        {
            _ = loader.Services.GetService<CharacterUtility>();
            cancel.ThrowIfCancellationRequested();
            _ = loader.Services.GetService<ResidentResourceManager>();
            cancel.ThrowIfCancellationRequested();
            _ = loader.Services.GetService<RedrawService>();
            cancel.ThrowIfCancellationRequested();
            _ = loader.Services.GetService<PathResolver>();
            cancel.ThrowIfCancellationRequested();
            loader.Services.EnsureRequiredServices(cancel);
        }, cancel);
    }

    public async Task CreateUiAsync(PluginLoader<Penumbra> loader, CancellationToken cancel)
    {
        _ = loader.Services.GetService<DalamudSubstitutionProvider>();
        cancel.ThrowIfCancellationRequested();
        await Task.WhenAll(loader.Services.GetServicesImplementing<IAwaitedService>().Select(s => s.Awaiter)).ConfigureAwait(false);
        cancel.ThrowIfCancellationRequested();
        await Task.Run(() =>
        {
            _ = loader.Services.GetService<PenumbraWindowSystem>();
            cancel.ThrowIfCancellationRequested();
            loader.Services.GetService<CommandHandler>();
            cancel.ThrowIfCancellationRequested();
            var config = loader.Services.GetService<Configuration>();
            if (config is not { Ui.OpenWindowAtStart: true, Ephemeral.AdvancedEditingOpenForModPaths.Count: > 0 })
                return;

            var mods              = loader.Services.GetService<ModManager>();
            var editWindowFactory = loader.Services.GetService<ModEditWindowFactory>();
            var modFileSystem     = loader.Services.GetService<ModFileSystem>();
            foreach (var identifier in config.Ephemeral.AdvancedEditingOpenForModPaths)
            {
                cancel.ThrowIfCancellationRequested();
                if (identifier is ModEditWindowFactory.UnpinnedWindowLabel
                 && modFileSystem.Selection.Selection?.GetValue<Mod>() is { } selectedMod)
                    editWindowFactory.OpenForMod(selectedMod, true);
                if (mods.TryGetMod(identifier, out var mod))
                    editWindowFactory.OpenForMod(mod, false);
            }
        }, cancel).ConfigureAwait(false);
    }

    public Task CreateApiAsync(PluginLoader<Penumbra> loader, CancellationToken cancel)
    {
        return Task.Run(() =>
        {
            var communicator = loader.Services.GetService<CommunicatorService>();
            loader.Services.GetService<IpcProviders>();
            cancel.ThrowIfCancellationRequested();
            var itemSheet = loader.Services.GetService<IDataManager>().GetExcelSheet<Item>();
            communicator.ChangedItemHover.Subscribe((in args) =>
            {
                if (args.Data is IdentifiedItem { Item.Id.IsItem: true })
                    Im.Text("Left Click to create an item link in chat."u8);
            }, ChangedItemHover.Priority.Link);
            communicator.ChangedItemClick.Subscribe((in args) =>
            {
                if (args is { Button: MouseButton.Left, Data: IdentifiedItem item } && itemSheet.GetRow(item.Item.ItemId.Id) is { } i)
                    Messager.LinkItem(i);
            }, ChangedItemClick.Priority.Link);
        }, cancel);
    }

    public ValueTask DisposeAsync()
        => _loader.Services.ExitScope();
}
