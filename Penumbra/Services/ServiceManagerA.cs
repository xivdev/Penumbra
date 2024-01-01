using Dalamud.Plugin;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Classes;
using OtterGui.Compression;
using OtterGui.Log;
using OtterGui.Services;
using Penumbra.Api;
using Penumbra.Collections.Cache;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.Import.Models;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Structs;
using Penumbra.Import.Textures;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.ResourceTree;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.UI;
using Penumbra.UI.AdvancedWindow;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ResourceWatcher;
using Penumbra.UI.Tabs;
using Penumbra.UI.Tabs.Debug;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;

namespace Penumbra.Services;

public static class ServiceManagerA
{
    public static ServiceManager CreateProvider(Penumbra penumbra, DalamudPluginInterface pi, Logger log)
    {
        var services = new ServiceManager(log)
            .AddExistingService(log)
            .AddExistingService(penumbra)
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
        services.AddIServices(typeof(EquipItem).Assembly);
        services.AddIServices(typeof(Penumbra).Assembly);
        DalamudServices.AddServices(services, pi);
        services.CreateProvider();
        return services;
    }

    private static ServiceManager AddMeta(this ServiceManager services)
        => services.AddSingleton<ValidityChecker>()
            .AddSingleton<PerformanceTracker>()
            .AddSingleton<FilenameService>()
            .AddSingleton<BackupService>()
            .AddSingleton<CommunicatorService>()
            .AddSingleton<MessageService>()
            .AddSingleton<SaveService>()
            .AddSingleton<FileCompactor>()
            .AddSingleton<DalamudConfigService>();


    private static ServiceManager AddGameData(this ServiceManager services)
        => services.AddSingleton<GamePathParser>()
            .AddSingleton<StainService>()
            .AddSingleton<HumanModelList>();

    private static ServiceManager AddInterop(this ServiceManager services)
        => services.AddSingleton<GameEventManager>()
            .AddSingleton<FrameworkManager>()
            .AddSingleton<CutsceneService>()
            .AddSingleton(p =>
            {
                var cutsceneService = p.GetRequiredService<CutsceneService>();
                return new CutsceneResolver(cutsceneService.GetParentIndex);
            })
            .AddSingleton<CharacterUtility>()
            .AddSingleton<ResourceManagerService>()
            .AddSingleton<ResourceService>()
            .AddSingleton<FileReadService>()
            .AddSingleton<TexMdlService>()
            .AddSingleton<CreateFileWHook>()
            .AddSingleton<ResidentResourceManager>()
            .AddSingleton<FontReloader>()
            .AddSingleton<RedrawService>()
            .AddSingleton<ModelResourceHandleUtility>();

    private static ServiceManager AddConfiguration(this ServiceManager services)
        => services.AddSingleton<ConfigMigrationService>()
            .AddSingleton<Configuration>()
            .AddSingleton<EphemeralConfig>();

    private static ServiceManager AddCollections(this ServiceManager services)
        => services.AddSingleton<CollectionStorage>()
            .AddSingleton<ActiveCollectionData>()
            .AddSingleton<ActiveCollections>()
            .AddSingleton<InheritanceManager>()
            .AddSingleton<CollectionCacheManager>()
            .AddSingleton<TempCollectionManager>()
            .AddSingleton<CollectionEditor>()
            .AddSingleton<CollectionManager>();

    private static ServiceManager AddMods(this ServiceManager services)
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

    private static ServiceManager AddResources(this ServiceManager services)
        => services.AddSingleton<ResourceLoader>()
            .AddSingleton<ResourceWatcher>()
            .AddSingleton<ResourceTreeFactory>()
            .AddSingleton<MetaFileManager>()
            .AddSingleton<SkinFixer>();

    private static ServiceManager AddResolvers(this ServiceManager services)
        => services.AddSingleton<AnimationHookService>()
            .AddSingleton<CollectionResolver>()
            .AddSingleton<CutsceneService>()
            .AddSingleton<DrawObjectState>()
            .AddSingleton<MetaState>()
            .AddSingleton<PathState>()
            .AddSingleton<SubfileHelper>()
            .AddSingleton<IdentifiedCollectionCache>()
            .AddSingleton<PathResolver>();

    private static ServiceManager AddInterface(this ServiceManager services)
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
            .AddSingleton<MultiModPanel>()
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
            .AddSingleton<MessagesTab>()
            .AddSingleton<ResourceTab>()
            .AddSingleton<ConfigTabBar>()
            .AddSingleton<ResourceWatcher>()
            .AddSingleton<ItemSwapTab>()
            .AddSingleton<ModMergeTab>()
            .AddSingleton<ChangedItemDrawer>()
            .AddSingleton(p => new Diagnostics(p));

    private static ServiceManager AddModEditor(this ServiceManager services)
        => services.AddSingleton<ModFileCollection>()
            .AddSingleton<DuplicateManager>()
            .AddSingleton<MdlMaterialEditor>()
            .AddSingleton<ModFileEditor>()
            .AddSingleton<ModMetaEditor>()
            .AddSingleton<ModSwapEditor>()
            .AddSingleton<ModNormalizer>()
            .AddSingleton<ModMerger>()
            .AddSingleton<ModEditor>()
            .AddSingleton<TextureManager>()
            .AddSingleton<ModelManager>();

    private static ServiceManager AddApi(this ServiceManager services)
        => services.AddSingleton<PenumbraApi>()
            .AddSingleton<IPenumbraApi>(x => x.GetRequiredService<PenumbraApi>())
            .AddSingleton<PenumbraIpcProviders>()
            .AddSingleton<HttpApi>()
            .AddSingleton<IpcTester>()
            .AddSingleton<DalamudSubstitutionProvider>();
}
