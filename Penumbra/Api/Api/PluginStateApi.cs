using Newtonsoft.Json;
using OtterGui.Services;
using Penumbra.Communication;
using Penumbra.Services;

namespace Penumbra.Api.Api;

public sealed class PluginStateApi : IPenumbraApiPluginState, IApiService, IDisposable
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;

    public PluginStateApi(Configuration config, CommunicatorService communicator)
    {
        _config       = config;
        _communicator = communicator;
        _communicator.ModDirectoryChanged.Subscribe(OnModDirectoryChanged, Communication.ModDirectoryChanged.Priority.Api);
        _communicator.EnabledChanged.Subscribe(OnEnabledChanged, EnabledChanged.Priority.Api);
    }

    public void Dispose()
    {
        _communicator.ModDirectoryChanged.Unsubscribe(OnModDirectoryChanged);
        _communicator.EnabledChanged.Unsubscribe(OnEnabledChanged);
    }

    public string GetModDirectory()
        => _config.ModDirectory;

    public string GetConfiguration()
        => JsonConvert.SerializeObject(_config, Formatting.Indented);

    public event Action<string, bool>? ModDirectoryChanged;

    public bool GetEnabledState()
        => _config.EnableMods;

    public event Action<bool>? EnabledChange;

    private void OnModDirectoryChanged(string modDirectory, bool valid)
        => ModDirectoryChanged?.Invoke(modDirectory, valid);

    private void OnEnabledChanged(bool value)
        => EnabledChange?.Invoke(value);
}
