using Dalamud.Plugin;
using Luna;
using Microsoft.Extensions.DependencyInjection;
using Penumbra.Api.Api;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;
using Penumbra.Mods.Manager;
using IPenumbraApi = Penumbra.Api.Api.IPenumbraApi;

namespace Penumbra.Services;

#pragma warning disable SeStringEvaluator

public static class StaticServiceManager
{
    public static ServiceManager CreateProvider(Penumbra penumbra, IDalamudPluginInterface pi, MainLogger log)
    {
        var services = new ServiceManager(log, log.PluginName)
            .AddDalamudServices(pi)
            .AddExistingService(log)
            .AddExistingService(penumbra);
        services.AddIServices(typeof(EquipItem).Assembly);
        services.AddIServices(typeof(Penumbra).Assembly);
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
}
