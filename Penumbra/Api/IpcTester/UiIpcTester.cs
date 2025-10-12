using Dalamud.Plugin;
using ImSharp;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using MouseButton = Penumbra.Api.Enums.MouseButton;

namespace Penumbra.Api.IpcTester;

public class UiIpcTester : Luna.IUiService, IDisposable
{
    private readonly IDalamudPluginInterface                             _pi;
    public readonly  EventSubscriber<string, float, float>               PreSettingsTabBar;
    public readonly  EventSubscriber<string>                             PreSettingsPanel;
    public readonly  EventSubscriber<string>                             PostEnabled;
    public readonly  EventSubscriber<string>                             PostSettingsPanelDraw;
    public readonly  EventSubscriber<ChangedItemType, uint>              ChangedItemTooltip;
    public readonly  EventSubscriber<MouseButton, ChangedItemType, uint> ChangedItemClicked;

    private StringU8       _lastDrawnMod     = StringU8.Empty;
    private DateTimeOffset _lastDrawnModTime = DateTimeOffset.MinValue;
    private bool           _subscribedToTooltip;
    private bool           _subscribedToClick;
    private StringU8       _lastClicked = StringU8.Empty;
    private StringU8       _lastHovered = StringU8.Empty;
    private TabType        _selectTab   = TabType.None;
    private string         _modName     = string.Empty;
    private PenumbraApiEc  _ec          = PenumbraApiEc.Success;

    public UiIpcTester(IDalamudPluginInterface pi)
    {
        _pi                   = pi;
        PreSettingsTabBar     = PreSettingsTabBarDraw.Subscriber(pi, UpdateLastDrawnMod);
        PreSettingsPanel      = PreSettingsDraw.Subscriber(pi, UpdateLastDrawnMod);
        PostEnabled           = PostEnabledDraw.Subscriber(pi, UpdateLastDrawnMod);
        PostSettingsPanelDraw = PostSettingsDraw.Subscriber(pi, UpdateLastDrawnMod);
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
        using var _ = Im.Tree.Node("UI"u8);
        if (!_)
            return;

        EnumCombo<TabType>.Instance.Draw("Tab to Open at"u8, ref _selectTab, default, 0);
        Im.Input.Text("##openMod"u8, ref _modName, "Mod to Open at..."u8);
        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(PostSettingsDraw.Label, "Last Drawn Mod"u8);
        Im.Text(_lastDrawnMod.Length > 0 ? $"{_lastDrawnMod} at {_lastDrawnModTime}" : "None"u8);

        IpcTester.DrawIntro(IpcSubscribers.ChangedItemTooltip.Label, "Add Tooltip"u8);
        if (Im.Checkbox("##tooltip"u8, ref _subscribedToTooltip))
        {
            if (_subscribedToTooltip)
                ChangedItemTooltip.Enable();
            else
                ChangedItemTooltip.Disable();
        }

        Im.Line.Same();
        Im.Text(_lastHovered);

        IpcTester.DrawIntro(IpcSubscribers.ChangedItemClicked.Label, "Subscribe Click"u8);
        if (Im.Checkbox("##click"u8, ref _subscribedToClick))
        {
            if (_subscribedToClick)
                ChangedItemClicked.Enable();
            else
                ChangedItemClicked.Disable();
        }

        Im.Line.Same();
        Im.Text(_lastClicked);
        IpcTester.DrawIntro(OpenMainWindow.Label, "Open Mod Window"u8);
        if (Im.Button("Open##window"u8))
            _ec = new OpenMainWindow(_pi).Invoke(_selectTab, _modName, _modName);

        Im.Line.Same();
        Im.Text($"{_ec}");

        IpcTester.DrawIntro(CloseMainWindow.Label, "Close Mod Window"u8);
        if (Im.Button("Close##window"u8))
            new CloseMainWindow(_pi).Invoke();
    }

    private void UpdateLastDrawnMod(string name)
        => (_lastDrawnMod, _lastDrawnModTime) = (new StringU8(name), DateTimeOffset.Now);

    private void UpdateLastDrawnMod(string name, float _1, float _2)
        => (_lastDrawnMod, _lastDrawnModTime) = (new StringU8(name), DateTimeOffset.Now);

    private void AddedTooltip(ChangedItemType type, uint id)
    {
        _lastHovered = new StringU8($"{type} {id} at {DateTime.UtcNow.ToLocalTime().ToString(CultureInfo.CurrentCulture)}");
        Im.Text("IPC Test Successful"u8);
    }

    private void AddedClick(MouseButton button, ChangedItemType type, uint id)
        => _lastClicked =
            new StringU8($"{button}-click on {type} {id} at {DateTime.UtcNow.ToLocalTime().ToString(CultureInfo.CurrentCulture)}");
}
