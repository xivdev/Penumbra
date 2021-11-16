using System.Collections.Generic;
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

            public TabInstalled( SettingsInterface ui, HashSet< string > newMods )
            {
                Selector    = new Selector( ui, newMods );
                ModPanel    = new ModPanel( ui, Selector, newMods );
                _modManager = Service< ModManager >.Get();
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
}