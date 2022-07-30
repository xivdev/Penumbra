using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using EmbedIO;
using EmbedIO.WebApi;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Classes;
using Penumbra.Api;
using Penumbra.GameData.Enums;
using Penumbra.Interop;
using Penumbra.UI;
using Penumbra.Util;
using Penumbra.Collections;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Resolver;
using Penumbra.Mods;
using CharacterUtility = Penumbra.Interop.CharacterUtility;
using ResidentResourceManager = Penumbra.Interop.ResidentResourceManager;

namespace Penumbra;

public class Penumbra : IDalamudPlugin
{
    public string Name
        => "Penumbra";

    private const string CommandName = "/penumbra";

    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;

    public static readonly string CommitHash =
        Assembly.GetExecutingAssembly().GetCustomAttribute< AssemblyInformationalVersionAttribute >()?.InformationalVersion ?? "Unknown";

    public static bool DevPenumbraExists;
    public static bool IsNotInstalledPenumbra;

    public static Configuration Config { get; private set; } = null!;

    public static ResidentResourceManager ResidentResources { get; private set; } = null!;
    public static CharacterUtility CharacterUtility { get; private set; } = null!;
    public static MetaFileManager MetaFileManager { get; private set; } = null!;
    public static Mod.Manager ModManager { get; private set; } = null!;
    public static ModCollection.Manager CollectionManager { get; private set; } = null!;
    public static TempModManager TempMods { get; private set; } = null!;
    public static ResourceLoader ResourceLoader { get; private set; } = null!;
    public static FrameworkManager Framework { get; private set; } = null!;
    public static int ImcExceptions = 0;

    public readonly  ResourceLogger ResourceLogger;
    public readonly  PathResolver   PathResolver;
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
        GameData.GameData.GetIdentifier( Dalamud.GameData, Dalamud.ClientState.ClientLanguage );
        DevPenumbraExists      = CheckDevPluginPenumbra();
        IsNotInstalledPenumbra = CheckIsNotInstalled();

        Framework        = new FrameworkManager();
        CharacterUtility = new CharacterUtility();
        Backup.CreateBackup( pluginInterface.ConfigDirectory, PenumbraBackupFiles() );
        Config = Configuration.Load();

        TempMods          = new TempModManager();
        MetaFileManager   = new MetaFileManager();
        ResourceLoader    = new ResourceLoader( this );
        ResourceLoader.EnableHooks();
        ResourceLogger    = new ResourceLogger( ResourceLoader );
        ResidentResources = new ResidentResourceManager();
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

        SetupInterface( out _configWindow, out _launchButton, out _windowSystem );

        if( Config.EnableMods )
        {
            ResourceLoader.EnableReplacements();
            PathResolver.Enable();
        }

        if( Config.EnableHttpApi )
        {
            CreateWebServer();
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

        if( CharacterUtility.Ready )
        {
            ResidentResources.Reload();
        }

        Api = new PenumbraApi( this );
        Ipc = new PenumbraIpc( Dalamud.PluginInterface, Api );
        SubscribeItemLinks();
        if( ImcExceptions > 0 )
        {
            PluginLog.Error( $"{ImcExceptions} IMC Exceptions thrown. Please repair your game files." );
        }
        else
        {
            PluginLog.Information( $"Penumbra Version {Version}, Commit #{CommitHash} successfully Loaded." );
        }
        Dalamud.PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
    }

    private void SetupInterface( out ConfigWindow cfg, out LaunchButton btn, out WindowSystem system )
    {
        cfg    = new ConfigWindow( this );
        btn    = new LaunchButton( _configWindow );
        system = new WindowSystem( Name );
        system.AddWindow( _configWindow );
        system.AddWindow( cfg.ModEditPopup );
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
        PathResolver.Enable();
        Config.Save();
        if( CharacterUtility.Ready )
        {
            CollectionManager.Default.SetFiles();
            ResidentResources.Reload();
            ObjectReloader.RedrawAll( RedrawType.Redraw );
        }

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
        PathResolver.Disable();
        Config.Save();
        if( CharacterUtility.Ready )
        {
            CharacterUtility.ResetAll();
            ResidentResources.Reload();
            ObjectReloader.RedrawAll( RedrawType.Redraw );
        }

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
        ResourceLoader.Dispose();
        CharacterUtility.Dispose();

        ShutdownWebServer();
    }

    public static bool SetCollection( string type, string collectionName )
    {
        type           = type.ToLowerInvariant();
        collectionName = collectionName.ToLowerInvariant();

        var collection = string.Equals( collectionName, ModCollection.Empty.Name, StringComparison.OrdinalIgnoreCase )
            ? ModCollection.Empty
            : CollectionManager[ collectionName ];
        if( collection == null )
        {
            Dalamud.Chat.Print( $"The collection {collection} does not exist." );
            return false;
        }

        foreach( var t in Enum.GetValues< CollectionType >() )
        {
            if( t is CollectionType.Inactive or CollectionType.Character
            || !string.Equals( t.ToString(), type, StringComparison.OrdinalIgnoreCase ) )
            {
                continue;
            }

            var oldCollection = CollectionManager.ByType( t );
            if( collection == oldCollection )
            {
                Dalamud.Chat.Print( $"{collection.Name} already is the {t.ToName()} Collection." );
                return false;
            }

            if( oldCollection == null && t.IsSpecial() )
            {
                CollectionManager.CreateSpecialCollection( t );
            }

            CollectionManager.SetCollection( collection, t, null );
            Dalamud.Chat.Print( $"Set {collection.Name} as {t.ToName()} Collection." );
            return true;
        }

        Dalamud.Chat.Print(
            "Second command argument is not default, the correct command format is: /penumbra collection <collectionType> <collectionName>" );
        return false;
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
                case "unfix":
                {
                    Config.FixMainWindow =  false;
                    _configWindow.Flags  &= ~( ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize );
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

    public static string GatherSupportInformation()
    {
        var sb     = new StringBuilder( 10240 );
        var exists = Config.ModDirectory.Length > 0 && Directory.Exists( Config.ModDirectory );
        var drive  = exists ? new DriveInfo( new DirectoryInfo( Config.ModDirectory ).Root.FullName ) : null;
        sb.AppendLine( "**Settings**" );
        sb.AppendFormat( "> **`Plugin Version:              `** {0}\n", Version );
        sb.AppendFormat( "> **`Commit Hash:                 `** {0}\n", CommitHash );
        sb.AppendFormat( "> **`Enable Mods:                 `** {0}\n", Config.EnableMods );
        sb.AppendFormat( "> **`Enable HTTP API:             `** {0}\n", Config.EnableHttpApi );
        sb.AppendFormat( "> **`Root Directory:              `** `{0}`, {1}\n", Config.ModDirectory, exists ? "Exists" : "Not Existing" );
        sb.AppendFormat( "> **`Free Drive Space:            `** {0}\n",
            drive != null ? Functions.HumanReadableSize( drive.AvailableFreeSpace ) : "Unknown" );
        sb.AppendLine( "**Mods**" );
        sb.AppendFormat( "> **`Installed Mods:              `** {0}\n", ModManager.Count );
        sb.AppendFormat( "> **`Mods with Config:            `** {0}\n", ModManager.Count( m => m.HasOptions ) );
        sb.AppendFormat( "> **`Mods with File Redirections: `** {0}, Total: {1}\n", ModManager.Count( m => m.TotalFileCount > 0 ),
            ModManager.Sum( m => m.TotalFileCount ) );
        sb.AppendFormat( "> **`Mods with FileSwaps:         `** {0}, Total: {1}\n", ModManager.Count( m => m.TotalSwapCount > 0 ),
            ModManager.Sum( m => m.TotalSwapCount ) );
        sb.AppendFormat( "> **`Mods with Meta Manipulations:`** {0}, Total {1}\n", ModManager.Count( m => m.TotalManipulations > 0 ),
            ModManager.Sum( m => m.TotalManipulations ) );
        sb.AppendFormat( "> **`IMC Exceptions Thrown:       `** {0}\n", ImcExceptions );

        string CharacterName( string name )
            => string.Join( " ", name.Split().Select( n => $"{n[ 0 ]}." ) ) + ':';

        void PrintCollection( ModCollection c )
            => sb.AppendFormat( "**Collection {0}**\n"
              + "> **`Inheritances:                `** {1}\n"
              + "> **`Enabled Mods:                `** {2}\n"
              + "> **`Total Conflicts:             `** {3}\n"
              + "> **`Solved Conflicts:            `** {4}\n",
                c.AnonymizedName, c.Inheritance.Count, c.ActualSettings.Count( s => s is { Enabled: true } ),
                c.AllConflicts.SelectMany( x => x ).Sum( x => x.HasPriority ? 0 : x.Conflicts.Count ),
                c.AllConflicts.SelectMany( x => x ).Sum( x => x.HasPriority || !x.Solved ? 0 : x.Conflicts.Count ) );

        sb.AppendLine( "**Collections**" );
        sb.AppendFormat( "> **`#Collections:                `** {0}\n", CollectionManager.Count - 1 );
        sb.AppendFormat( "> **`Active Collections:          `** {0}\n", CollectionManager.Count( c => c.HasCache ) );
        sb.AppendFormat( "> **`Default Collection:          `** {0}\n", CollectionManager.Default.AnonymizedName);
        sb.AppendFormat( "> **`Current Collection:          `** {0}\n", CollectionManager.Current.AnonymizedName);
        foreach( var type in CollectionTypeExtensions.Special )
        {
            var collection = CollectionManager.ByType( type );
            if( collection != null )
            {
                sb.AppendFormat( "> **`{0,-29}`** {1}\n", type.ToName(), collection.AnonymizedName );
            }
        }

        foreach( var (name, collection) in CollectionManager.Characters )
        {
            sb.AppendFormat( "> **`{1,-29}`** {0}\n", collection.AnonymizedName, CharacterName( name ) );
        }

        foreach( var collection in CollectionManager.Where( c => c.HasCache ) )
        {
            PrintCollection( collection );
        }

        return sb.ToString();
    }

    // Because remnants of penumbra in devPlugins cause issues, we check for them to warn users to remove them.
    private static bool CheckDevPluginPenumbra()
    {
#if !DEBUG
        var path = Path.Combine( Dalamud.PluginInterface.DalamudAssetDirectory.Parent?.FullName ?? "INVALIDPATH", "devPlugins", "Penumbra" );
        var dir = new DirectoryInfo( path );

        try
        {
            return dir.Exists && dir.EnumerateFiles( "*.dll", SearchOption.AllDirectories ).Any();
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not check for dev plugin Penumbra:\n{e}" );
            return true;
        }
#else
        return false;
#endif
    }

    // Check if the loaded version of penumbra itself is in devPlugins.
    private static bool CheckIsNotInstalled()
    {
#if !DEBUG
        var checkedDirectory = Dalamud.PluginInterface.AssemblyLocation.Directory?.Parent?.Parent?.Name;
        var ret = checkedDirectory?.Equals( "installedPlugins", StringComparison.OrdinalIgnoreCase ) ?? false;
        if (!ret)
            PluginLog.Error($"Penumbra is not correctly installed. Application loaded from \"{Dalamud.PluginInterface.AssemblyLocation.Directory!.FullName}\"."  );
        return !ret;
#else
        return false;
#endif
    }
}