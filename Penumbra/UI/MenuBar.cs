using ImGuiNET;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class MenuBar
        {
            private const string MenuLabel          = "Penumbra";
            private const string MenuItemToggle     = "Toggle UI";
            private const string SlashCommand       = "/penumbra";
            private const string MenuItemRediscover = "Rediscover Mods";
            private const string MenuItemHide       = "Hide Menu Bar";

#if DEBUG
            private bool _showDebugBar = true;
#else
            private const bool _showDebugBar = false;
#endif

            private readonly SettingsInterface _base;
            public MenuBar(SettingsInterface ui) => _base = ui;

            public void Draw()
            {
                if( _showDebugBar && ImGui.BeginMainMenuBar() )
                {
                    if( ImGui.BeginMenu( MenuLabel ) )
                    {
                        if( ImGui.MenuItem( MenuItemToggle, SlashCommand, _base._menu.Visible ) )
                            _base.FlipVisibility();

                        if( ImGui.MenuItem( MenuItemRediscover ) )
                            _base.ReloadMods();
#if DEBUG
                        if ( ImGui.MenuItem( MenuItemHide) )
                            _showDebugBar = false;
#endif

                        ImGui.EndMenu();
                    }

                    ImGui.EndMainMenuBar();
                }
            }
        }
    }
}