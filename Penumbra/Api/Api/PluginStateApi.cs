using Newtonsoft.Json;
using OtterGui.Services;
using Penumbra.Communication;
using Penumbra.Services;

namespace Penumbra.Api.Api;

public class PluginStateApi(Configuration config, CommunicatorService communicator) : IPenumbraApiPluginState, IApiService
{
    public string GetModDirectory()
        => config.ModDirectory;

    public string GetConfiguration()
        => JsonConvert.SerializeObject(config, Formatting.Indented);

    public event Action<string, bool> ModDirectoryChanged
    {
        add => communicator.ModDirectoryChanged.Subscribe(value, Communication.ModDirectoryChanged.Priority.Api);
        remove => communicator.ModDirectoryChanged.Unsubscribe(value);
    }

    public bool GetEnabledState()
        => config.EnableMods;

    public event Action<bool> EnabledChange
    {
        add => communicator.EnabledChanged.Subscribe(value!, EnabledChanged.Priority.Api);
        remove => communicator.EnabledChanged.Unsubscribe(value!);
    }
}
