using System.Numerics;
using ImGuiNET;
using Penumbra.Mods;
using Penumbra.UI.Custom;
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
            private readonly  TabEffective      _effectiveTab;
            private readonly  TabChangedItems   _changedItems;
            internal readonly TabCollections    CollectionsTab;
            internal readonly TabInstalled      InstalledTab;

            public SettingsMenu( SettingsInterface ui )
            {
                _base          = ui;
                _settingsTab   = new TabSettings( _base );
                _importTab     = new TabImport( _base );
                _browserTab    = new TabBrowser();
                InstalledTab   = new TabInstalled( _base, _importTab.NewMods );
                CollectionsTab = new TabCollections( InstalledTab.Selector );
                _effectiveTab  = new TabEffective();
                _changedItems  = new TabChangedItems( _base );
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
                using var raii = ImGuiRaii.DeferredEnd( ImGui.End );
                if( !ret )
                {
                    return;
                }

                ImGui.BeginTabBar( PenumbraSettingsLabel );
                raii.Push( ImGui.EndTabBar );

                _settingsTab.Draw();
                CollectionsTab.Draw();
                _importTab.Draw();

                if( Service< ModManager >.Get().Valid && !_importTab.IsImporting() )
                {
                    _browserTab.Draw();
                    InstalledTab.Draw();
                    _changedItems.Draw();
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
            }
        }
    }
}