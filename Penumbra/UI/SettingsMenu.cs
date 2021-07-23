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

            private readonly SettingsInterface _base;
            private readonly TabSettings       _settingsTab;
            private readonly TabImport         _importTab;
            private readonly TabBrowser        _browserTab;
            private readonly TabCollections    _collectionsTab;
            public readonly  TabInstalled      InstalledTab;
            private readonly TabEffective      _effectiveTab;

            public SettingsMenu( SettingsInterface ui )
            {
                _base           = ui;
                _settingsTab    = new TabSettings( _base );
                _importTab      = new TabImport( _base );
                _browserTab     = new TabBrowser();
                InstalledTab    = new TabInstalled( _base );
                _collectionsTab = new TabCollections( InstalledTab.Selector );
                _effectiveTab   = new TabEffective();
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
                var ret = ImGui.Begin( _base._plugin.PluginDebugTitleStr, ref Visible );
#else
                var ret = ImGui.Begin( _base._plugin.Name, ref Visible );
#endif
                if( !ret )
                {
                    return;
                }

                ImGui.BeginTabBar( PenumbraSettingsLabel );

                _settingsTab.Draw();
                _collectionsTab.Draw();
                _importTab.Draw();

                if( Service<ModManager>.Get().Valid && !_importTab.IsImporting() )
                {
                    _browserTab.Draw();
                    InstalledTab.Draw();

                    if( _base._plugin!.Configuration!.ShowAdvanced )
                    {
                        _effectiveTab.Draw();
                    }
                }

                if( DebugTabVisible )
                {
                    _base.DrawDebugTab();
                }

                ImGui.EndTabBar();
                ImGui.End();
            }
        }
    }
}