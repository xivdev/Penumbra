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
                HelpMessage = "/penumbra - toggle ui\n/penumbra reload - reload mod file lists & discover any new mods"
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

        private void OnCommand( string command, string rawArgs )
        {
            var args = rawArgs.Split( ' ' );
            if( args.Length > 0 && args[ 0 ].Length > 0 )
            {
                switch( args[ 0 ] )
                {
                    case "reload":
                    {
                        ModManager.DiscoverMods();
                        PluginInterface.Framework.Gui.Chat.Print(
                            $"Reloaded Penumbra mods. You have {ModManager.Mods.ModSettings.Count} mods, {ModManager.Mods.EnabledMods.Length} of which are enabled."
                        );
                        break;
                    }
                }

                return;
            }

            SettingsInterface.Visible = !SettingsInterface.Visible;
        }
    }
}