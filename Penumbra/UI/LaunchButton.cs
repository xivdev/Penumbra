using System;
using System.IO;
using Dalamud.Interface;
using ImGuiNET;
using ImGuiScene;
using Penumbra.UI.Custom;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private class ManageModsButton : IDisposable
    {
        // magic numbers
        private const string MenuButtonLabel = "Manage Mods";

        private readonly SettingsInterface                    _base;
        private readonly TextureWrap                          _icon;
        private readonly TitleScreenMenu.TitleScreenMenuEntry _entry;

        public ManageModsButton( SettingsInterface ui )
        {
            _base  = ui;

            _icon = Dalamud.PluginInterface.UiBuilder.LoadImage( Path.Combine( Dalamud.PluginInterface.AssemblyLocation.DirectoryName!,
                "tsmLogo.png" ) );
            if( _icon == null )
                throw new Exception( "Could not load title screen icon." );

            _entry = Dalamud.TitleScreenMenu.AddEntry( MenuButtonLabel, _icon, OnTriggered );
        }

        private void OnTriggered()
        {
            _base.FlipVisibility();
        }

        public void Dispose()
        {
            _icon.Dispose();
            Dalamud.TitleScreenMenu.RemoveEntry( _entry );
        }
    }
}