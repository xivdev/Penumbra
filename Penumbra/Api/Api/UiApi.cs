using FFXIVClientStructs.FFXIV.Common.Lua;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI;

namespace Penumbra.Api.Api;

public class UiApi : IPenumbraApiUi, Luna.IApiService, IDisposable
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
        _communicator.PreSettingsTabBarDraw.Subscribe(OnPreSettingsTabBarDraw, Communication.PreSettingsTabBarDraw.Priority.Default);
        _communicator.PreSettingsPanelDraw.Subscribe(OnPreSettingsPanelDraw, Communication.PreSettingsPanelDraw.Priority.Default);
        _communicator.PostEnabledDraw.Subscribe(OnPostEnabledDraw, Communication.PostEnabledDraw.Priority.Default);
        _communicator.PostSettingsPanelDraw.Subscribe(OnPostSettingsPanelDraw, Communication.PostSettingsPanelDraw.Priority.Default);
    }

    private void OnPostSettingsPanelDraw(in PostSettingsPanelDraw.Arguments arguments)
        => PostSettingsPanelDraw?.Invoke(arguments.Mod.Identifier);

    private void OnPostEnabledDraw(in PostEnabledDraw.Arguments arguments)
        => PostEnabledDraw?.Invoke(arguments.Mod.Identifier);

    private void OnPreSettingsPanelDraw(in PreSettingsPanelDraw.Arguments arguments)
        => PreSettingsPanelDraw?.Invoke(arguments.Mod.Identifier);

    private void OnPreSettingsTabBarDraw(in PreSettingsTabBarDraw.Arguments arguments)
        => PreSettingsTabBarDraw?.Invoke(arguments.Mod.Identifier, arguments.HeaderWidth, arguments.TitleBoxWidth);

    public void Dispose()
    {
        _communicator.ChangedItemHover.Unsubscribe(OnChangedItemHover);
        _communicator.ChangedItemClick.Unsubscribe(OnChangedItemClick);
        _communicator.PreSettingsTabBarDraw.Unsubscribe(OnPreSettingsTabBarDraw);
        _communicator.PreSettingsPanelDraw.Unsubscribe(OnPreSettingsPanelDraw);
        _communicator.PostEnabledDraw.Unsubscribe(OnPostEnabledDraw);
        _communicator.PostSettingsPanelDraw.Unsubscribe(OnPostSettingsPanelDraw);
    }

    public event Action<ChangedItemType, uint>?              ChangedItemTooltip;
    public event Action<MouseButton, ChangedItemType, uint>? ChangedItemClicked;
    public event Action<string, float, float>?               PreSettingsTabBarDraw;
    public event Action<string>?                             PreSettingsPanelDraw;
    public event Action<string>?                             PostEnabledDraw;
    public event Action<string>?                             PostSettingsPanelDraw;

    public PenumbraApiEc OpenMainWindow(TabType tab, string modDirectory, string modName)
    {
        _configWindow.IsOpen = true;
        if (!Enum.IsDefined(tab))
            return PenumbraApiEc.InvalidArgument;

        if (tab == TabType.Mods && (modDirectory.Length > 0 || modName.Length > 0))
        {
            if (_modManager.TryGetMod(modDirectory, modName, out var mod))
                _communicator.SelectTab.Invoke(new SelectTab.Arguments(tab, mod));
            else
                return PenumbraApiEc.ModMissing;
        }
        else if (tab != TabType.None)
        {
            _communicator.SelectTab.Invoke(new SelectTab.Arguments(tab, null));
        }

        return PenumbraApiEc.Success;
    }

    public void CloseMainWindow()
        => _configWindow.IsOpen = false;

    private void OnChangedItemClick(in ChangedItemClick.Arguments arguments)
    {
        if (ChangedItemClicked == null)
            return;

        var (type, id) = arguments.Data.ToApiObject();
        ChangedItemClicked.Invoke(arguments.Button, type, id);
    }

    private void OnChangedItemHover(in ChangedItemHover.Arguments arguments)
    {
        if (ChangedItemTooltip == null)
            return;

        var (type, id) = arguments.Data.ToApiObject();
        ChangedItemTooltip.Invoke(type, id);
    }
}
