using System;
using System.Numerics;

namespace Penumbra.UI;

public partial class SettingsInterface : IDisposable
{
    private const float DefaultVerticalSpace = 20f;

    private static readonly Vector2 AutoFillSize = new(-1, -1);
    private static readonly Vector2 ZeroVector   = new(0, 0);

    private readonly Penumbra _penumbra;

    private readonly ManageModsButton _manageModsButton;
    private readonly SettingsMenu     _menu;

    public SettingsInterface( Penumbra penumbra )
    {
        _penumbra         = penumbra;
        _manageModsButton = new ManageModsButton( this );
        _menu             = new SettingsMenu( this );

        Dalamud.PluginInterface.UiBuilder.DisableGposeUiHide =  true;
        Dalamud.PluginInterface.UiBuilder.Draw               += Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi       += OpenConfig;
    }

    public void Dispose()
    {
        _manageModsButton.Dispose();
        _menu.InstalledTab.Selector.Cache.Dispose();
        Dalamud.PluginInterface.UiBuilder.Draw         -= Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
    }

    private void OpenConfig()
        => _menu.Visible = true;

    public void FlipVisibility()
        => _menu.Visible = !_menu.Visible;

    public void MakeDebugTabVisible()
        => _menu.DebugTabVisible = true;

    public void Draw()
    {
        _menu.Draw();
    }

    private void ReloadMods()
    {
        _menu.InstalledTab.Selector.ClearSelection();
        Penumbra.ModManager.DiscoverMods( Penumbra.Config.ModDirectory );
        _menu.InstalledTab.Selector.Cache.TriggerListReset();
    }

    private void SaveCurrentCollection( bool recalculateMeta )
    {
        var current = Penumbra.CollectionManager.CurrentCollection;
        current.Save();
        RecalculateCurrent( recalculateMeta );
    }

    private void RecalculateCurrent( bool recalculateMeta )
    {
        var current    = Penumbra.CollectionManager.CurrentCollection;
        if( current.Cache != null )
        {
            current.CalculateEffectiveFileList( recalculateMeta, Penumbra.CollectionManager.IsActive( current ) );
            _menu.InstalledTab.Selector.Cache.TriggerFilterReset();
        }
    }

    public void ResetDefaultCollection()
        => _menu.CollectionsTab.UpdateDefaultIndex();

    public void ResetForcedCollection()
        => _menu.CollectionsTab.UpdateForcedIndex();
}