using System.IO;
using Dalamud.Plugin;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Classes;
using OtterGui.Log;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.Interop;
using Penumbra.Services;
using Penumbra.Util;

namespace Penumbra;

public class PenumbraNew
{
    public string Name
        => "Penumbra";

    public static readonly Logger                          Log        = new();
    public readonly        StartTimeTracker<StartTimeType> StartTimer = new();

    public readonly IServiceCollection Services = new ServiceCollection();


    public PenumbraNew(DalamudPluginInterface pi)
    {
        using var time = StartTimer.Measure(StartTimeType.Total);

        // Add meta services.
        Services.AddSingleton(Log);
        Services.AddSingleton(StartTimer);
        Services.AddSingleton<ValidityChecker>();
        Services.AddSingleton<PerformanceTracker<PerformanceType>>();

        // Add Dalamud services
        var dalamud = new DalamudServices(pi);
        dalamud.AddServices(Services);

        // Add Game Data
        Services.AddSingleton<GameEventManager>();
        Services.AddSingleton<IGamePathParser, GamePathParser>();
        Services.AddSingleton<IObjectIdentifier, ObjectIdentifier>();

        // Add Configuration
        Services.AddSingleton<Configuration>();
    }

    public void Dispose()
    { }
}