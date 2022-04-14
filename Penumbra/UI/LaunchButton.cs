using System;
using System.IO;
using Dalamud.Interface;
using ImGuiScene;

namespace Penumbra.UI;

public class LaunchButton : IDisposable
{
    private readonly ConfigWindow                          _configWindow;
    private readonly TextureWrap?                          _icon;
    private readonly TitleScreenMenu.TitleScreenMenuEntry? _entry;

    public LaunchButton( ConfigWindow ui )
    {
        _configWindow = ui;

        _icon = Dalamud.PluginInterface.UiBuilder.LoadImage( Path.Combine( Dalamud.PluginInterface.AssemblyLocation.DirectoryName!,
            "tsmLogo.png" ) );
        if( _icon != null )
        {
            _entry = Dalamud.TitleScreenMenu.AddEntry( "Manage Penumbra", _icon, OnTriggered );
        }
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