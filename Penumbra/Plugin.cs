using System.Linq;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using EmbedIO;
using EmbedIO.WebApi;
using Penumbra.API;
using Penumbra.Game;
using Penumbra.Interop;
using Penumbra.Meta.Files;
using Penumbra.Mods;
using Penumbra.UI;
using Penumbra.Util;

namespace Penumbra
{
    public class Plugin : IDalamudPlugin
    {
        public string Name { get; }
        public string PluginDebugTitleStr { get; }

        public Plugin()
        {
            Name                = "Penumbra";
            PluginDebugTitleStr = $"{Name} - Debug Build";
        }

        private const string CommandName = "/penumbra";

        public DalamudPluginInterface PluginInterface { get; set; } = null!;
        public Configuration Configuration { get; set; } = null!;
        public ResourceLoader ResourceLoader { get; set; } = null!;
        public SettingsInterface SettingsInterface { get; set; } = null!;
        public MusicManager SoundShit { get; set; } = null!;
        public ActorRefresher ActorRefresher { get; set; } = null!;
        public PlayerWatcher PlayerWatcher { get; set; } = null!;

        private WebServer? _webServer;

        public void Initialize( DalamudPluginInterface pluginInterface )
        {
            PluginInterface = pluginInterface;
            Service< DalamudPluginInterface >.Set( PluginInterface );

            Configuration = Configuration.Load( PluginInterface );

            SoundShit = new MusicManager( this );
            SoundShit.DisableStreaming();

            var gameUtils = Service< GameResourceManagement >.Set( PluginInterface );
            Service< MetaDefaults >.Set( PluginInterface );
            var modManager = Service< ModManager >.Set( this );

            modManager.DiscoverMods();

            ActorRefresher = new ActorRefresher( PluginInterface, modManager );
            PlayerWatcher  = new PlayerWatcher( PluginInterface );

            ResourceLoader = new ResourceLoader( this );

            PluginInterface.CommandManager.AddHandler( CommandName, new CommandInfo( OnCommand )
            {
                HelpMessage = "/penumbra - toggle ui\n/penumbra reload - reload mod file lists & discover any new mods",
            } );

            ResourceLoader.Init();
            ResourceLoader.Enable();

            gameUtils.ReloadPlayerResources();

            SettingsInterface = new SettingsInterface( this );

            PluginInterface.UiBuilder.OnBuildUi += SettingsInterface.Draw;

            if( Configuration.EnableHttpApi )
            {
                CreateWebServer();
            }

            if( Configuration.EnableActorWatch )
            {
                PlayerWatcher.EnableActorWatch();
            }
        }

        public void CreateWebServer()
        {
            var prefix = "http://localhost:42069/";

            ShutdownWebServer();

            _webServer = new WebServer( o => o
                   .WithUrlPrefix( prefix )
                   .WithMode( HttpListenerMode.EmbedIO ) )
               .WithCors( prefix )
               .WithWebApi( "/api", m => m
                   .WithController( () => new ModsController( this ) ) );

            _webServer.StateChanged += ( s, e ) => PluginLog.Information( $"WebServer New State - {e.NewState}" );

            _webServer.RunAsync();
        }

        public void ShutdownWebServer()
        {
            _webServer?.Dispose();
            _webServer = null;
        }

        public void Dispose()
        {
            ActorRefresher.Dispose();
            PlayerWatcher.Dispose();
            PluginInterface.UiBuilder.OnBuildUi -= SettingsInterface.Draw;

            PluginInterface.CommandManager.RemoveHandler( CommandName );
            PluginInterface.Dispose();

            ResourceLoader.Dispose();

            ShutdownWebServer();
        }

        private void OnCommand( string command, string rawArgs )
        {
            var args = rawArgs.Split( new[] { ' ' }, 2 );
            if( args.Length > 0 && args[ 0 ].Length > 0 )
            {
                switch( args[ 0 ] )
                {
                    case "reload":
                    {
                        Service< ModManager >.Get().DiscoverMods();
                        PluginInterface.Framework.Gui.Chat.Print(
                            $"Reloaded Penumbra mods. You have {Service< ModManager >.Get()?.Mods.Count} mods."
                        );
                        break;
                    }
                    case "redraw":
                    {
                        if( args.Length > 1 )
                        {
                            ActorRefresher.RedrawActor( args[ 1 ] );
                        }
                        else
                        {
                            ActorRefresher.RedrawAll();
                        }

                        break;
                    }
                }

                return;
            }

            SettingsInterface.FlipVisibility();
        }
    }
}