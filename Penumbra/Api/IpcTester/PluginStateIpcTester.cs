using Dalamud.Plugin;
using ImSharp;
using Luna;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class PluginStateIpcTester : IUiService, IDisposable
{
    private readonly IDalamudPluginInterface       _pi;
    public readonly  EventSubscriber<string, bool> ModDirectoryChanged;
    public readonly  EventSubscriber               Initialized;
    public readonly  EventSubscriber               Disposed;
    public readonly  EventSubscriber<bool>         EnabledChange;

    private string         _currentConfiguration = string.Empty;
    private string         _lastModDirectory     = string.Empty;
    private bool           _lastModDirectoryValid;
    private DateTimeOffset _lastModDirectoryTime = DateTimeOffset.MinValue;

    private readonly List<DateTimeOffset> _initializedList = [];
    private readonly List<DateTimeOffset> _disposedList    = [];

    private string   _requiredFeatureString = string.Empty;
    private string[] _requiredFeatures      = [];

    private DateTimeOffset _lastEnabledChange = DateTimeOffset.UnixEpoch;
    private bool?          _lastEnabledValue;

    public PluginStateIpcTester(IDalamudPluginInterface pi)
    {
        _pi                 = pi;
        ModDirectoryChanged = IpcSubscribers.ModDirectoryChanged.Subscriber(pi, UpdateModDirectoryChanged);
        Initialized         = IpcSubscribers.Initialized.Subscriber(pi, AddInitialized);
        Disposed            = IpcSubscribers.Disposed.Subscriber(pi, AddDisposed);
        EnabledChange       = IpcSubscribers.EnabledChange.Subscriber(pi, SetLastEnabled);
        ModDirectoryChanged.Disable();
        EnabledChange.Disable();
    }

    public void Dispose()
    {
        ModDirectoryChanged.Dispose();
        Initialized.Dispose();
        Disposed.Dispose();
        EnabledChange.Dispose();
    }


    public void Draw()
    {
        using var tree = Im.Tree.Node("Plugin State"u8);
        if (!tree)
            return;

        if (Im.Input.Text("Required Features"u8, ref _requiredFeatureString))
            _requiredFeatures = _requiredFeatureString.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        DrawList(IpcSubscribers.Initialized.Label, "Last Initialized"u8, _initializedList);
        DrawList(IpcSubscribers.Disposed.Label,    "Last Disposed"u8,    _disposedList);

        IpcTester.DrawIntro(ApiVersion.Label, "Current Version"u8);
        var (breaking, features) = new ApiVersion(_pi).Invoke();
        Im.Text($"{breaking}.{features:D4}");

        IpcTester.DrawIntro(GetEnabledState.Label, "Current State"u8);
        Im.Text($"{new GetEnabledState(_pi).Invoke()}");

        IpcTester.DrawIntro(IpcSubscribers.EnabledChange.Label, "Last Change"u8);
        Im.Text(_lastEnabledValue is { } v ? $"{_lastEnabledChange} (to {v})" : "Never"u8);

        IpcTester.DrawIntro(SupportedFeatures.Label, "Supported Features"u8);
        Im.Text(StringU8.Join(", "u8, new SupportedFeatures(_pi).Invoke()));

        IpcTester.DrawIntro(CheckSupportedFeatures.Label, "Missing Features"u8);
        Im.Text(StringU8.Join(", "u8, new CheckSupportedFeatures(_pi).Invoke(_requiredFeatures)));

        DrawConfigPopup();
        IpcTester.DrawIntro(GetConfiguration.Label, "Configuration"u8);
        if (Im.Button("Get"u8))
        {
            _currentConfiguration = new GetConfiguration(_pi).Invoke();
            Im.Popup.Open("Config Popup"u8);
        }

        IpcTester.DrawIntro(GetModDirectory.Label, "Current Mod Directory"u8);
        Im.Text(new GetModDirectory(_pi).Invoke());

        IpcTester.DrawIntro(IpcSubscribers.ModDirectoryChanged.Label, "Last Mod Directory Change"u8);
        Im.Text(_lastModDirectoryTime > DateTimeOffset.MinValue
            ? $"{_lastModDirectory} ({(_lastModDirectoryValid ? "Valid" : "Invalid")}) at {_lastModDirectoryTime}"
            : "None"u8);

        void DrawList(string label, ReadOnlySpan<byte> text, List<DateTimeOffset> list)
        {
            IpcTester.DrawIntro(label, text);
            if (list.Count is 0)
            {
                Im.Text("Never"u8);
            }
            else
            {
                Im.Text(list[^1].LocalDateTime.ToString(CultureInfo.CurrentCulture));
                if (list.Count > 1 && Im.Item.Hovered())
                    Im.Tooltip.Set(
                        StringU8.Join((byte)'\n', list.SkipLast(1).Select(t => t.LocalDateTime.ToString(CultureInfo.CurrentCulture))));
            }
        }
    }

    private void DrawConfigPopup()
    {
        Im.Window.SetNextSize(ImEx.ScaledVector(500, 500));
        using var popup = Im.Popup.Begin("Config Popup"u8);
        if (!popup)
            return;

        using (Im.Font.PushMono())
        {
            Im.TextWrapped(_currentConfiguration);
        }

        if (Im.Button("Close"u8, -Vector2.UnitX) || !Im.Window.Focused())
            Im.Popup.CloseCurrent();
    }

    private void UpdateModDirectoryChanged(string path, bool valid)
        => (_lastModDirectory, _lastModDirectoryValid, _lastModDirectoryTime) = (path, valid, DateTimeOffset.Now);

    private void AddInitialized()
        => _initializedList.Add(DateTimeOffset.UtcNow);

    private void AddDisposed()
        => _disposedList.Add(DateTimeOffset.UtcNow);

    private void SetLastEnabled(bool val)
        => (_lastEnabledChange, _lastEnabledValue) = (DateTimeOffset.Now, val);
}
