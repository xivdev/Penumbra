using System;
using Dalamud.Plugin;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Classes;
using OtterGui.Log;
using Penumbra.Api;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.ResourceTree;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Services;
using Penumbra.UI;
using Penumbra.UI.AdvancedWindow;
using Penumbra.UI.ModsTab;
using Penumbra.UI.Tabs;
using Penumbra.Util;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;
using ModFileSystemSelector = Penumbra.UI.ModsTab.ModFileSystemSelector;
using Penumbra.Mods.Manager;
using Penumbra.Collections.Cache;
using Penumbra.Meta;

namespace Penumbra;

public class PenumbraNew
{
    public string Name
        => "Penumbra";

    public static readonly Logger          Log = new();
    public readonly        ServiceProvider Services;

    public PenumbraNew(Penumbra penumbra, DalamudPluginInterface pi)
    {
        var       startTimer = new StartTracker();
        using var time       = startTimer.Measure(StartTimeType.Total);

        var services = new ServiceCollection();
        // Add meta services.
        services.AddSingleton(Log)
            .AddSingleton(startTimer)
            .AddSingleton<ValidityChecker>()
            .AddSingleton<PerformanceTracker>()
            .AddSingleton<FilenameService>()
            .AddSingleton<BackupService>()
            .AddSingleton<CommunicatorService>()
            .AddSingleton<ChatService>()
            .AddSingleton<SaveService>();

        // Add Dalamud services
        var dalamud = new DalamudServices(pi);
        dalamud.AddServices(services);
        services.AddSingleton(penumbra);

        // Add Game Data
        services.AddSingleton<IGamePathParser, GamePathParser>()
            .AddSingleton<IdentifierService>()
            .AddSingleton<StainService>()
            .AddSingleton<ItemService>()
            .AddSingleton<ActorService>();

        // Add Game Services
        services.AddSingleton<GameEventManager>()
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

        // Add Configuration
        services.AddTransient<ConfigMigrationService>()
            .AddSingleton<Configuration>();

        // Add Collection Services
        services.AddSingleton<CollectionStorage>()
            .AddSingleton<ActiveCollectionData>()
            .AddSingleton<ActiveCollections>()
            .AddSingleton<InheritanceManager>()
            .AddSingleton<CollectionCacheManager>()
            .AddSingleton<TempCollectionManager>()
            .AddSingleton<CollectionEditor>()
            .AddSingleton<CollectionManager>();

        // Add Mod Services
        services.AddSingleton<TempModManager>()
            .AddSingleton<ModDataEditor>()
            .AddSingleton<ModOptionEditor>()
            .AddSingleton<ModCreator>()
            .AddSingleton<ModManager>()
            .AddSingleton<ModExportManager>()
            .AddSingleton<ModImportManager>()
            .AddSingleton<ModFileSystem>()
            .AddSingleton<ModCacheManager>()
            .AddSingleton(s => (ModStorage) s.GetRequiredService<ModManager>());

        // Add Resource services
        services.AddSingleton<ResourceLoader>()
            .AddSingleton<ResourceWatcher>()
            .AddSingleton<ResourceTreeFactory>()
            .AddSingleton<MetaFileManager>();

        // Add Path Resolver
        services.AddSingleton<AnimationHookService>()
            .AddSingleton<CollectionResolver>()
            .AddSingleton<CutsceneService>()
            .AddSingleton<DrawObjectState>()
            .AddSingleton<MetaState>()
            .AddSingleton<PathState>()
            .AddSingleton<SubfileHelper>()
            .AddSingleton<IdentifiedCollectionCache>()
            .AddSingleton<PathResolver>();

        // Add Interface
        services.AddSingleton<FileDialogService>()
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
            .AddSingleton<ItemSwapTab>();

        // Add Mod Editor
        services.AddSingleton<ModFileCollection>()
            .AddSingleton<DuplicateManager>()
            .AddSingleton<MdlMaterialEditor>()
            .AddSingleton<ModFileEditor>()
            .AddSingleton<ModMetaEditor>()
            .AddSingleton<ModSwapEditor>()
            .AddSingleton<ModNormalizer>()
            .AddSingleton<ModEditor>();

        // Add API
        services.AddSingleton<PenumbraApi>()
            .AddSingleton<IPenumbraApi>(x => x.GetRequiredService<PenumbraApi>())
            .AddSingleton<PenumbraIpcProviders>()
            .AddSingleton<HttpApi>();

        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public void Dispose()
    {
        Services.Dispose();
    }
}
