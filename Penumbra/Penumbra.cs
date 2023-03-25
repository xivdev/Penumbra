using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Log;
using OtterGui.Widgets;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Interop;
using Penumbra.UI;
using Penumbra.Util;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Resolver;
using Penumbra.Mods;
using CharacterUtility = Penumbra.Interop.CharacterUtility;
using DalamudUtil = Dalamud.Utility.Util;
using ResidentResourceManager = Penumbra.Interop.ResidentResourceManager;

namespace Penumbra;

public class Penumbra : IDalamudPlugin
{
    public string Name
        => "Penumbra";

    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;

    public static readonly string CommitHash =
        Assembly.GetExecutingAssembly().GetCustomAttribute< AssemblyInformationalVersionAttribute >()?.InformationalVersion ?? "Unknown";

    public static Logger Log { get; private set; } = null!;
    public static Configuration Config { get; private set; } = null!;

    public static ResidentResourceManager ResidentResources { get; private set; } = null!;
    public static CharacterUtility CharacterUtility { get; private set; } = null!;
    public static GameEventManager GameEvents { get; private set; } = null!;
    public static MetaFileManager MetaFileManager { get; private set; } = null!;
    public static Mod.Manager ModManager { get; private set; } = null!;
    public static ModCollection.Manager CollectionManager { get; private set; } = null!;
    public static TempModManager TempMods { get; private set; } = null!;
    public static ResourceLoader ResourceLoader { get; private set; } = null!;
    public static FrameworkManager Framework { get; private set; } = null!;
    public static ActorManager Actors { get; private set; } = null!;
    public static IObjectIdentifier Identifier { get; private set; } = null!;
    public static IGamePathParser GamePathParser { get; private set; } = null!;
    public static StainManager StainManager { get; private set; } = null!;

    public static ValidityChecker ValidityChecker { get; private set; } = null!;

    public static PerformanceTracker< PerformanceType > Performance { get; private set; } = null!;

    public static readonly StartTimeTracker< StartTimeType > StartTimer = new();

    public readonly ResourceLogger       ResourceLogger;
    public readonly PathResolver         PathResolver;
    public readonly ObjectReloader       ObjectReloader;
    public readonly ModFileSystem        ModFileSystem;
    public readonly PenumbraApi          Api;
    public readonly HttpApi              HttpApi;
    public readonly PenumbraIpcProviders IpcProviders;
    internal ConfigWindow? ConfigWindow { get; private set; }
    private          LaunchButton?   _launchButton;
    private          WindowSystem?   _windowSystem;
    private          Changelog?      _changelog;
    private          CommandHandler? _commandHandler;
    private readonly ResourceWatcher _resourceWatcher;
    private          bool            _disposed;

    public static ItemData ItemData { get; private set; } = null!;

    public Penumbra( DalamudPluginInterface pluginInterface )
    {
        using var time = StartTimer.Measure( StartTimeType.Total );

        try
        {
            Dalamud.Initialize( pluginInterface );

            Performance     = new PerformanceTracker< PerformanceType >( Dalamud.Framework );
            Log             = new Logger();
            ValidityChecker = new ValidityChecker( Dalamud.PluginInterface );

            GameEvents = new GameEventManager();
            StartTimer.Measure( StartTimeType.Identifier, () => Identifier         = GameData.GameData.GetIdentifier( Dalamud.PluginInterface, Dalamud.GameData ) );
            StartTimer.Measure( StartTimeType.GamePathParser, () => GamePathParser = GameData.GameData.GetGamePathParser() );
            StartTimer.Measure( StartTimeType.Stains, () => StainManager           = new StainManager( Dalamud.PluginInterface, Dalamud.GameData ) );
            ItemData = StartTimer.Measure( StartTimeType.Items, () => new ItemData( Dalamud.PluginInterface, Dalamud.GameData, Dalamud.GameData.Language ) );
            StartTimer.Measure( StartTimeType.Actors,
                () => Actors = new ActorManager( Dalamud.PluginInterface, Dalamud.Objects, Dalamud.ClientState, Dalamud.Framework, Dalamud.GameData, Dalamud.GameGui,
                    ResolveCutscene ) );

            Framework        = new FrameworkManager( Dalamud.Framework, Log );
            CharacterUtility = new CharacterUtility();

            StartTimer.Measure( StartTimeType.Backup, () => Backup.CreateBackup( pluginInterface.ConfigDirectory, PenumbraBackupFiles() ) );
            Config = Configuration.Load();

            TempMods        = new TempModManager();
            MetaFileManager = new MetaFileManager();
            ResourceLoader  = new ResourceLoader( this );
            ResourceLoader.EnableHooks();
            _resourceWatcher  = new ResourceWatcher( ResourceLoader );
            ResourceLogger    = new ResourceLogger( ResourceLoader );
            ResidentResources = new ResidentResourceManager();
            StartTimer.Measure( StartTimeType.Mods, () =>
            {
                ModManager = new Mod.Manager( Config.ModDirectory );
                ModManager.DiscoverMods();
            } );

            StartTimer.Measure( StartTimeType.Collections, () =>
            {
                CollectionManager = new ModCollection.Manager( ModManager );
                CollectionManager.CreateNecessaryCaches();
            } );

            ModFileSystem  = ModFileSystem.Load();
            ObjectReloader = new ObjectReloader();
            PathResolver   = new PathResolver( ResourceLoader );

            SetupInterface();

            if( Config.EnableMods )
            {
                ResourceLoader.EnableReplacements();
                PathResolver.Enable();
            }

            if( Config.DebugMode )
            {
                ResourceLoader.EnableDebug();
            }

            using( var tApi = StartTimer.Measure( StartTimeType.Api ) )
            {
                Api          = new PenumbraApi( this );
                IpcProviders = new PenumbraIpcProviders( Dalamud.PluginInterface, Api );
                HttpApi      = new HttpApi( Api );
                if( Config.EnableHttpApi )
                {
                    HttpApi.CreateWebServer();
                }

                SubscribeItemLinks();
            }

            ValidityChecker.LogExceptions();
            Log.Information( $"Penumbra Version {Version}, Commit #{CommitHash} successfully Loaded from {pluginInterface.SourceRepository}." );
            OtterTex.NativeDll.Initialize( Dalamud.PluginInterface.AssemblyLocation.DirectoryName );
            Log.Information( $"Loading native OtterTex assembly from {OtterTex.NativeDll.Directory}." );

            if( CharacterUtility.Ready )
            {
                ResidentResources.Reload();
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void SetupInterface()
    {
        Task.Run( () =>
            {
                using var tInterface = StartTimer.Measure( StartTimeType.Interface );
                var       changelog  = ConfigWindow.CreateChangelog();
                var cfg = new ConfigWindow( this, _resourceWatcher )
                {
                    IsOpen = Config.DebugMode,
                };
                var btn    = new LaunchButton( cfg );
                var system = new WindowSystem( Name );
                var cmd    = new CommandHandler( Dalamud.Framework, Dalamud.Commands, ObjectReloader, Config, this, cfg, ModManager, CollectionManager, Actors );
                system.AddWindow( cfg );
                system.AddWindow( cfg.ModEditPopup );
                system.AddWindow( changelog );
                if( !_disposed )
                {
                    _changelog                                     =  changelog;
                    ConfigWindow                                   =  cfg;
                    _windowSystem                                  =  system;
                    _launchButton                                  =  btn;
                    _commandHandler                                =  cmd;
                    Dalamud.PluginInterface.UiBuilder.OpenConfigUi += cfg.Toggle;
                    Dalamud.PluginInterface.UiBuilder.Draw         += _windowSystem.Draw;
                }
                else
                {
                    cfg.Dispose();
                    btn.Dispose();
                    cmd.Dispose();
                }
            }
        );
    }

    private void DisposeInterface()
    {
        if( _windowSystem != null )
        {
            Dalamud.PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        }

        _launchButton?.Dispose();
        if( ConfigWindow != null )
        {
            Dalamud.PluginInterface.UiBuilder.OpenConfigUi -= ConfigWindow.Toggle;
            ConfigWindow.Dispose();
        }
    }

    public event Action< bool >? EnabledChange;

    public bool SetEnabled( bool enabled )
    {
        if( enabled == Config.EnableMods )
        {
            return false;
        }

        Config.EnableMods = enabled;
        if( enabled )
        {
            ResourceLoader.EnableReplacements();
            PathResolver.Enable();
            if( CharacterUtility.Ready )
            {
                CollectionManager.Default.SetFiles();
                ResidentResources.Reload();
                ObjectReloader.RedrawAll( RedrawType.Redraw );
            }
        }
        else
        {
            ResourceLoader.DisableReplacements();
            PathResolver.Disable();
            if( CharacterUtility.Ready )
            {
                CharacterUtility.ResetAll();
                ResidentResources.Reload();
                ObjectReloader.RedrawAll( RedrawType.Redraw );
            }
        }

        Config.Save();
        EnabledChange?.Invoke( enabled );

        return true;
    }

    public void ForceChangelogOpen()
    {
        if( _changelog != null )
        {
            _changelog.ForceOpen = true;
        }
    }

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

    private short ResolveCutscene( ushort index )
        => ( short )PathResolver.CutsceneActor( index );

    public void Dispose()
    {
        if( _disposed )
        {
            return;
        }

        _disposed = true;
        HttpApi?.Dispose();
        IpcProviders?.Dispose();
        Api?.Dispose();
        _commandHandler?.Dispose();
        StainManager?.Dispose();
        ItemData?.Dispose();
        Actors?.Dispose();
        Identifier?.Dispose();
        Framework?.Dispose();
        DisposeInterface();
        ObjectReloader?.Dispose();
        ModFileSystem?.Dispose();
        CollectionManager?.Dispose();
        PathResolver?.Dispose();
        ResourceLogger?.Dispose();
        _resourceWatcher?.Dispose();
        ResourceLoader?.Dispose();
        GameEvents?.Dispose();
        CharacterUtility?.Dispose();
        Performance?.Dispose();
    }

    // Collect all relevant files for penumbra configuration.
    private static IReadOnlyList< FileInfo > PenumbraBackupFiles()
    {
        var collectionDir = ModCollection.CollectionDirectory;
        var list = Directory.Exists( collectionDir )
            ? new DirectoryInfo( collectionDir ).EnumerateFiles( "*.json" ).ToList()
            : new List< FileInfo >();
        list.AddRange( Mod.LocalDataDirectory.Exists ? Mod.LocalDataDirectory.EnumerateFiles( "*.json" ) : Enumerable.Empty< FileInfo >() );
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
        sb.Append( $"> **`Plugin Version:              `** {Version}\n" );
        sb.Append( $"> **`Commit Hash:                 `** {CommitHash}\n" );
        sb.Append( $"> **`Enable Mods:                 `** {Config.EnableMods}\n" );
        sb.Append( $"> **`Enable HTTP API:             `** {Config.EnableHttpApi}\n" );
        sb.Append( $"> **`Operating System:            `** {( DalamudUtil.IsLinux() ? "Mac/Linux (Wine)" : "Windows" )}\n" );
        sb.Append( $"> **`Root Directory:              `** `{Config.ModDirectory}`, {( exists ? "Exists" : "Not Existing" )}\n" );
        sb.Append( $"> **`Free Drive Space:            `** {( drive != null ? Functions.HumanReadableSize( drive.AvailableFreeSpace ) : "Unknown" )}\n" );
        sb.Append( $"> **`Auto-Deduplication:          `** {Config.AutoDeduplicateOnImport}\n" );
        sb.Append( $"> **`Debug Mode:                  `** {Config.DebugMode}\n" );
        sb.Append( $"> **`Synchronous Load (Dalamud):  `** {( Dalamud.GetDalamudConfig( Dalamud.WaitingForPluginsOption, out bool v ) ? v.ToString() : "Unknown" )}\n" );
        sb.Append( $"> **`Logging:                     `** Log: {Config.EnableResourceLogging}, Watcher: {Config.EnableResourceWatcher} ({Config.MaxResourceWatcherRecords})\n" );
        sb.Append( $"> **`Use Ownership:               `** {Config.UseOwnerNameForCharacterCollection}\n" );
        sb.AppendLine( "**Mods**" );
        sb.Append( $"> **`Installed Mods:              `** {ModManager.Count}\n" );
        sb.Append( $"> **`Mods with Config:            `** {ModManager.Count( m => m.HasOptions )}\n" );
        sb.Append( $"> **`Mods with File Redirections: `** {ModManager.Count( m => m.TotalFileCount     > 0 )}, Total: {ModManager.Sum( m => m.TotalFileCount )}\n" );
        sb.Append( $"> **`Mods with FileSwaps:         `** {ModManager.Count( m => m.TotalSwapCount     > 0 )}, Total: {ModManager.Sum( m => m.TotalSwapCount )}\n" );
        sb.Append( $"> **`Mods with Meta Manipulations:`** {ModManager.Count( m => m.TotalManipulations > 0 )}, Total {ModManager.Sum( m => m.TotalManipulations )}\n" );
        sb.Append( $"> **`IMC Exceptions Thrown:       `** {ValidityChecker.ImcExceptions.Count}\n" );
        sb.Append( $"> **`#Temp Mods:                  `** {TempMods.Mods.Sum( kvp => kvp.Value.Count ) + TempMods.ModsForAllCollections.Count}\n" );

        string CharacterName( ActorIdentifier id, string name )
        {
            if( id.Type is IdentifierType.Player or IdentifierType.Owned )
            {
                var parts = name.Split( ' ', 3 );
                return string.Join( " ", parts.Length != 3 ? parts.Select( n => $"{n[ 0 ]}." ) : parts[ ..2 ].Select( n => $"{n[ 0 ]}." ).Append( parts[ 2 ] ) );
            }

            return name + ':';
        }

        void PrintCollection( ModCollection c )
            => sb.Append( $"**Collection {c.AnonymizedName}**\n"
              + $"> **`Inheritances:                 `** {c.Inheritance.Count}\n"
              + $"> **`Enabled Mods:                 `** {c.ActualSettings.Count( s => s is { Enabled: true } )}\n"
              + $"> **`Conflicts (Solved/Total):     `** {c.AllConflicts.SelectMany( x => x ).Sum( x => x.HasPriority ? 0 : x.Conflicts.Count )}/{c.AllConflicts.SelectMany( x => x ).Sum( x => x.HasPriority || !x.Solved ? 0 : x.Conflicts.Count )}\n" );

        sb.AppendLine( "**Collections**" );
        sb.Append( $"> **`#Collections:                 `** {CollectionManager.Count - 1}\n" );
        sb.Append( $"> **`#Temp Collections:            `** {TempMods.CustomCollections.Count}\n" );
        sb.Append( $"> **`Active Collections:           `** {CollectionManager.Count( c => c.HasCache )}\n" );
        sb.Append( $"> **`Base Collection:              `** {CollectionManager.Default.AnonymizedName}\n" );
        sb.Append( $"> **`Interface Collection:         `** {CollectionManager.Interface.AnonymizedName}\n" );
        sb.Append( $"> **`Selected Collection:          `** {CollectionManager.Current.AnonymizedName}\n" );
        foreach( var (type, name, _) in CollectionTypeExtensions.Special )
        {
            var collection = CollectionManager.ByType( type );
            if( collection != null )
            {
                sb.Append( $"> **`{name,-30}`** {collection.AnonymizedName}\n" );
            }
        }

        foreach( var (name, id, collection) in CollectionManager.Individuals.Assignments )
        {
            sb.Append( $"> **`{CharacterName( id[ 0 ], name ),-30}`** {collection.AnonymizedName}\n" );
        }

        foreach( var collection in CollectionManager.Where( c => c.HasCache ) )
        {
            PrintCollection( collection );
        }

        return sb.ToString();
    }
}