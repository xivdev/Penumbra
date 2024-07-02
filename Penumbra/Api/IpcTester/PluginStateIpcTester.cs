using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class PluginStateIpcTester : IUiService, IDisposable
{
    private readonly IDalamudPluginInterface        _pi;
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
        using var _ = ImRaii.TreeNode("Plugin State");
        if (!_)
            return;

        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        DrawList(IpcSubscribers.Initialized.Label, "Last Initialized", _initializedList);
        DrawList(IpcSubscribers.Disposed.Label,    "Last Disposed",    _disposedList);

        IpcTester.DrawIntro(ApiVersion.Label, "Current Version");
        var (breaking, features) = new ApiVersion(_pi).Invoke();
        ImGui.TextUnformatted($"{breaking}.{features:D4}");

        IpcTester.DrawIntro(GetEnabledState.Label, "Current State");
        ImGui.TextUnformatted($"{new GetEnabledState(_pi).Invoke()}");

        IpcTester.DrawIntro(IpcSubscribers.EnabledChange.Label, "Last Change");
        ImGui.TextUnformatted(_lastEnabledValue is { } v ? $"{_lastEnabledChange} (to {v})" : "Never");

        DrawConfigPopup();
        IpcTester.DrawIntro(GetConfiguration.Label, "Configuration");
        if (ImGui.Button("Get"))
        {
            _currentConfiguration = new GetConfiguration(_pi).Invoke();
            ImGui.OpenPopup("Config Popup");
        }

        IpcTester.DrawIntro(GetModDirectory.Label, "Current Mod Directory");
        ImGui.TextUnformatted(new GetModDirectory(_pi).Invoke());

        IpcTester.DrawIntro(IpcSubscribers.ModDirectoryChanged.Label, "Last Mod Directory Change");
        ImGui.TextUnformatted(_lastModDirectoryTime > DateTimeOffset.MinValue
            ? $"{_lastModDirectory} ({(_lastModDirectoryValid ? "Valid" : "Invalid")}) at {_lastModDirectoryTime}"
            : "None");

        void DrawList(string label, string text, List<DateTimeOffset> list)
        {
            IpcTester.DrawIntro(label, text);
            if (list.Count == 0)
            {
                ImGui.TextUnformatted("Never");
            }
            else
            {
                ImGui.TextUnformatted(list[^1].LocalDateTime.ToString(CultureInfo.CurrentCulture));
                if (list.Count > 1 && ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.Join("\n",
                        list.SkipLast(1).Select(t => t.LocalDateTime.ToString(CultureInfo.CurrentCulture))));
            }
        }
    }

    private void DrawConfigPopup()
    {
        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(500, 500));
        using var popup = ImRaii.Popup("Config Popup");
        if (!popup)
            return;

        using (ImRaii.PushFont(UiBuilder.MonoFont))
        {
            ImGuiUtil.TextWrapped(_currentConfiguration);
        }

        if (ImGui.Button("Close", -Vector2.UnitX) || !ImGui.IsWindowFocused())
            ImGui.CloseCurrentPopup();
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
