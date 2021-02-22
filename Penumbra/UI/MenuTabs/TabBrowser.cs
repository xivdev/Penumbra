using System.Diagnostics;
using ImGuiNET;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabBrowser
        {
            [Conditional( "DEBUG" )]
            public void Draw()
            {
                var ret = ImGui.BeginTabItem( "Available Mods" );
                if( !ret )
                {
                    return;
                }

                ImGui.Text( "woah" );
                ImGui.EndTabItem();
            }
        }
    }
}