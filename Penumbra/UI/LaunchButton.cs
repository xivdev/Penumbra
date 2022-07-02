using System;
using System.IO;
using Dalamud.Interface;
using ImGuiScene;

namespace Penumbra.UI;

// A Launch Button used in the title screen of the game,
// using the Dalamud-provided collapsible submenu.
public class LaunchButton : IDisposable
{
    private readonly ConfigWindow                          _configWindow;
    private          TextureWrap?                          _icon;
    private          TitleScreenMenu.TitleScreenMenuEntry? _entry;

    public LaunchButton( ConfigWindow ui )
    {
        _configWindow = ui;
        _icon         = null;
        _entry        = null;

        void CreateEntry()
        {
            _icon = Dalamud.PluginInterface.UiBuilder.LoadImage( Path.Combine( Dalamud.PluginInterface.AssemblyLocation.DirectoryName!,
                "tsmLogo.png" ) );
            if( _icon != null )
            {
                _entry = Dalamud.TitleScreenMenu.AddEntry( "Manage Penumbra", _icon, OnTriggered );
            }

            Dalamud.PluginInterface.UiBuilder.Draw -= CreateEntry;
        }

        Dalamud.PluginInterface.UiBuilder.Draw += CreateEntry;
    }

    private void OnTriggered()
        => _configWindow.Toggle();

    public void Dispose()
    {
        _icon?.Dispose();
        if( _entry != null )
        {
            Dalamud.TitleScreenMenu.RemoveEntry( _entry );
        }
    }
}