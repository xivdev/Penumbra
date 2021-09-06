using ImGuiNET;
using Penumbra.Mods;
using Penumbra.UI.Custom;
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
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

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
            }
        }
    }
}