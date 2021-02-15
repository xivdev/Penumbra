using System.IO;
using System.Numerics;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private const float DefaultVerticalSpace = 20f;

        private static readonly Vector2 AutoFillSize = new(-1, -1);
        private static readonly Vector2 ZeroVector   = new( 0, 0);

        private readonly Plugin _plugin;

        private readonly LaunchButton _launchButton;
        private readonly MenuBar      _menuBar;
        private readonly SettingsMenu _menu;

        public SettingsInterface( Plugin plugin )
        {
            _plugin       = plugin;
            _launchButton = new(this);
            _menuBar      = new(this);
            _menu         = new(this);
        }

        public void FlipVisibility() => _menu.Visible = !_menu.Visible;

        public void Draw()
        {
            _menuBar.Draw();
            _launchButton.Draw();
            _menu.Draw();
        }

        private void ReloadMods()
        {
            _menu._installedTab._selector.ResetModNamesLower();
            _menu._installedTab._selector.ClearSelection();
            // create the directory if it doesn't exist
            Directory.CreateDirectory( _plugin.Configuration.CurrentCollection );

            _plugin.ModManager.DiscoverMods( _plugin.Configuration.CurrentCollection );
            _menu._effectiveTab.RebuildFileList(_plugin.Configuration.ShowAdvanced);
            _menu._installedTab._selector.ResetModNamesLower();
        }
    }
}