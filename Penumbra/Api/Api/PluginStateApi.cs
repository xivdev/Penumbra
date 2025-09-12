using System.Collections.Frozen;
using Luna;
using Newtonsoft.Json;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Services;

namespace Penumbra.Api.Api;

public class PluginStateApi : IPenumbraApiPluginState, IApiService, IDisposable
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

    private void OnEnabledChanged(in EnabledChanged.Arguments arguments)
        => EnabledChange?.Invoke(arguments.Enabled);

    private void OnModDirectoryChanged(in ModDirectoryChanged.Arguments arguments)
        => ModDirectoryChanged?.Invoke(arguments.Directory, arguments.Valid);

    public string GetModDirectory()
        => _config.ModDirectory;

    public string GetConfiguration()
        => JsonConvert.SerializeObject(_config, Formatting.Indented);

    public event Action<string, bool>? ModDirectoryChanged;

    public bool GetEnabledState()
        => _config.EnableMods;

    public event Action<bool>? EnabledChange;

    public FrozenSet<string> SupportedFeatures
        => FeatureChecker.SupportedFeatures.ToFrozenSet();

    public string[] CheckSupportedFeatures(IEnumerable<string> requiredFeatures)
        => requiredFeatures.Where(f => !FeatureChecker.Supported(f)).ToArray();

    public void Dispose()
    {
        _communicator.ModDirectoryChanged.Unsubscribe(OnModDirectoryChanged);
        _communicator.EnabledChanged.Unsubscribe(OnEnabledChanged);
    }
}
