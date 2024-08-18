using Newtonsoft.Json;
using OtterGui.Services;
using Penumbra.Communication;
using Penumbra.Services;

namespace Penumbra.Api.Api;

public class PluginStateApi : IPenumbraApiPluginState, IApiService
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;

    public PluginStateApi(Configuration config, CommunicatorService communicator)
    {
        _config       = config;
        _communicator = communicator;
    }

    public string GetModDirectory()
        => _config.ModDirectory;

    public string GetConfiguration()
        => JsonConvert.SerializeObject(_config, Formatting.Indented);

    public event Action<string, bool>? ModDirectoryChanged
    {
        add => _communicator.ModDirectoryChanged.Subscribe(value!, Communication.ModDirectoryChanged.Priority.Api);
        remove => _communicator.ModDirectoryChanged.Unsubscribe(value!);
    }

    public bool GetEnabledState()
        => _config.EnableMods;

    public event Action<bool>? EnabledChange
    {
        add => _communicator.EnabledChanged.Subscribe(value!, EnabledChanged.Priority.Api);
        remove => _communicator.EnabledChanged.Unsubscribe(value!);
    }
}
