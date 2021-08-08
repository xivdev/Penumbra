using System.Numerics;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private const float DefaultVerticalSpace = 20f;

        private static readonly Vector2 AutoFillSize = new( -1, -1 );
        private static readonly Vector2 ZeroVector   = new( 0, 0 );

        private readonly Plugin _plugin;

        private readonly ManageModsButton _manageModsButton;
        private readonly MenuBar          _menuBar;
        private readonly SettingsMenu     _menu;
        private readonly ModManager       _modManager;

        public SettingsInterface( Plugin plugin )
        {
            _plugin           = plugin;
            _manageModsButton = new ManageModsButton( this );
            _menuBar          = new MenuBar( this );
            _menu             = new SettingsMenu( this );
            _modManager       = Service< ModManager >.Get();
        }

        public void FlipVisibility()
            => _menu.Visible = !_menu.Visible;

        public void MakeDebugTabVisible()
            => _menu.DebugTabVisible = true;

        public void Draw()
        {
            _menuBar.Draw();
            _manageModsButton.Draw();
            _menu.Draw();
        }

        private void ReloadMods()
        {
            _menu.InstalledTab.Selector.ClearSelection();
            _modManager.DiscoverMods( _plugin.Configuration.ModDirectory );
            _menu.InstalledTab.Selector.Cache.ResetModList();
        }

        private void SaveCurrentCollection( bool recalculateMeta )
        {
            var current = _modManager.Collections.CurrentCollection;
            current.Save( _plugin.PluginInterface );
            RecalculateCurrent( recalculateMeta );
        }

        private void RecalculateCurrent( bool recalculateMeta )
        {
            var current = _modManager.Collections.CurrentCollection;
            if( current.Cache != null )
            {
                current.CalculateEffectiveFileList( _modManager.BasePath, recalculateMeta,
                    current == _modManager.Collections.ActiveCollection );
                _menu.InstalledTab.Selector.Cache.ResetFilters();
            }
        }
    }
}