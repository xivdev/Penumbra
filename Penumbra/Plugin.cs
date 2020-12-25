using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Penumbra.Extensions;
using Penumbra.Mods;
using Penumbra.UI;

namespace Penumbra
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Penumbra";

        private const string CommandName = "/penumbra";

        public DalamudPluginInterface PluginInterface { get; set; }
        public Configuration Configuration { get; set; }

        public ResourceLoader ResourceLoader { get; set; }

        public ModManager ModManager { get; set; }

        public SettingsInterface SettingsInterface { get; set; }

        public string PluginDebugTitleStr { get; private set; }

        public void Initialize( DalamudPluginInterface pluginInterface )
        {
            PluginInterface = pluginInterface;

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize( PluginInterface );

            ModManager = new ModManager();
            ModManager.DiscoverMods( Configuration.CurrentCollection );

            ResourceLoader = new ResourceLoader( this );


            PluginInterface.CommandManager.AddHandler( CommandName, new CommandInfo( OnCommand )
            {
                HelpMessage = "/penumbra 0 will disable penumbra, /penumbra 1 will enable it."
            } );

            ResourceLoader.Init();
            ResourceLoader.Enable();

            SettingsInterface = new SettingsInterface( this );
            PluginInterface.UiBuilder.OnBuildUi += SettingsInterface.Draw;

            PluginDebugTitleStr = $"{Name} - Debug Build";
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.OnBuildUi -= SettingsInterface.Draw;

            PluginInterface.CommandManager.RemoveHandler( CommandName );
            PluginInterface.Dispose();

            ResourceLoader.Dispose();
        }

        private void OnCommand( string command, string args )
        {
            if( args.Length > 0 )
                Configuration.IsEnabled = args[ 0 ] == '1';

            if( Configuration.IsEnabled )
                ResourceLoader.Enable();
            else
                ResourceLoader.Disable();
        }
    }
}