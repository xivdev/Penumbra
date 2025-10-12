using Dalamud.Plugin.Services;
using ImSharp;
using Penumbra.Api.Api;

namespace Penumbra.Api.IpcTester;

public class IpcTester(
    IPenumbraApi api,
    PluginStateIpcTester pluginStateIpcTester,
    UiIpcTester uiIpcTester,
    RedrawingIpcTester redrawingIpcTester,
    GameStateIpcTester gameStateIpcTester,
    ResolveIpcTester resolveIpcTester,
    CollectionsIpcTester collectionsIpcTester,
    MetaIpcTester metaIpcTester,
    ModsIpcTester modsIpcTester,
    ModSettingsIpcTester modSettingsIpcTester,
    EditingIpcTester editingIpcTester,
    TemporaryIpcTester temporaryIpcTester,
    ResourceTreeIpcTester resourceTreeIpcTester,
    IFramework framework) : Luna.IUiService
{
    private DateTime _lastUpdate;
    private bool     _subscribed;

    public void Draw()
    {
        try
        {
            _lastUpdate = framework.LastUpdateUTC.AddSeconds(1);
            Subscribe();

            Im.Text($"API Version: {api.ApiVersion.Breaking}.{api.ApiVersion.Feature:D4}");
            collectionsIpcTester.Draw();
            editingIpcTester.Draw();
            gameStateIpcTester.Draw();
            metaIpcTester.Draw();
            modSettingsIpcTester.Draw();
            modsIpcTester.Draw();
            pluginStateIpcTester.Draw();
            redrawingIpcTester.Draw();
            resolveIpcTester.Draw();
            resourceTreeIpcTester.Draw();
            uiIpcTester.Draw();
            temporaryIpcTester.Draw();
            temporaryIpcTester.DrawCollections();
            temporaryIpcTester.DrawMods();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Error during IPC Tests:\n{e}");
        }
    }

    internal static void DrawIntro(Utf8StringHandler<LabelStringHandlerBuffer> label, Utf8StringHandler<TextStringHandlerBuffer> info)
    {
        Im.Table.NextRow();
        Im.Table.DrawColumn(ref label);
        Im.Table.DrawColumn(ref info);
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        Penumbra.Log.Debug("[IPCTester] Subscribed to IPC events for IPC tester.");
        gameStateIpcTester.GameObjectResourcePathResolved.Enable();
        gameStateIpcTester.CharacterBaseCreated.Enable();
        gameStateIpcTester.CharacterBaseCreating.Enable();
        modSettingsIpcTester.SettingChanged.Enable();
        modsIpcTester.DeleteSubscriber.Enable();
        modsIpcTester.AddSubscriber.Enable();
        modsIpcTester.MoveSubscriber.Enable();
        pluginStateIpcTester.ModDirectoryChanged.Enable();
        pluginStateIpcTester.Initialized.Enable();
        pluginStateIpcTester.Disposed.Enable();
        pluginStateIpcTester.EnabledChange.Enable();
        redrawingIpcTester.Redrawn.Enable();
        uiIpcTester.PreSettingsTabBar.Enable();
        uiIpcTester.PreSettingsPanel.Enable();
        uiIpcTester.PostEnabled.Enable();
        uiIpcTester.PostSettingsPanelDraw.Enable();
        uiIpcTester.ChangedItemTooltip.Enable();
        uiIpcTester.ChangedItemClicked.Enable();

        framework.Update += CheckUnsubscribe;
        _subscribed      =  true;
    }

    private void CheckUnsubscribe(IFramework framework1)
    {
        if (_lastUpdate > framework.LastUpdateUTC)
            return;

        Unsubscribe();
        framework.Update -= CheckUnsubscribe;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        Penumbra.Log.Debug("[IPCTester] Unsubscribed from IPC events for IPC tester.");
        _subscribed = false;
        gameStateIpcTester.GameObjectResourcePathResolved.Disable();
        gameStateIpcTester.CharacterBaseCreated.Disable();
        gameStateIpcTester.CharacterBaseCreating.Disable();
        modSettingsIpcTester.SettingChanged.Disable();
        modsIpcTester.DeleteSubscriber.Disable();
        modsIpcTester.AddSubscriber.Disable();
        modsIpcTester.MoveSubscriber.Disable();
        pluginStateIpcTester.ModDirectoryChanged.Disable();
        pluginStateIpcTester.Initialized.Disable();
        pluginStateIpcTester.Disposed.Disable();
        pluginStateIpcTester.EnabledChange.Disable();
        redrawingIpcTester.Redrawn.Disable();
        uiIpcTester.PreSettingsTabBar.Disable();
        uiIpcTester.PreSettingsPanel.Disable();
        uiIpcTester.PostEnabled.Disable();
        uiIpcTester.PostSettingsPanelDraw.Disable();
        uiIpcTester.ChangedItemTooltip.Disable();
        uiIpcTester.ChangedItemClicked.Disable();
    }
}
