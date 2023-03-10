using System;
using System.IO;
using Dalamud.Interface;
using ImGuiScene;
using Penumbra.Services;

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
            _icon = DalamudServices.PluginInterface.UiBuilder.LoadImage( Path.Combine( DalamudServices.PluginInterface.AssemblyLocation.DirectoryName!,
                "tsmLogo.png" ) );
            if( _icon != null )
            {
                _entry = DalamudServices.TitleScreenMenu.AddEntry( "Manage Penumbra", _icon, OnTriggered );
            }

            DalamudServices.PluginInterface.UiBuilder.Draw -= CreateEntry;
        }

        DalamudServices.PluginInterface.UiBuilder.Draw += CreateEntry;
    }

    private void OnTriggered()
        => _configWindow.Toggle();

    public void Dispose()
    {
        _icon?.Dispose();
        if( _entry != null )
        {
            DalamudServices.TitleScreenMenu.RemoveEntry( _entry );
        }
    }
}