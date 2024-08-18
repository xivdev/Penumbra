using Dalamud.Plugin;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class UiIpcTester : IUiService, IDisposable
{
    private readonly IDalamudPluginInterface                              _pi;
    public readonly  EventSubscriber<string, float, float>               PreSettingsTabBar;
    public readonly  EventSubscriber<string>                             PreSettingsPanel;
    public readonly  EventSubscriber<string>                             PostEnabled;
    public readonly  EventSubscriber<string>                             PostSettingsPanelDraw;
    public readonly  EventSubscriber<ChangedItemType, uint>              ChangedItemTooltip;
    public readonly  EventSubscriber<MouseButton, ChangedItemType, uint> ChangedItemClicked;

    private string         _lastDrawnMod     = string.Empty;
    private DateTimeOffset _lastDrawnModTime = DateTimeOffset.MinValue;
    private bool           _subscribedToTooltip;
    private bool           _subscribedToClick;
    private string         _lastClicked = string.Empty;
    private string         _lastHovered = string.Empty;
    private TabType        _selectTab   = TabType.None;
    private string         _modName     = string.Empty;
    private PenumbraApiEc  _ec          = PenumbraApiEc.Success;

    public UiIpcTester(IDalamudPluginInterface pi)
    {
        _pi                   = pi;
        PreSettingsTabBar     = IpcSubscribers.PreSettingsTabBarDraw.Subscriber(pi, UpdateLastDrawnMod);
        PreSettingsPanel      = IpcSubscribers.PreSettingsDraw.Subscriber(pi, UpdateLastDrawnMod);
        PostEnabled           = IpcSubscribers.PostEnabledDraw.Subscriber(pi, UpdateLastDrawnMod);
        PostSettingsPanelDraw = IpcSubscribers.PostSettingsDraw.Subscriber(pi, UpdateLastDrawnMod);
        ChangedItemTooltip    = IpcSubscribers.ChangedItemTooltip.Subscriber(pi, AddedTooltip);
        ChangedItemClicked    = IpcSubscribers.ChangedItemClicked.Subscriber(pi, AddedClick);
        PreSettingsTabBar.Disable();
        PreSettingsPanel.Disable();
        PostEnabled.Disable();
        PostSettingsPanelDraw.Disable();
        ChangedItemTooltip.Disable();
        ChangedItemClicked.Disable();
    }

    public void Dispose()
    {
        PreSettingsTabBar.Dispose();
        PreSettingsPanel.Dispose();
        PostEnabled.Dispose();
        PostSettingsPanelDraw.Dispose();
        ChangedItemTooltip.Dispose();
        ChangedItemClicked.Dispose();
    }

    public void Draw()
    {
        using var _ = ImRaii.TreeNode("UI");
        if (!_)
            return;

        using (var combo = ImRaii.Combo("Tab to Open at", _selectTab.ToString()))
        {
            if (combo)
                foreach (var val in Enum.GetValues<TabType>())
                {
                    if (ImGui.Selectable(val.ToString(), _selectTab == val))
                        _selectTab = val;
                }
        }

        ImGui.InputTextWithHint("##openMod", "Mod to Open at...", ref _modName, 256);
        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(IpcSubscribers.PostSettingsDraw.Label, "Last Drawn Mod");
        ImGui.TextUnformatted(_lastDrawnMod.Length > 0 ? $"{_lastDrawnMod} at {_lastDrawnModTime}" : "None");

        IpcTester.DrawIntro(IpcSubscribers.ChangedItemTooltip.Label, "Add Tooltip");
        if (ImGui.Checkbox("##tooltip", ref _subscribedToTooltip))
        {
            if (_subscribedToTooltip)
                ChangedItemTooltip.Enable();
            else
                ChangedItemTooltip.Disable();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(_lastHovered);

        IpcTester.DrawIntro(IpcSubscribers.ChangedItemClicked.Label, "Subscribe Click");
        if (ImGui.Checkbox("##click", ref _subscribedToClick))
        {
            if (_subscribedToClick)
                ChangedItemClicked.Enable();
            else
                ChangedItemClicked.Disable();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(_lastClicked);
        IpcTester.DrawIntro(OpenMainWindow.Label, "Open Mod Window");
        if (ImGui.Button("Open##window"))
            _ec = new OpenMainWindow(_pi).Invoke(_selectTab, _modName, _modName);

        ImGui.SameLine();
        ImGui.TextUnformatted(_ec.ToString());

        IpcTester.DrawIntro(CloseMainWindow.Label, "Close Mod Window");
        if (ImGui.Button("Close##window"))
            new CloseMainWindow(_pi).Invoke();
    }

    private void UpdateLastDrawnMod(string name)
        => (_lastDrawnMod, _lastDrawnModTime) = (name, DateTimeOffset.Now);

    private void UpdateLastDrawnMod(string name, float _1, float _2)
        => (_lastDrawnMod, _lastDrawnModTime) = (name, DateTimeOffset.Now);

    private void AddedTooltip(ChangedItemType type, uint id)
    {
        _lastHovered = $"{type} {id} at {DateTime.UtcNow.ToLocalTime().ToString(CultureInfo.CurrentCulture)}";
        ImGui.TextUnformatted("IPC Test Successful");
    }

    private void AddedClick(MouseButton button, ChangedItemType type, uint id)
    {
        _lastClicked = $"{button}-click on {type} {id} at {DateTime.UtcNow.ToLocalTime().ToString(CultureInfo.CurrentCulture)}";
    }
}
