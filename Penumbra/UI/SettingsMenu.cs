using System.Numerics;
using ImGuiNET;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private partial class SettingsMenu
        {
            private const string PenumbraSettingsLabel = "PenumbraSettings";

            private static readonly Vector2 MinSettingsSize = new( 800, 450 );
            private static readonly Vector2 MaxSettingsSize = new( 69420, 42069 );

            private readonly SettingsInterface _base;
            public  readonly TabSettings       _settingsTab;
            public  readonly TabImport         _importTab;
            public  readonly TabBrowser        _browserTab;
            public  readonly TabInstalled      _installedTab;
            public  readonly TabEffective      _effectiveTab;

            public SettingsMenu(SettingsInterface ui)
            {
                _base         = ui;
                _settingsTab  = new(_base);
                _importTab    = new(_base);
                _browserTab   = new();
                _installedTab = new(_base);
                _effectiveTab = new(_base);
            }

#if DEBUG
            private const bool DefaultVisibility = true;
#else
            private const bool DefaultVisibility = false;
#endif
            public bool Visible = DefaultVisibility;

            public void Draw()
            {
                if( !Visible )
                    return;

                ImGui.SetNextWindowSizeConstraints( MinSettingsSize, MaxSettingsSize );
#if DEBUG
                var ret = ImGui.Begin( _base._plugin.PluginDebugTitleStr, ref Visible );
#else
                var ret = ImGui.Begin( _base._plugin.Name, ref Visible );
#endif
                if( !ret )
                    return;

                ImGui.BeginTabBar( PenumbraSettingsLabel );

                _settingsTab.Draw();
                _importTab.Draw();

                if( !_importTab.IsImporting() )
                {
                    _browserTab.Draw();
                    _installedTab.Draw();

                    if( _base._plugin.Configuration.ShowAdvanced )
                        _effectiveTab.Draw();
                }

                ImGui.EndTabBar();
                ImGui.End();
            }
        }
    }
}