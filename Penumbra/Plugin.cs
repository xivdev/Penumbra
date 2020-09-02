using System.IO;
using Dalamud.Game.Command;
using Dalamud.Plugin;

namespace Penumbra
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Penumbra";

        private const string CommandName = "/penumbra";

        public DalamudPluginInterface PluginInterface { get; set; }
        public Configuration Configuration { get; set; }

        public Penumbra Penumbra { get; set; }

        public ModManager ModManager { get; set; }

        public SettingsInterface SettingsInterface { get; set; }

        public void Initialize( DalamudPluginInterface pluginInterface )
        {
            PluginInterface = pluginInterface;

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize( PluginInterface );

            SettingsInterface = new SettingsInterface( this );
            PluginInterface.UiBuilder.OnBuildUi += SettingsInterface.Draw;

            ModManager = new ModManager( new DirectoryInfo( Configuration.BaseFolder ) );
            ModManager.DiscoverMods();

            Penumbra = new Penumbra( this );


            PluginInterface.CommandManager.AddHandler( CommandName, new CommandInfo( OnCommand )
            {
                HelpMessage = "/penumbra 0 will disable penumbra, /penumbra 1 will enable it."
            } );

            Penumbra.Init();
            Penumbra.Enable();
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.OnBuildUi -= SettingsInterface.Draw;

            PluginInterface.CommandManager.RemoveHandler( CommandName );
            PluginInterface.Dispose();

            Penumbra.Dispose();
        }

        private void OnCommand( string command, string args )
        {
            if( args.Length > 0 )
                Configuration.IsEnabled = args[ 0 ] == '1';

            if( Configuration.IsEnabled )
                Penumbra.Enable();
            else
                Penumbra.Disable();
        }
    }
}