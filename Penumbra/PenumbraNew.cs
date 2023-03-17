using System;
using Dalamud.Plugin;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Classes;
using OtterGui.Log;
using Penumbra.Api;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.Interop;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Resolver;
using Penumbra.Interop.Services;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.Util;

namespace Penumbra;

public class PenumbraNew
{
    public string Name
        => "Penumbra";

    public static readonly Logger          Log = new();
    public readonly        ServiceProvider Services;

    public PenumbraNew(DalamudPluginInterface pi)
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
            .AddSingleton<CommunicatorService>();

        // Add Dalamud services
        var dalamud = new DalamudServices(pi);
        dalamud.AddServices(services);

        // Add Game Data
        services.AddSingleton<IGamePathParser, GamePathParser>()
            .AddSingleton<IdentifierService>()
            .AddSingleton<StainService>()
            .AddSingleton<ItemService>()
            .AddSingleton<ActorService>();

        // Add Game Services
        services.AddSingleton<GameEventManager>()
            .AddSingleton<FrameworkManager>()
            .AddSingleton<MetaFileManager>()
            .AddSingleton<CutsceneCharacters>()
            .AddSingleton<CharacterUtility>()
            .AddSingleton<ResourceManagerService>()
            .AddSingleton<ResourceService>()
            .AddSingleton<FileReadService>()
            .AddSingleton<TexMdlService>()
            .AddSingleton<CreateFileWHook>()
            .AddSingleton<ResidentResourceManager>()
            .AddSingleton<FontReloader>();
        
        // Add Configuration
        services.AddTransient<ConfigMigrationService>()
            .AddSingleton<Configuration>();

        // Add Collection Services
        services.AddTransient<IndividualCollections>()
            .AddSingleton<TempCollectionManager>();

        // Add Mod Services
        // TODO
        services.AddSingleton<TempModManager>();

        // Add Interface
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public void Dispose()
    {
        Services.Dispose();
    }
}
