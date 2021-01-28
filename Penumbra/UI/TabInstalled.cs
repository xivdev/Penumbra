using ImGuiNET;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private partial class TabInstalled
        {
            private const string LabelTab = "Installed Mods";

            private readonly SettingsInterface _base;
            public  readonly Selector          _selector;
            public  readonly ModPanel          _modPanel;

            public TabInstalled(SettingsInterface ui)
            {
                _base     = ui;
                _selector = new(_base);
                _modPanel = new(_base, _selector);
            }

            private void DrawNoModsAvailable()
            {
                ImGui.Text( "You don't have any mods :(" );
                ImGuiCustom.VerticalDistance(20f);
                ImGui.Text( "You'll need to install them first by creating a folder close to the root of your drive (preferably an SSD)." );
                ImGui.Text( "For example: D:/ffxiv/mods/" );
                ImGui.Text( "And pasting that path into the settings tab and clicking the 'Rediscover Mods' button." );
                ImGui.Text( "You can return to this tab once you've done that." );
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                    return;

                if (_base._plugin.ModManager.Mods != null)
                {
                    _selector.Draw();
                    ImGui.SameLine();
                    _modPanel.Draw();
                }
                else
                    DrawNoModsAvailable();

                ImGui.EndTabItem();

                return;
            }
        }
    }
}