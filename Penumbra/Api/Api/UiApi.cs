using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI;

namespace Penumbra.Api.Api;

public class UiApi : IPenumbraApiUi, IApiService, IDisposable
{
    private readonly CommunicatorService _communicator;
    private readonly ConfigWindow        _configWindow;
    private readonly ModManager          _modManager;

    public UiApi(CommunicatorService communicator, ConfigWindow configWindow, ModManager modManager)
    {
        _communicator = communicator;
        _configWindow = configWindow;
        _modManager   = modManager;
        _communicator.ChangedItemHover.Subscribe(OnChangedItemHover, ChangedItemHover.Priority.Default);
        _communicator.ChangedItemClick.Subscribe(OnChangedItemClick, ChangedItemClick.Priority.Default);
    }

    public void Dispose()
    {
        _communicator.ChangedItemHover.Unsubscribe(OnChangedItemHover);
        _communicator.ChangedItemClick.Unsubscribe(OnChangedItemClick);
    }

    public event Action<ChangedItemType, uint>? ChangedItemTooltip;

    public event Action<MouseButton, ChangedItemType, uint>? ChangedItemClicked;

    public event Action<string, float, float>? PreSettingsTabBarDraw
    {
        add => _communicator.PreSettingsTabBarDraw.Subscribe(value!, Communication.PreSettingsTabBarDraw.Priority.Default);
        remove => _communicator.PreSettingsTabBarDraw.Unsubscribe(value!);
    }

    public event Action<string>? PreSettingsPanelDraw
    {
        add => _communicator.PreSettingsPanelDraw.Subscribe(value!, Communication.PreSettingsPanelDraw.Priority.Default);
        remove => _communicator.PreSettingsPanelDraw.Unsubscribe(value!);
    }

    public event Action<string>? PostEnabledDraw
    {
        add => _communicator.PostEnabledDraw.Subscribe(value!, Communication.PostEnabledDraw.Priority.Default);
        remove => _communicator.PostEnabledDraw.Unsubscribe(value!);
    }

    public event Action<string>? PostSettingsPanelDraw
    {
        add => _communicator.PostSettingsPanelDraw.Subscribe(value!, Communication.PostSettingsPanelDraw.Priority.Default);
        remove => _communicator.PostSettingsPanelDraw.Unsubscribe(value!);
    }

    public PenumbraApiEc OpenMainWindow(TabType tab, string modDirectory, string modName)
    {
        _configWindow.IsOpen = true;
        if (!Enum.IsDefined(tab))
            return PenumbraApiEc.InvalidArgument;

        if (tab == TabType.Mods && (modDirectory.Length > 0 || modName.Length > 0))
        {
            if (_modManager.TryGetMod(modDirectory, modName, out var mod))
                _communicator.SelectTab.Invoke(tab, mod);
            else
                return PenumbraApiEc.ModMissing;
        }
        else if (tab != TabType.None)
        {
            _communicator.SelectTab.Invoke(tab, null);
        }

        return PenumbraApiEc.Success;
    }

    public void CloseMainWindow()
        => _configWindow.IsOpen = false;

    private void OnChangedItemClick(MouseButton button, IIdentifiedObjectData data)
    {
        if (ChangedItemClicked == null)
            return;

        var (type, id) = data.ToApiObject();
        ChangedItemClicked.Invoke(button, type, id);
    }

    private void OnChangedItemHover(IIdentifiedObjectData data)
    {
        if (ChangedItemTooltip == null)
            return;

        var (type, id) = data.ToApiObject();
        ChangedItemTooltip.Invoke(type, id);
    }
}
