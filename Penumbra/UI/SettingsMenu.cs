using System.Numerics;
using ImGuiNET;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class SettingsMenu
        {
            private const string PenumbraSettingsLabel = "PenumbraSettings";

            public static readonly Vector2 MinSettingsSize = new( 800, 450 );
            public static readonly Vector2 MaxSettingsSize = new( 69420, 42069 );

            private readonly  SettingsInterface _base;
            private readonly  TabSettings       _settingsTab;
            private readonly  TabImport         _importTab;
            private readonly  TabBrowser        _browserTab;
            internal readonly TabCollections    CollectionsTab;
            internal readonly TabInstalled      InstalledTab;
            private readonly  TabEffective      _effectiveTab;

            public SettingsMenu( SettingsInterface ui )
            {
                _base          = ui;
                _settingsTab   = new TabSettings( _base );
                _importTab     = new TabImport( _base );
                _browserTab    = new TabBrowser();
                InstalledTab   = new TabInstalled( _base );
                CollectionsTab = new TabCollections( InstalledTab.Selector );
                _effectiveTab  = new TabEffective();
            }

#if DEBUG
            private const bool DefaultVisibility = true;
#else
            private const bool DefaultVisibility = false;
#endif
            public bool Visible         = DefaultVisibility;
            public bool DebugTabVisible = DefaultVisibility;

            public void Draw()
            {
                if( !Visible )
                {
                    return;
                }

                ImGui.SetNextWindowSizeConstraints( MinSettingsSize, MaxSettingsSize );
#if DEBUG
                var ret = ImGui.Begin( _base._penumbra.PluginDebugTitleStr, ref Visible );
#else
                var ret = ImGui.Begin( _base._penumbra.Name, ref Visible );
#endif
                if( !ret )
                {
                    ImGui.End();
                    return;
                }

                ImGui.BeginTabBar( PenumbraSettingsLabel );

                _settingsTab.Draw();
                CollectionsTab.Draw();
                _importTab.Draw();

                if( Service< ModManager >.Get().Valid && !_importTab.IsImporting() )
                {
                    _browserTab.Draw();
                    InstalledTab.Draw();

                    if( Penumbra.Config.ShowAdvanced )
                    {
                        _effectiveTab.Draw();
                    }
                }

                if( DebugTabVisible )
                {
                    _base.DrawDebugTab();
                    _base.DrawResourceManagerTab();
                }

                ImGui.EndTabBar();
                ImGui.End();
            }
        }
    }
}
