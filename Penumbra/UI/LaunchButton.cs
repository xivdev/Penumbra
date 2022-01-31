using System;
using System.IO;
using Dalamud.Interface;
using ImGuiScene;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private class ManageModsButton : IDisposable
    {
        private readonly SettingsInterface                     _base;
        private readonly TextureWrap?                          _icon;
        private readonly TitleScreenMenu.TitleScreenMenuEntry? _entry;

        public ManageModsButton( SettingsInterface ui )
        {
            _base  = ui;

            _icon = Dalamud.PluginInterface.UiBuilder.LoadImage( Path.Combine( Dalamud.PluginInterface.AssemblyLocation.DirectoryName!,
                "tsmLogo.png" ) );
            if( _icon != null )
            {
                _entry = Dalamud.TitleScreenMenu.AddEntry( "Manage Penumbra", _icon, OnTriggered );
            }
        }

        private void OnTriggered()
        {
            _base.FlipVisibility();
        }

        public void Dispose()
        {
            _icon?.Dispose();
            if( _entry != null )
            {
                Dalamud.TitleScreenMenu.RemoveEntry( _entry );
            }
        }
    }
}