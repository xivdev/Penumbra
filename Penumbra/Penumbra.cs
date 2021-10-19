using Dalamud.Game.Command;
using Dalamud.Logging;
using Dalamud.Plugin;
using EmbedIO;
using EmbedIO.WebApi;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Penumbra.Api;
using Penumbra.GameData.Enums;
using Penumbra.Interop;
using Penumbra.Meta.Files;
using Penumbra.Mods;
using Penumbra.PlayerWatch;
using Penumbra.UI;
using Penumbra.Util;

namespace Penumbra
{
    public class Penumbra : IDalamudPlugin
    {
        public string Name { get; } = "Penumbra";
        public string PluginDebugTitleStr { get; } = "Penumbra - Debug Build";

        private const string CommandName = "/penumbra";

        public static Configuration Config { get; private set; } = null!;
        public static IPlayerWatcher PlayerWatcher { get; private set; } = null!;

        public ResourceLoader ResourceLoader { get; }
        public SettingsInterface SettingsInterface { get; }
        public MusicManager MusicManager { get; }
        public ObjectReloader ObjectReloader { get; }

        public PenumbraApi Api { get; }
        public PenumbraIpc Ipc { get; }

        private WebServer? _webServer;

        public Penumbra( DalamudPluginInterface pluginInterface )
        {
            FFXIVClientStructs.Resolver.Initialize();
            Dalamud.Initialize( pluginInterface );
            GameData.GameData.GetIdentifier( Dalamud.GameData, Dalamud.ClientState.ClientLanguage );
            Config = Configuration.Load();

            MusicManager = new MusicManager();
            MusicManager.DisableStreaming();

            var gameUtils = Service< ResidentResources >.Set();
            PlayerWatcher = PlayerWatchFactory.Create( Dalamud.Framework, Dalamud.ClientState, Dalamud.Objects );
            Service< MetaDefaults >.Set();
            var modManager = Service< ModManager >.Set();

            modManager.DiscoverMods();

            ObjectReloader = new ObjectReloader( modManager, Config.WaitFrames );

            ResourceLoader = new ResourceLoader( this );

            Dalamud.Commands.AddHandler( CommandName, new CommandInfo( OnCommand )
            {
                HelpMessage = "/penumbra - toggle ui\n/penumbra reload - reload mod file lists & discover any new mods",
            } );

            ResourceLoader.Init();
            ResourceLoader.Enable();

            gameUtils.ReloadPlayerResources();

            SettingsInterface = new SettingsInterface( this );

            if( Config.EnableHttpApi )
            {
                CreateWebServer();
            }

            if( !Config.EnablePlayerWatch || !Config.IsEnabled )
            {
                PlayerWatcher.Disable();
            }

            PlayerWatcher.PlayerChanged += p =>
            {
                PluginLog.Debug( "Triggered Redraw of {Player}.", p.Name );
                ObjectReloader.RedrawObject( p, RedrawType.OnlyWithSettings );
            };

            Api = new PenumbraApi( this );
            SubscribeItemLinks();
            Ipc = new PenumbraIpc( pluginInterface, Api );
        }

        public bool Enable()
        {
            if( Config.IsEnabled )
            {
                return false;
            }

            Config.IsEnabled = true;
            Service< ResidentResources >.Get().ReloadPlayerResources();
            if( Config.EnablePlayerWatch )
            {
                PlayerWatcher.SetStatus( true );
            }

            Config.Save();
            ObjectReloader.RedrawAll( RedrawType.WithSettings );
            return true;
        }

        public bool Disable()
        {
            if( !Config.IsEnabled )
            {
                return false;
            }

            Config.IsEnabled = false;
            Service< ResidentResources >.Get().ReloadPlayerResources();
            if( Config.EnablePlayerWatch )
            {
                PlayerWatcher.SetStatus( false );
            }

            Config.Save();
            ObjectReloader.RedrawAll( RedrawType.WithoutSettings );
            return true;
        }

        public bool SetEnabled( bool enabled )
            => enabled ? Enable() : Disable();

        private void SubscribeItemLinks()
        {
            Api.ChangedItemTooltip += it =>
            {
                if( it is Item )
                {
                    ImGui.Text( "Left Click to create an item link in chat." );
                }
            };
            Api.ChangedItemClicked += ( button, it ) =>
            {
                if( button == MouseButton.Left && it is Item item )
                {
                    ChatUtil.LinkItem( item );
                }
            };
        }

        public void CreateWebServer()
        {
            const string prefix = "http://localhost:42069/";

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
            Ipc.Dispose();
            Api.Dispose();
            SettingsInterface.Dispose();
            ObjectReloader.Dispose();
            PlayerWatcher.Dispose();

            Dalamud.Commands.RemoveHandler( CommandName );

            ResourceLoader.Dispose();

            ShutdownWebServer();
        }

        private void OnCommand( string command, string rawArgs )
        {
            const string modsEnabled  = "Your mods have now been enabled.";
            const string modsDisabled = "Your mods have now been disabled.";

            var args = rawArgs.Split( new[] { ' ' }, 2 );
            if( args.Length > 0 && args[ 0 ].Length > 0 )
            {
                switch( args[ 0 ] )
                {
                    case "reload":
                    {
                        Service< ModManager >.Get().DiscoverMods();
                        Dalamud.Chat.Print(
                            $"Reloaded Penumbra mods. You have {Service< ModManager >.Get()?.Mods.Count} mods."
                        );
                        break;
                    }
                    case "redraw":
                    {
                        if( args.Length > 1 )
                        {
                            ObjectReloader.RedrawObject( args[ 1 ] );
                        }
                        else
                        {
                            ObjectReloader.RedrawAll();
                        }

                        break;
                    }
                    case "debug":
                    {
                        SettingsInterface.MakeDebugTabVisible();
                        break;
                    }
                    case "enable":
                    {
                        Dalamud.Chat.Print( Enable()
                            ? "Your mods are already enabled. To disable your mods, please run the following command instead: /penumbra disable"
                            : modsEnabled );
                        break;
                    }
                    case "disable":
                    {
                        Dalamud.Chat.Print( Disable()
                            ? "Your mods are already disabled. To enable your mods, please run the following command instead: /penumbra enable"
                            : modsDisabled );
                        break;
                    }
                    case "toggle":
                    {
                        SetEnabled( !Config.IsEnabled );
                        Dalamud.Chat.Print( Config.IsEnabled
                            ? modsEnabled
                            : modsDisabled );
                        break;
                    }
                }

                return;
            }

            SettingsInterface.FlipVisibility();
        }
    }
}