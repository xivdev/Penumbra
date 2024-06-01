using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.DragDrop;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Log;
using OtterGui.Services;
using Penumbra.Api;
using Penumbra.Api.Api;
using Penumbra.Collections.Cache;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.Import.Models;
using Penumbra.GameData.Structs;
using Penumbra.Import.Textures;
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
using Penumbra.UI.ResourceWatcher;
using Penumbra.UI.Tabs;
using Penumbra.UI.Tabs.Debug;
using IPenumbraApi = Penumbra.Api.Api.IPenumbraApi;
using MdlMaterialEditor = Penumbra.Mods.Editor.MdlMaterialEditor;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;
using Penumbra.Api.IpcTester;

namespace Penumbra.Services;

public static class StaticServiceManager
{
    public static ServiceManager CreateProvider(Penumbra penumbra, DalamudPluginInterface pi, Logger log)
    {
        var services = new ServiceManager(log)
            .AddDalamudServices(pi)
            .AddExistingService(log)
            .AddExistingService(penumbra)
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
        services.AddIServices(typeof(ImGuiUtil).Assembly);
        services.CreateProvider();
        return services;
    }

    private static ServiceManager AddDalamudServices(this ServiceManager services, DalamudPluginInterface pi)
        => services.AddExistingService(pi)
            .AddExistingService(pi.UiBuilder)
            .AddDalamudService<ICommandManager>(pi)
            .AddDalamudService<IDataManager>(pi)
            .AddDalamudService<IClientState>(pi)
            .AddDalamudService<IChatGui>(pi)
            .AddDalamudService<IFramework>(pi)
            .AddDalamudService<ICondition>(pi)
            .AddDalamudService<ITargetManager>(pi)
            .AddDalamudService<IObjectTable>(pi)
            .AddDalamudService<ITitleScreenMenu>(pi)
            .AddDalamudService<IGameGui>(pi)
            .AddDalamudService<IKeyState>(pi)
            .AddDalamudService<ISigScanner>(pi)
            .AddDalamudService<IDragDropManager>(pi)
            .AddDalamudService<ITextureProvider>(pi)
            .AddDalamudService<ITextureSubstitutionProvider>(pi)
            .AddDalamudService<IGameInteropProvider>(pi)
            .AddDalamudService<IPluginLog>(pi)
            .AddDalamudService<INotificationManager>(pi);

    private static ServiceManager AddInterop(this ServiceManager services)
        => services.AddSingleton<FrameworkManager>()
            .AddSingleton<CutsceneService>()
            .AddSingleton(p =>
            {
                var cutsceneService = p.GetRequiredService<CutsceneService>();
                return new CutsceneResolver(cutsceneService.GetParentIndex);
            })
            .AddSingleton<CharacterUtility>()
            .AddSingleton<ModelRenderer>()
            .AddSingleton<ResourceManagerService>()
            .AddSingleton<ResourceService>()
            .AddSingleton<FileReadService>()
            .AddSingleton<TexMdlService>()
            .AddSingleton<CreateFileWHook>()
            .AddSingleton<ResidentResourceManager>()
            .AddSingleton<FontReloader>()
            .AddSingleton<RedrawService>()
            .AddSingleton(p => p.GetRequiredService<MetaFileManager>().ImcChecker);

    private static ServiceManager AddConfiguration(this ServiceManager services)
        => services.AddSingleton<Configuration>()
            .AddSingleton<EphemeralConfig>()
            .AddSingleton<PredefinedTagManager>();

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
            .AddSingleton<MetaFileManager>();

    private static ServiceManager AddResolvers(this ServiceManager services)
        => services.AddSingleton<CollectionResolver>()
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
        => services.AddSingleton<IPenumbraApi>(x => x.GetRequiredService<PenumbraApi>());
}
