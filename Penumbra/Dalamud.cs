using System;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.Linq;
using System.Reflection;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace Penumbra;

public class Dalamud
{
    public static void Initialize( DalamudPluginInterface pluginInterface )
        => pluginInterface.Create< Dalamud >();

    // @formatter:off
    [PluginService][RequiredVersion("1.0")] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static CommandManager         Commands        { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static DataManager            GameData        { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static ClientState            ClientState     { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static ChatGui                Chat            { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static Framework              Framework       { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static Condition              Conditions      { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static TargetManager          Targets         { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static ObjectTable            Objects         { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static TitleScreenMenu        TitleScreenMenu { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static GameGui                GameGui         { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static KeyState               KeyState        { get; private set; } = null!;
    // @formatter:on

    private static readonly object?     DalamudConfig;
    private static readonly MethodInfo? InterfaceGetter;
    private static readonly MethodInfo? SaveDalamudConfig;
    public const            string      WaitingForPluginsOption = "IsResumeGameAfterPluginLoad";

    static Dalamud()
    {
        try
        {
            var serviceType   = typeof( DalamudPluginInterface ).Assembly.DefinedTypes.FirstOrDefault( t => t.Name == "Service`1" && t.IsGenericType );
            var configType    = typeof( DalamudPluginInterface ).Assembly.DefinedTypes.FirstOrDefault( t => t.Name == "DalamudConfiguration" );
            var interfaceType = typeof( DalamudPluginInterface ).Assembly.DefinedTypes.FirstOrDefault( t => t.Name == "DalamudInterface" );
            if( serviceType == null || configType == null || interfaceType == null )
            {
                return;
            }

            var configService    = serviceType.MakeGenericType( configType );
            var interfaceService = serviceType.MakeGenericType( interfaceType );
            var configGetter     = configService.GetMethod( "Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic );
            InterfaceGetter = interfaceService.GetMethod( "Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic );
            if( configGetter == null || InterfaceGetter == null )
            {
                return;
            }

            DalamudConfig = configGetter.Invoke( null, null );
            if( DalamudConfig != null )
            {
                SaveDalamudConfig = DalamudConfig.GetType().GetMethod( "Save", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
                if( SaveDalamudConfig == null )
                {
                    DalamudConfig   = null;
                    InterfaceGetter = null;
                }
            }
        }
        catch
        {
            DalamudConfig     = null;
            SaveDalamudConfig = null;
            InterfaceGetter   = null;
        }
    }

    public static bool GetDalamudConfig< T >( string fieldName, out T? value )
    {
        value = default;
        try
        {
            if( DalamudConfig == null )
            {
                return false;
            }

            var getter = DalamudConfig.GetType().GetProperty( fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
            if( getter == null )
            {
                return false;
            }

            var result = getter.GetValue( DalamudConfig );
            if( result is not T v )
            {
                return false;
            }

            value = v;
            return true;
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Error while fetching Dalamud Config {fieldName}:\n{e}" );
            return false;
        }
    }

    public static bool SetDalamudConfig< T >( string fieldName, in T? value, string? windowFieldName = null )
    {
        try
        {
            if( DalamudConfig == null )
            {
                return false;
            }

            var getter = DalamudConfig.GetType().GetProperty( fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
            if( getter == null )
            {
                return false;
            }

            getter.SetValue( DalamudConfig, value );
            if( windowFieldName != null )
            {
                var inter          = InterfaceGetter!.Invoke( null, null );
                var settingsWindow = inter?.GetType().GetField( "settingsWindow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )?.GetValue( inter );
                settingsWindow?.GetType().GetField( windowFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )?.SetValue( settingsWindow, value );
            }

            SaveDalamudConfig!.Invoke( DalamudConfig, null );
            return true;
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Error while fetching Dalamud Config {fieldName}:\n{e}" );
            return false;
        }
    }
}