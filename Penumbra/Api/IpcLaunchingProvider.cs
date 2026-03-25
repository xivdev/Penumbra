using Dalamud.Plugin;
using Luna;
using Penumbra.Api.Api;
using Serilog.Events;

namespace Penumbra.Api;

public sealed class IpcLaunchingProvider : IApiService
{
    public IpcLaunchingProvider(IDalamudPluginInterface pi, Logger log)
    {
        try
        {
            using var subscriber = log.MainLogger.IsEnabled(LogEventLevel.Debug)
                ? IpcSubscribers.Launching.Subscriber(pi,
                    (major, minor) => log.Debug($"[IPC] Invoked Penumbra.Launching IPC with API Version {major}.{minor}."))
                : null;

            using var provider = IpcSubscribers.Launching.Provider(pi);
            provider.Invoke(PenumbraApi.BreakingVersion, PenumbraApi.FeatureVersion);
        }
        catch (Exception ex)
        {
            log.Error($"[IPC] Could not invoke Penumbra.Launching IPC:\n{ex}");
        }
    }
}
