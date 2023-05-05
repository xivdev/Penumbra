using System.Collections.Concurrent;
using Dalamud.Plugin;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Classes;
using OtterGui.Log;
using Penumbra.Api;
using Penumbra.Collections.Cache;
using Penumbra.Collections.Manager;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.ResourceTree;
using Penumbra.Interop.Services;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.UI;
using Penumbra.UI.AdvancedWindow;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;
using Penumbra.UI.Tabs;

namespace Penumbra.Services;

public static class ServiceManager
{
    public static ServiceProvider CreateProvider(Penumbra penumbra, DalamudPluginInterface pi, Logger log, StartTracker startTimer)
    {
        var services = new ServiceCollection()
            .AddSingleton(log)
            .AddSingleton(startTimer)
            .AddSingleton(penumbra)
            .AddDalamud(pi)
            .AddMeta()
            .AddGameData()
            .AddInterop()
            .AddConfiguration()
            .AddCollections()
            .AddMods()
            .AddResources()
            .AddResolvers()
            .AddInterface()
            .AddModEditor()
            .AddApi();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private static IServiceCollection AddDalamud(this IServiceCollection services, DalamudPluginInterface pi)
    {
        var dalamud = new DalamudServices(pi);
        dalamud.AddServices(services);
        return services;
    }

    private static IServiceCollection AddMeta(this IServiceCollection services)
        => services.AddSingleton<ValidityChecker>()
            .AddSingleton<PerformanceTracker>()
            .AddSingleton<FilenameService>()
            .AddSingleton<BackupService>()
            .AddSingleton<CommunicatorService>()
            .AddSingleton<ChatService>()
            .AddSingleton<SaveService>();


    private static IServiceCollection AddGameData(this IServiceCollection services)
        => services.AddSingleton<IGamePathParser, GamePathParser>()
            .AddSingleton<IdentifierService>()
            .AddSingleton<StainService>()
            .AddSingleton<ItemService>()
            .AddSingleton<ActorService>();

    private static IServiceCollection AddInterop(this IServiceCollection services)
        => services.AddSingleton<GameEventManager>()
            .AddSingleton<FrameworkManager>()
            .AddSingleton<CutsceneService>()
            .AddSingleton<CharacterUtility>()
            .AddSingleton<ResourceManagerService>()
            .AddSingleton<ResourceService>()
            .AddSingleton<FileReadService>()
            .AddSingleton<TexMdlService>()
            .AddSingleton<CreateFileWHook>()
            .AddSingleton<ResidentResourceManager>()
            .AddSingleton<FontReloader>()
            .AddSingleton<RedrawService>();

    private static IServiceCollection AddConfiguration(this IServiceCollection services)
        => services.AddTransient<ConfigMigrationService>()
            .AddSingleton<Configuration>();

    private static IServiceCollection AddCollections(this IServiceCollection services)
        => services.AddSingleton<CollectionStorage>()
            .AddSingleton<ActiveCollectionData>()
            .AddSingleton<ActiveCollections>()
            .AddSingleton<InheritanceManager>()
            .AddSingleton<CollectionCacheManager>()
            .AddSingleton<TempCollectionManager>()
            .AddSingleton<CollectionEditor>()
            .AddSingleton<CollectionManager>();

    private static IServiceCollection AddMods(this IServiceCollection services)
        => services.AddSingleton<TempModManager>()
            .AddSingleton<ModDataEditor>()
            .AddSingleton<ModOptionEditor>()
            .AddSingleton<ModCreator>()
            .AddSingleton<ModManager>()
            .AddSingleton<ModExportManager>()
            .AddSingleton<ModImportManager>()
            .AddSingleton<ModFileSystem>()
            .AddSingleton<ModCacheManager>()
            .AddSingleton(s => (ModStorage)s.GetRequiredService<ModManager>());

    private static IServiceCollection AddResources(this IServiceCollection services)
        => services.AddSingleton<ResourceLoader>()
            .AddSingleton<ResourceWatcher>()
            .AddSingleton<ResourceTreeFactory>()
            .AddSingleton<MetaFileManager>();

    private static IServiceCollection AddResolvers(this IServiceCollection services)
        => services.AddSingleton<AnimationHookService>()
            .AddSingleton<CollectionResolver>()
            .AddSingleton<CutsceneService>()
            .AddSingleton<DrawObjectState>()
            .AddSingleton<MetaState>()
            .AddSingleton<PathState>()
            .AddSingleton<SubfileHelper>()
            .AddSingleton<IdentifiedCollectionCache>()
            .AddSingleton<PathResolver>();

    private static IServiceCollection AddInterface(this IServiceCollection services)
        => services.AddSingleton<FileDialogService>()
            .AddSingleton<TutorialService>()
            .AddSingleton<PenumbraChangelog>()
            .AddSingleton<LaunchButton>()
            .AddSingleton<ConfigWindow>()
            .AddSingleton<PenumbraWindowSystem>()
            .AddSingleton<ModEditWindow>()
            .AddSingleton<CommandHandler>()
            .AddSingleton<SettingsTab>()
            .AddSingleton<ModsTab>()
            .AddSingleton<ModPanel>()
            .AddSingleton<ModFileSystemSelector>()
            .AddSingleton<CollectionSelectHeader>()
            .AddSingleton<ImportPopup>()
            .AddSingleton<ModPanelDescriptionTab>()
            .AddSingleton<ModPanelSettingsTab>()
            .AddSingleton<ModPanelEditTab>()
            .AddSingleton<ModPanelChangedItemsTab>()
            .AddSingleton<ModPanelConflictsTab>()
            .AddSingleton<ModPanelCollectionsTab>()
            .AddSingleton<ModPanelTabBar>()
            .AddSingleton<ModFileSystemSelector>()
            .AddSingleton<CollectionsTab>()
            .AddSingleton<ChangedItemsTab>()
            .AddSingleton<EffectiveTab>()
            .AddSingleton<OnScreenTab>()
            .AddSingleton<DebugTab>()
            .AddSingleton<ResourceTab>()
            .AddSingleton<ConfigTabBar>()
            .AddSingleton<ResourceWatcher>()
            .AddSingleton<ItemSwapTab>()
            .AddSingleton<ModMergeTab>();

    private static IServiceCollection AddModEditor(this IServiceCollection services)
        => services.AddSingleton<ModFileCollection>()
            .AddSingleton<DuplicateManager>()
            .AddSingleton<MdlMaterialEditor>()
            .AddSingleton<ModFileEditor>()
            .AddSingleton<ModMetaEditor>()
            .AddSingleton<ModSwapEditor>()
            .AddSingleton<ModNormalizer>()
            .AddSingleton<ModMerger>()
            .AddSingleton<ModEditor>();

    private static IServiceCollection AddApi(this IServiceCollection services)
        => services.AddSingleton<PenumbraApi>()
            .AddSingleton<IPenumbraApi>(x => x.GetRequiredService<PenumbraApi>())
            .AddSingleton<PenumbraIpcProviders>()
            .AddSingleton<HttpApi>();
}
