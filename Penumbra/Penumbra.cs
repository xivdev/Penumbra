using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using EmbedIO;
using EmbedIO.WebApi;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Penumbra.Api;
using Penumbra.GameData.Enums;
using Penumbra.Interop;
using Penumbra.UI;
using Penumbra.Util;
using Penumbra.Collections;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Resolver;
using Penumbra.Mods;

namespace Penumbra;

public class Penumbra : IDalamudPlugin
{
    public string Name
        => "Penumbra";

    private const string CommandName = "/penumbra";

    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;

    public static readonly string CommitHash =
        Assembly.GetExecutingAssembly().GetCustomAttribute< AssemblyInformationalVersionAttribute >()?.InformationalVersion ?? "Unknown";

    public static Configuration Config { get; private set; } = null!;

    public static ResidentResourceManager ResidentResources { get; private set; } = null!;
    public static CharacterUtility CharacterUtility { get; private set; } = null!;
    public static MetaFileManager MetaFileManager { get; private set; } = null!;
    public static Mod.Manager ModManager { get; private set; } = null!;
    public static ModCollection.Manager CollectionManager { get; private set; } = null!;
    public static SimpleRedirectManager Redirects { get; private set; } = null!;
    public static ResourceLoader ResourceLoader { get; private set; } = null!;
    public static FrameworkManager Framework { get; private set; } = null!;


    public readonly  ResourceLogger ResourceLogger;
    public readonly  PathResolver   PathResolver;
    public readonly  MusicManager   MusicManager;
    public readonly  ObjectReloader ObjectReloader;
    public readonly  ModFileSystem  ModFileSystem;
    public readonly  PenumbraApi    Api;
    public readonly  PenumbraIpc    Ipc;
    private readonly ConfigWindow   _configWindow;
    private readonly LaunchButton   _launchButton;
    private readonly WindowSystem   _windowSystem;

    internal WebServer? WebServer;

    public Penumbra( DalamudPluginInterface pluginInterface )
    {
        Dalamud.Initialize( pluginInterface );
        Framework = new FrameworkManager();
        GameData.GameData.GetIdentifier( Dalamud.GameData, Dalamud.ClientState.ClientLanguage );
        Backup.CreateBackup( PenumbraBackupFiles() );
        Config = Configuration.Load();

        MusicManager = new MusicManager();
        if( Config.DisableSoundStreaming )
        {
            MusicManager.DisableStreaming();
        }

        ResidentResources = new ResidentResourceManager();
        CharacterUtility  = new CharacterUtility();
        Redirects         = new SimpleRedirectManager();
        MetaFileManager   = new MetaFileManager();
        ResourceLoader    = new ResourceLoader( this );
        ResourceLogger    = new ResourceLogger( ResourceLoader );
        ModManager        = new Mod.Manager( Config.ModDirectory );
        ModManager.DiscoverMods();
        CollectionManager = new ModCollection.Manager( ModManager );
        ModFileSystem     = ModFileSystem.Load();
        ObjectReloader    = new ObjectReloader();
        PathResolver      = new PathResolver( ResourceLoader );

        Dalamud.Commands.AddHandler( CommandName, new CommandInfo( OnCommand )
        {
            HelpMessage = "/penumbra - toggle ui\n/penumbra reload - reload mod file lists & discover any new mods",
        } );

        ResidentResources.Reload();

        Api = new PenumbraApi( this );
        Ipc = new PenumbraIpc( pluginInterface, Api );
        SubscribeItemLinks();
        SetupInterface( out _configWindow, out _launchButton, out _windowSystem );

        if( Config.EnableHttpApi )
        {
            CreateWebServer();
        }

        ResourceLoader.EnableHooks();
        if( Config.EnableMods )
        {
            ResourceLoader.EnableReplacements();
            PathResolver.Enable();
        }

        if( Config.DebugMode )
        {
            ResourceLoader.EnableDebug();
            _configWindow.IsOpen = true;
        }

        if( Config.EnableFullResourceLogging )
        {
            ResourceLoader.EnableFullLogging();
        }

        ResidentResources.Reload();
    }

    private void SetupInterface( out ConfigWindow cfg, out LaunchButton btn, out WindowSystem system )
    {
        cfg    = new ConfigWindow( this );
        btn    = new LaunchButton( _configWindow );
        system = new WindowSystem( Name );
        system.AddWindow( _configWindow );
        system.AddWindow( cfg.ModEditPopup );
        Dalamud.PluginInterface.UiBuilder.Draw         += system.Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi += cfg.Toggle;
    }

    private void DisposeInterface()
    {
        Dalamud.PluginInterface.UiBuilder.Draw         -= _windowSystem.Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi -= _configWindow.Toggle;
        _launchButton.Dispose();
        _configWindow.Dispose();
    }

    public bool Enable()
    {
        if( Config.EnableMods )
        {
            return false;
        }

        Config.EnableMods = true;
        ResourceLoader.EnableReplacements();
        CollectionManager.Default.SetFiles();
        ResidentResources.Reload();
        PathResolver.Enable();

        Config.Save();
        ObjectReloader.RedrawAll( RedrawType.Redraw );
        return true;
    }

    public bool Disable()
    {
        if( !Config.EnableMods )
        {
            return false;
        }

        Config.EnableMods = false;
        ResourceLoader.DisableReplacements();
        CharacterUtility.ResetAll();
        ResidentResources.Reload();
        PathResolver.Disable();

        Config.Save();
        ObjectReloader.RedrawAll( RedrawType.Redraw );
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
                ImGui.TextUnformatted( "Left Click to create an item link in chat." );
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

        WebServer = new WebServer( o => o
               .WithUrlPrefix( prefix )
               .WithMode( HttpListenerMode.EmbedIO ) )
           .WithCors( prefix )
           .WithWebApi( "/api", m => m
               .WithController( () => new ModsController( this ) )
               .WithController( () => new RedrawController( this ) ) );

        WebServer.StateChanged += ( _, e ) => PluginLog.Information( $"WebServer New State - {e.NewState}" );

        WebServer.RunAsync();
    }

    public void ShutdownWebServer()
    {
        WebServer?.Dispose();
        WebServer = null;
    }

    public void Dispose()
    {
        DisposeInterface();
        Ipc.Dispose();
        Api.Dispose();
        ObjectReloader.Dispose();
        ModFileSystem.Dispose();
        CollectionManager.Dispose();

        Dalamud.Commands.RemoveHandler( CommandName );

        PathResolver.Dispose();
        ResourceLogger.Dispose();
        MetaFileManager.Dispose();
        ResourceLoader.Dispose();
        CharacterUtility.Dispose();

        ShutdownWebServer();
    }

    public bool SetCollection( string type, string collectionName )
    {
        type           = type.ToLowerInvariant();
        collectionName = collectionName.ToLowerInvariant();

        var collection = string.Equals( collectionName, ModCollection.Empty.Name, StringComparison.InvariantCultureIgnoreCase )
            ? ModCollection.Empty
            : CollectionManager[ collectionName ];
        if( collection == null )
        {
            Dalamud.Chat.Print( $"The collection {collection} does not exist." );
            return false;
        }

        switch( type )
        {
            case "default":
                if( collection == CollectionManager.Default )
                {
                    Dalamud.Chat.Print( $"{collection.Name} already is the default collection." );
                    return false;
                }

                CollectionManager.SetCollection( collection, ModCollection.Type.Default );
                Dalamud.Chat.Print( $"Set {collection.Name} as default collection." );
                return true;
            default:
                Dalamud.Chat.Print(
                    "Second command argument is not default, the correct command format is: /penumbra collection default <collectionName>" );
                return false;
        }
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
                    ModManager.DiscoverMods();
                    Dalamud.Chat.Print( $"Reloaded Penumbra mods. You have {ModManager.Count} mods."
                    );
                    break;
                }
                case "redraw":
                {
                    if( args.Length > 1 )
                    {
                        ObjectReloader.RedrawObject( args[ 1 ], RedrawType.Redraw );
                    }
                    else
                    {
                        ObjectReloader.RedrawAll( RedrawType.Redraw );
                    }

                    break;
                }
                case "debug":
                {
                    Config.DebugMode = true;
                    Config.Save();
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
                    SetEnabled( !Config.EnableMods );
                    Dalamud.Chat.Print( Config.EnableMods
                        ? modsEnabled
                        : modsDisabled );
                    break;
                }
                case "collection":
                {
                    if( args.Length == 2 )
                    {
                        args = args[ 1 ].Split( new[] { ' ' }, 2 );
                        if( args.Length == 2 )
                        {
                            SetCollection( args[ 0 ], args[ 1 ] );
                        }
                    }
                    else
                    {
                        Dalamud.Chat.Print( "Missing arguments, the correct command format is:"
                          + " /penumbra collection {default} <collectionName>" );
                    }

                    break;
                }
            }

            return;
        }

        _configWindow.Toggle();
    }

    // Collect all relevant files for penumbra configuration.
    private static IReadOnlyList< FileInfo > PenumbraBackupFiles()
    {
        var collectionDir = ModCollection.CollectionDirectory;
        var list = Directory.Exists( collectionDir )
            ? new DirectoryInfo( collectionDir ).EnumerateFiles( "*.json" ).ToList()
            : new List< FileInfo >();
        list.Add( Dalamud.PluginInterface.ConfigFile );
        list.Add( new FileInfo( ModFileSystem.ModFileSystemFile ) );
        list.Add( new FileInfo( ModCollection.Manager.ActiveCollectionFile ) );
        return list;
    }
}