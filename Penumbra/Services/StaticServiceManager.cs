using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.DragDrop;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Luna;
using Microsoft.Extensions.DependencyInjection;
using OtterGui;
using Penumbra.Api.Api;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;
using Penumbra.Mods.Manager;
using IPenumbraApi = Penumbra.Api.Api.IPenumbraApi;
using Logger = OtterGui.Log.Logger;

namespace Penumbra.Services;

#pragma warning disable SeStringEvaluator

public static class StaticServiceManager
{
    public static ServiceManager CreateProvider(Penumbra penumbra, IDalamudPluginInterface pi, Logger log)
    {
        var services = new ServiceManager(new Luna.Logger())
            .AddDalamudServices(pi)
            .AddExistingService(log)
            .AddExistingService(penumbra);
        services.AddIServices(typeof(EquipItem).Assembly);
        services.AddIServices(typeof(Penumbra).Assembly);
        services.AddIServices<OtterGui.Services.IService>(typeof(Penumbra).Assembly);
        services.AddIServices<OtterGui.Services.IService>(typeof(ImGuiUtil).Assembly);
        services.AddIServices(typeof(IService).Assembly);

        services.AddSingleton(p =>
            {
                var cutsceneService = p.GetRequiredService<CutsceneService>();
                return new CutsceneResolver(cutsceneService.GetParentIndex);
            })
            .AddSingleton(p => p.GetRequiredService<MetaFileManager>().ImcChecker)
            .AddSingleton(s => (ModStorage)s.GetRequiredService<ModManager>())
            .AddSingleton<IPenumbraApi>(x => x.GetRequiredService<PenumbraApi>());
        services.BuildProvider();
        return services;
    }

    private static ServiceManager AddDalamudServices(this ServiceManager services, IDalamudPluginInterface pi)
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
            .AddDalamudService<INotificationManager>(pi)
            .AddDalamudService<ISeStringEvaluator>(pi);
}
