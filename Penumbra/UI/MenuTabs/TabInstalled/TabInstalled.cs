using System.Collections.Generic;
using ImGuiNET;
using Penumbra.UI.Custom;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private class TabInstalled
    {
        private const string LabelTab = "Installed Mods";

        public readonly Selector Selector;
        public readonly ModPanel ModPanel;

        public TabInstalled( SettingsInterface ui, HashSet< string > newMods )
        {
            Selector = new Selector( ui, newMods );
            ModPanel = new ModPanel( ui, Selector, newMods );
        }

        public void Draw()
        {
            var ret = ImGui.BeginTabItem( LabelTab );
            if( !ret )
            {
                return;
            }

            using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

            Selector.Draw();
            ImGui.SameLine();
            ModPanel.Draw();
        }
    }
}