using ImGuiNET;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabInstalled
        {
            private const string LabelTab = "Installed Mods";

            private readonly ModManager _modManager;
            public readonly  Selector   Selector;
            public readonly  ModPanel   ModPanel;

            public TabInstalled( SettingsInterface ui )
            {
                Selector    = new Selector( ui );
                ModPanel    = new ModPanel( ui, Selector );
                _modManager = Service< ModManager >.Get();
            }

            private static void DrawNoModsAvailable()
            {
                ImGui.Text( "You don't have any mods :(" );
                Custom.ImGuiCustom.VerticalDistance( 20f );
                ImGui.Text( "You'll need to install them first by creating a folder close to the root of your drive (preferably an SSD)." );
                ImGui.Text( "For example: D:/ffxiv/mods/" );
                ImGui.Text( "And pasting that path into the settings tab and clicking the 'Rediscover Mods' button." );
                ImGui.Text( "You can return to this tab once you've done that." );
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                {
                    return;
                }

                if( _modManager.Mods.Count > 0 )
                {
                    Selector.Draw();
                    ImGui.SameLine();
                    ModPanel.Draw();
                }
                else
                {
                    DrawNoModsAvailable();
                }

                ImGui.EndTabItem();
            }
        }
    }
}