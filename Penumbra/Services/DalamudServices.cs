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
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace Penumbra.Services;

public class DalamudServices
{
    public DalamudServices(DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Inject(this);
        try
        {
            var serviceType =
                typeof(DalamudPluginInterface).Assembly.DefinedTypes.FirstOrDefault(t => t.Name == "Service`1" && t.IsGenericType);
            var configType    = typeof(DalamudPluginInterface).Assembly.DefinedTypes.FirstOrDefault(t => t.Name == "DalamudConfiguration");
            var interfaceType = typeof(DalamudPluginInterface).Assembly.DefinedTypes.FirstOrDefault(t => t.Name == "DalamudInterface");
            if (serviceType == null || configType == null || interfaceType == null)
                return;

            var configService    = serviceType.MakeGenericType(configType);
            var interfaceService = serviceType.MakeGenericType(interfaceType);
            var configGetter     = configService.GetMethod("Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            _interfaceGetter = interfaceService.GetMethod("Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (configGetter == null || _interfaceGetter == null)
                return;

            _dalamudConfig = configGetter.Invoke(null, null);
            if (_dalamudConfig != null)
            {
                _saveDalamudConfig = _dalamudConfig.GetType()
                    .GetMethod("Save", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (_saveDalamudConfig == null)
                {
                    _dalamudConfig   = null;
                    _interfaceGetter = null;
                }
            }
        }
        catch
        {
            _dalamudConfig     = null;
            _saveDalamudConfig = null;
            _interfaceGetter   = null;
        }
    }

    public void AddServices(IServiceCollection services)
    {
        services.AddSingleton(PluginInterface);
        services.AddSingleton(Commands);
        services.AddSingleton(SGameData);
        services.AddSingleton(SClientState);
        services.AddSingleton(Chat);
        services.AddSingleton(Framework);
        services.AddSingleton(Conditions);
        services.AddSingleton(Targets);
        services.AddSingleton(SObjects);
        services.AddSingleton(TitleScreenMenu);
        services.AddSingleton(GameGui);
        services.AddSingleton(KeyState);
        services.AddSingleton(SigScanner);
        services.AddSingleton(this);
    }

    // TODO remove static
    // @formatter:off
    [PluginService][RequiredVersion("1.0")] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static CommandManager Commands { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static DataManager SGameData { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static ClientState SClientState { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static ChatGui Chat { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static Framework Framework { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static Condition Conditions { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static TargetManager Targets { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static ObjectTable SObjects { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static TitleScreenMenu TitleScreenMenu { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static GameGui GameGui { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static KeyState KeyState { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static SigScanner SigScanner { get; private set; } = null!;
    // @formatter:on

    public UiBuilder UiBuilder
        => PluginInterface.UiBuilder;

    public ObjectTable Objects
        => SObjects;

    public ClientState ClientState
        => SClientState;

    public DataManager GameData
        => SGameData;

    public const string WaitingForPluginsOption = "IsResumeGameAfterPluginLoad";

    private readonly object?     _dalamudConfig;
    private readonly MethodInfo? _interfaceGetter;
    private readonly MethodInfo? _saveDalamudConfig;

    public bool GetDalamudConfig<T>(string fieldName, out T? value)
    {
        value = default;
        try
        {
            if (_dalamudConfig == null)
                return false;

            var getter = _dalamudConfig.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getter == null)
                return false;

            var result = getter.GetValue(_dalamudConfig);
            if (result is not T v)
                return false;

            value = v;
            return true;
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Error while fetching Dalamud Config {fieldName}:\n{e}");
            return false;
        }
    }

    public bool SetDalamudConfig<T>(string fieldName, in T? value, string? windowFieldName = null)
    {
        try
        {
            if (_dalamudConfig == null)
                return false;

            var getter = _dalamudConfig.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getter == null)
                return false;

            getter.SetValue(_dalamudConfig, value);
            if (windowFieldName != null)
            {
                var inter = _interfaceGetter!.Invoke(null, null);
                var settingsWindow = inter?.GetType()
                    .GetField("settingsWindow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(inter);
                settingsWindow?.GetType().GetField(windowFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.SetValue(settingsWindow, value);
            }

            _saveDalamudConfig!.Invoke(_dalamudConfig, null);
            return true;
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Error while fetching Dalamud Config {fieldName}:\n{e}");
            return false;
        }
    }
}
