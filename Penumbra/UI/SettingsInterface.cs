using System.IO;
using System.Numerics;
using Penumbra.Mods;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private const float DefaultVerticalSpace = 20f;

        private static readonly Vector2 AutoFillSize = new( -1, -1 );
        private static readonly Vector2 ZeroVector   = new( 0, 0 );

        private readonly Plugin _plugin;

        private readonly ManageModsButton _manageModsButton;
        private readonly MenuBar      _menuBar;
        private readonly SettingsMenu _menu;

        public SettingsInterface( Plugin plugin )
        {
            _plugin       = plugin;
            _manageModsButton = new ManageModsButton( this );
            _menuBar      = new MenuBar( this );
            _menu         = new SettingsMenu( this );
        }

        public void FlipVisibility() => _menu.Visible = !_menu.Visible;

        public void Draw()
        {
            _menuBar.Draw();
            _manageModsButton.Draw();
            _menu.Draw();
        }

        private void ReloadMods()
        {
            _menu.InstalledTab.Selector.ResetModNamesLower();
            _menu.InstalledTab.Selector.ClearSelection();
            // create the directory if it doesn't exist
            Directory.CreateDirectory( _plugin.Configuration.CurrentCollection );

            var modManager = Service< ModManager >.Get();
            modManager.DiscoverMods( _plugin.Configuration.CurrentCollection );
            _menu.EffectiveTab.RebuildFileList( _plugin.Configuration.ShowAdvanced );
            _menu.InstalledTab.Selector.ResetModNamesLower();
        }
    }
}