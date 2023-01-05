using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using ImGuiNET;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.Interop;
using Penumbra.Mods;
using Penumbra.UI;

namespace Penumbra;

public static class SeStringBuilderExtensions
{
    public const ushort Green  = 504;
    public const ushort Yellow = 31;
    public const ushort Red    = 534;
    public const ushort Blue   = 517;
    public const ushort White  = 1;
    public const ushort Purple = 541;

    public static SeStringBuilder AddText( this SeStringBuilder sb, string text, int color, bool brackets = false )
        => sb.AddUiForeground( ( ushort )color ).AddText( brackets ? $"[{text}]" : text ).AddUiForegroundOff();

    public static SeStringBuilder AddGreen( this SeStringBuilder sb, string text, bool brackets = false )
        => AddText( sb, text, Green, brackets );

    public static SeStringBuilder AddYellow( this SeStringBuilder sb, string text, bool brackets = false )
        => AddText( sb, text, Yellow, brackets );

    public static SeStringBuilder AddRed( this SeStringBuilder sb, string text, bool brackets = false )
        => AddText( sb, text, Red, brackets );

    public static SeStringBuilder AddBlue( this SeStringBuilder sb, string text, bool brackets = false )
        => AddText( sb, text, Blue, brackets );

    public static SeStringBuilder AddWhite( this SeStringBuilder sb, string text, bool brackets = false )
        => AddText( sb, text, White, brackets );

    public static SeStringBuilder AddPurple( this SeStringBuilder sb, string text, bool brackets = false )
        => AddText( sb, text, Purple, brackets );

    public static SeStringBuilder AddCommand( this SeStringBuilder sb, string command, string description )
        => sb.AddText( "    》 " )
           .AddBlue( command )
           .AddText( $" - {description}" );

    public static SeStringBuilder AddInitialPurple( this SeStringBuilder sb, string word, bool withComma = true )
        => sb.AddPurple( $"[{word[ 0 ]}]" )
           .AddText( withComma ? $"{word[ 1.. ]}, " : word[ 1.. ] );
}

public class CommandHandler : IDisposable
{
    private const string CommandName = "/penumbra";

    private readonly CommandManager        _commandManager;
    private readonly ObjectReloader        _objectReloader;
    private readonly Configuration         _config;
    private readonly Penumbra              _penumbra;
    private readonly ConfigWindow          _configWindow;
    private readonly ActorManager          _actors;
    private readonly Mod.Manager           _modManager;
    private readonly ModCollection.Manager _collectionManager;

    public CommandHandler( CommandManager commandManager, ObjectReloader objectReloader, Configuration config, Penumbra penumbra, ConfigWindow configWindow, Mod.Manager modManager,
        ModCollection.Manager collectionManager, ActorManager actors )
    {
        _commandManager    = commandManager;
        _objectReloader    = objectReloader;
        _config            = config;
        _penumbra          = penumbra;
        _configWindow      = configWindow;
        _modManager        = modManager;
        _collectionManager = collectionManager;
        _actors            = actors;
        _commandManager.AddHandler( CommandName, new CommandInfo( OnCommand )
        {
            HelpMessage = "Without arguments, toggles the main window. Use /penumbra help to get further command help.",
            ShowInHelp  = true,
        } );
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler( CommandName );
    }

    private void OnCommand( string command, string arguments )
    {
        if( arguments.Length == 0 )
        {
            arguments = "window";
        }

        var argumentList = arguments.Split( ' ', 2 );
        arguments = argumentList.Length == 2 ? argumentList[ 1 ] : string.Empty;

        var _ = argumentList[ 0 ].ToLowerInvariant() switch
        {
            "window"     => ToggleWindow( arguments ),
            "enable"     => SetPenumbraState( arguments, true ),
            "disable"    => SetPenumbraState( arguments, false ),
            "toggle"     => SetPenumbraState( arguments, null ),
            "reload"     => Reload( arguments ),
            "redraw"     => Redraw( arguments ),
            "lockui"     => SetUiLockState( arguments ),
            "debug"      => SetDebug( arguments ),
            "collection" => SetCollection( arguments ),
            "mod"        => SetMod( arguments ),
            "bulktag"    => SetTag( arguments ),
            _            => PrintHelp( argumentList[ 0 ] ),
        };
    }

    private static bool PrintHelp( string arguments )
    {
        if( !string.Equals( arguments, "help", StringComparison.OrdinalIgnoreCase ) && arguments == "?" )
        {
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "The given argument " ).AddRed( arguments, true ).AddText( " is not valid. Valid arguments are:" ).BuiltString );
        }
        else
        {
            Dalamud.Chat.Print( "Valid arguments for /penumbra are:" );
        }

        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "window",
            "Toggle the Penumbra main config window. Can be used with [on|off] to force specific state. Also used when no argument is provided." ).BuiltString );
        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "enable", "Enable modding and force a redraw of all game objects if it was previously disabled." ).BuiltString );
        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "disable", "Disable modding and force a redraw of all game objects if it was previously enabled." ).BuiltString );
        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "toggle", "Toggle modding and force a redraw of all game objects." ).BuiltString );
        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "reload", "Rediscover the mod directory and reload all mods." ).BuiltString );
        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "redraw", "Redraw all game objects. Specify a placeholder or a name to redraw specific objects." ).BuiltString );
        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "lockui", "Toggle the locked state of the main Penumbra window. Can be used with [on|off] to force specific state." )
           .BuiltString );
        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "debug", "Toggle debug mode for Penumbra. Can be used with [on|off] to force specific state." ).BuiltString );
        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "collection", "Change your active collection setup. Use without further parameters for more detailed help." )
           .BuiltString );
        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "mod", "Change a specific mods settings. Use without further parameters for more detailed help." ).BuiltString );
        Dalamud.Chat.Print( new SeStringBuilder().AddCommand( "tag", "Change multiple mods settings based on their tags. Use without further parameters for more detailed help." )
           .BuiltString );
        return true;
    }

    private bool ToggleWindow( string arguments )
    {
        var value = ParseTrueFalseToggle( arguments ) ?? !_configWindow.IsOpen;
        if( value == _configWindow.IsOpen )
        {
            return false;
        }

        _configWindow.Toggle();
        return true;
    }

    private bool Reload( string _ )
    {
        _modManager.DiscoverMods();
        Dalamud.Chat.Print( $"Reloaded Penumbra mods. You have {_modManager.Count} mods." );
        return true;
    }

    private bool Redraw( string arguments )
    {
        if( arguments.Length > 0 )
        {
            _objectReloader.RedrawObject( arguments, RedrawType.Redraw );
        }
        else
        {
            _objectReloader.RedrawAll( RedrawType.Redraw );
        }

        return true;
    }

    private bool SetDebug( string arguments )
    {
        var value = ParseTrueFalseToggle( arguments ) ?? !_config.DebugMode;
        if( value == _config.DebugMode )
        {
            return false;
        }

        Dalamud.Chat.Print( value
            ? "Debug mode enabled."
            : "Debug mode disabled." );

        _config.DebugMode = value;
        _config.Save();
        return true;
    }

    private bool SetPenumbraState( string _, bool? newValue )
    {
        var value = newValue ?? !_config.EnableMods;

        if( value == _config.EnableMods )
        {
            Dalamud.Chat.Print( value
                ? "Your mods are already enabled. To disable your mods, please run the following command instead: /penumbra disable"
                : "Your mods are already disabled. To enable your mods, please run the following command instead: /penumbra enable" );
            return false;
        }

        Dalamud.Chat.Print( value
            ? "Your mods have been enabled."
            : "Your mods have been disabled." );
        return _penumbra.SetEnabled( value );
    }

    private bool SetUiLockState( string arguments )
    {
        var value = ParseTrueFalseToggle( arguments ) ?? !_config.FixMainWindow;
        if( value == _config.FixMainWindow )
        {
            return false;
        }

        if( value )
        {
            Dalamud.Chat.Print( "Penumbra UI locked in place." );
            _configWindow.Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        }
        else
        {
            Dalamud.Chat.Print( "Penumbra UI unlocked." );
            _configWindow.Flags &= ~( ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize );
        }

        _config.FixMainWindow = value;
        _config.Save();
        return true;
    }

    private bool SetCollection( string arguments )
    {
        if( arguments.Length == 0 )
        {
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "Use with /penumbra collection " ).AddBlue( "[Collection Type]" ).AddText( " | " ).AddYellow( "[Collection Name]" )
               .AddText( " | " ).AddGreen( "<Identifier>" ).BuiltString );
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "    》 Valid Collection Types are " ).AddBlue( "Base" ).AddText( ", " ).AddBlue( "Ui" ).AddText( ", " )
               .AddBlue( "Selected" ).AddText( ", " )
               .AddBlue( "Individual" ).AddText( ", and all those selectable in Character Groups." ).BuiltString );
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "    》 Valid Collection Names are " ).AddYellow( "None" )
               .AddText( ", all collections you have created by their full names, and " ).AddYellow( "Delete" ).AddText( " to remove assignments (not valid for all types)." )
               .BuiltString );
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "    》 If the type is " ).AddBlue( "Individual" )
               .AddText( " you need to specify an individual with an identifier of the form:" ).BuiltString );
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "    》》》 " ).AddGreen( "<me>" ).AddText( " or " ).AddGreen( "<t>" ).AddText( " or " ).AddGreen( "<mo>" )
               .AddText( " or " ).AddGreen( "<f>"  ).AddText( " as placeholders for your character, your target, your mouseover or your focus, if they exist." ).BuiltString );
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "    》》》 " ).AddGreen( "p" ).AddText( " | " ).AddWhite( "[Player Name]@<World Name>" )
               .AddText( ", if no @ is provided, Any World is used." ).BuiltString );
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "    》》》 " ).AddGreen( "r" ).AddText( " | " ).AddWhite( "[Retainer Name]" ).BuiltString );
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "    》》》 " ).AddGreen( "n" ).AddText( " | " ).AddPurple( "[NPC Type]" ).AddText( " : " )
               .AddRed( "[NPC Name]" ).AddText( ", where NPC Type can be " ).AddInitialPurple( "Mount" ).AddInitialPurple( "Companion" ).AddInitialPurple( "Accessory" )
               .AddInitialPurple( "Event NPC" ).AddText( "or " )
               .AddInitialPurple( "Battle NPC", false ).AddText( "." ).BuiltString );
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "    》》》 " ).AddGreen( "o" ).AddText( " | " ).AddPurple( "[NPC Type]" ).AddText( " : " )
               .AddRed( "[NPC Name]" ).AddText( " | " ).AddWhite( "[Player Name]@<World Name>" ).AddText( "." ).BuiltString );
            return true;
        }

        var split    = arguments.Split( '|', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
        var typeName = split[ 0 ];

        if( !CollectionTypeExtensions.TryParse( typeName, out var type ) )
        {
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "The argument " ).AddRed( typeName, true ).AddText( " is not a valid collection type." ).BuiltString );
            return false;
        }

        if( split.Length == 1 )
        {
            Dalamud.Chat.Print( "There was no collection name provided." );
            return false;
        }

        if( !GetModCollection( split[ 1 ], out var collection ) )
        {
            return false;
        }

        var identifier = ActorIdentifier.Invalid;
        if( type is CollectionType.Individual )
        {
            if( split.Length == 2 )
            {
                Dalamud.Chat.Print( "Setting an individual collection requires a collection name and an identifier, but no identifier was provided." );
                return false;
            }

            try
            {
                if( ObjectReloader.GetName( split[ 2 ].ToLowerInvariant(), out var obj ) )
                {
                    identifier = _actors.FromObject( obj, false, true );
                    if( !identifier.IsValid )
                    {
                        Dalamud.Chat.Print( new SeStringBuilder().AddText( "The placeholder " ).AddGreen( split[ 2 ] )
                           .AddText( " did not resolve to a game object with a valid identifier." ).BuiltString );
                        return false;
                    }
                }
                else
                {
                    identifier = _actors.FromUserString( split[ 2 ] );
                }
            }
            catch( ActorManager.IdentifierParseError e )
            {
                Dalamud.Chat.Print( new SeStringBuilder().AddText( "The argument " ).AddRed( split[ 2 ], true ).AddText( $" could not be converted to an identifier. {e.Message}" )
                   .BuiltString );
                return false;
            }
        }

        var oldCollection = _collectionManager.ByType( type, identifier );
        if( collection == oldCollection )
        {
            Dalamud.Chat.Print( collection == null
                ? $"The {type.ToName()} Collection{( identifier.IsValid ? $" for {identifier}" : string.Empty )} is already unassigned"
                : $"{collection.Name} already is the {type.ToName()} Collection{( identifier.IsValid ? $" for {identifier}." : "." )}" );
            return false;
        }

        var individualIndex = _collectionManager.Individuals.Index( identifier );

        if( oldCollection == null )
        {
            if( type.IsSpecial() )
            {
                _collectionManager.CreateSpecialCollection( type );
            }
            else if( identifier.IsValid )
            {
                var identifiers = _collectionManager.Individuals.GetGroup( identifier );
                individualIndex = _collectionManager.Individuals.Count;
                _collectionManager.CreateIndividualCollection( identifiers );
            }
        }
        else if( collection == null )
        {
            if( type.IsSpecial() )
            {
                _collectionManager.RemoveSpecialCollection( type );
            }
            else if( individualIndex >= 0 )
            {
                _collectionManager.RemoveIndividualCollection( individualIndex );
            }
            else
            {
                Dalamud.Chat.Print( $"Can not remove the {type.ToName()} Collection assignment {( identifier.IsValid ? $" for {identifier}." : "." )}" );
                return false;
            }

            Dalamud.Chat.Print( $"Removed {oldCollection.Name} as {type.ToName()} Collection assignment {( identifier.IsValid ? $" for {identifier}." : "." )}" );
            return true;
        }

        _collectionManager.SetCollection( collection!, type, individualIndex );
        Dalamud.Chat.Print( $"Assigned {collection!.Name} as {type.ToName()} Collection{( identifier.IsValid ? $" for {identifier}." : "." )}" );
        return true;
    }

    private bool SetMod( string arguments )
    {
        if( arguments.Length == 0 )
        {
            var seString = new SeStringBuilder()
               .AddText( "Use with /penumbra mod " ).AddBlue( "[enable|disable|inherit|toggle]" ).AddText( "  " ).AddYellow( "[Collection Name]" ).AddText( " | " )
               .AddPurple( "[Mod Name or Mod Directory Name]" );
            Dalamud.Chat.Print( seString.BuiltString );
            return true;
        }

        var split     = arguments.Split( ' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries );
        var nameSplit = split.Length != 2 ? Array.Empty< string >() : split[ 1 ].Split( '|', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries );
        if( nameSplit.Length != 2 )
        {
            Dalamud.Chat.Print( "Not enough arguments provided." );
            return false;
        }

        var state = ConvertToSettingState( split[ 0 ] );
        if( state == -1 )
        {
            Dalamud.Chat.Print( new SeStringBuilder().AddRed( split[ 0 ], true ).AddText( " is not a valid type of setting." ).BuiltString );
            return false;
        }

        if( !GetModCollection( nameSplit[ 0 ], out var collection ) || collection == ModCollection.Empty )
        {
            return false;
        }

        if( !_modManager.TryGetMod( nameSplit[ 1 ], nameSplit[ 1 ], out var mod ) )
        {
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "The mod " ).AddRed( nameSplit[ 1 ], true ).AddText( " does not exist." ).BuiltString );
            return false;
        }

        if( HandleModState( state, collection!, mod ) )
        {
            return true;
        }

        Dalamud.Chat.Print( new SeStringBuilder().AddText( "Mod " ).AddPurple( mod.Name, true ).AddText( "already had the desired state in collection " )
           .AddYellow( collection!.Name, true ).AddText( "." ).BuiltString );
        return false;
    }

    private bool SetTag( string arguments )
    {
        if( arguments.Length == 0 )
        {
            var seString = new SeStringBuilder()
               .AddText( "Use with /penumbra tag " ).AddBlue( "[enable|disable|toggle|inherit]" ).AddText( "  " ).AddYellow( "[Collection Name]" ).AddText( " | " )
               .AddPurple( "[Local Tag]" );
            Dalamud.Chat.Print( seString.BuiltString );
            return true;
        }

        var split     = arguments.Split( ' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries );
        var nameSplit = split.Length != 2 ? Array.Empty< string >() : split[ 1 ].Split( '|', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries );
        if( nameSplit.Length != 2 )
        {
            Dalamud.Chat.Print( "Not enough arguments provided." );
            return false;
        }

        var state = ConvertToSettingState( split[ 0 ] );

        if( state == -1 )
        {
            Dalamud.Chat.Print( new SeStringBuilder().AddRed( split[ 0 ], true ).AddText( " is not a valid type of setting." ).BuiltString );
            return false;
        }

        if( !GetModCollection( nameSplit[ 0 ], out var collection ) || collection == ModCollection.Empty )
        {
            return false;
        }

        var mods = _modManager.Where( m => m.LocalTags.Contains( nameSplit[ 1 ], StringComparer.OrdinalIgnoreCase ) ).ToList();

        if( mods.Count == 0 )
        {
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "The tag " ).AddRed( nameSplit[ 1 ], true ).AddText( " does not match any mods." ).BuiltString );
            return false;
        }

        var changes = false;
        foreach( var mod in mods )
        {
            changes |= HandleModState( state, collection!, mod );
        }

        if( !changes )
        {
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "No mod states were changed in collection " ).AddYellow( collection!.Name, true ).AddText( "." ).BuiltString );
        }

        return true;
    }

    private bool GetModCollection( string collectionName, out ModCollection? collection )
    {
        var lowerName = collectionName.ToLowerInvariant();
        if( lowerName == "delete" )
        {
            collection = null;
            return true;
        }

        collection = string.Equals( lowerName, ModCollection.Empty.Name, StringComparison.OrdinalIgnoreCase )
            ? ModCollection.Empty
            : _collectionManager[ lowerName ];
        if( collection == null )
        {
            Dalamud.Chat.Print( new SeStringBuilder().AddText( "The collection " ).AddRed( collectionName, true ).AddText( " does not exist." ).BuiltString );
            return false;
        }

        return true;
    }

    private static bool? ParseTrueFalseToggle( string value )
        => value.ToLowerInvariant() switch
        {
            "0"        => false,
            "false"    => false,
            "off"      => false,
            "disable"  => false,
            "disabled" => false,

            "1"       => true,
            "true"    => true,
            "on"      => true,
            "enable"  => true,
            "enabled" => true,

            _ => null,
        };

    private static int ConvertToSettingState( string text )
        => text.ToLowerInvariant() switch
        {
            "enable"    => 0,
            "enabled"   => 0,
            "disable"   => 1,
            "disabled"  => 1,
            "toggle"    => 2,
            "inherit"   => 3,
            "inherited" => 3,
            _           => -1,
        };

    private static bool HandleModState( int settingState, ModCollection collection, Mod mod )
    {
        var settings = collection!.Settings[ mod.Index ];
        switch( settingState )
        {
            case 0:
                if( collection.SetModState( mod.Index, true ) )
                {
                    Dalamud.Chat.Print( new SeStringBuilder().AddText( "Enabled mod " ).AddPurple( mod.Name, true ).AddText( " in collection " )
                       .AddYellow( collection.Name, true )
                       .AddText( "." ).BuiltString );
                    return true;
                }

                return false;
            case 1:
                if( collection.SetModState( mod.Index, false ) )
                {
                    Dalamud.Chat.Print( new SeStringBuilder().AddText( "Disabled mod " ).AddPurple( mod.Name, true ).AddText( " in collection " )
                       .AddYellow( collection.Name, true )
                       .AddText( "." ).BuiltString );
                    return true;
                }

                return false;
            case 2:
                var setting = !( settings?.Enabled ?? false );
                if( collection.SetModState( mod.Index, setting ) )
                {
                    Dalamud.Chat.Print( new SeStringBuilder().AddText( setting ? "Enabled mod " : "Disabled mod " ).AddPurple( mod.Name, true ).AddText( " in collection " )
                       .AddYellow( collection.Name, true )
                       .AddText( "." ).BuiltString );
                    return true;
                }

                return false;
            case 3:
                if( collection.SetModInheritance( mod.Index, true ) )
                {
                    Dalamud.Chat.Print( new SeStringBuilder().AddText( "Set mod " ).AddPurple( mod.Name, true ).AddText( " in collection " ).AddYellow( collection.Name, true )
                       .AddText( " to inherit." ).BuiltString );
                    return true;
                }

                return false;
        }

        return false;
    }
}