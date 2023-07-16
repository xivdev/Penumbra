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
using Dalamud.Interface.DragDrop;
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
        services.AddSingleton(GameData);
        services.AddSingleton(ClientState);
        services.AddSingleton(Chat);
        services.AddSingleton(Framework);
        services.AddSingleton(Conditions);
        services.AddSingleton(Targets);
        services.AddSingleton(Objects);
        services.AddSingleton(TitleScreenMenu);
        services.AddSingleton(GameGui);
        services.AddSingleton(KeyState);
        services.AddSingleton(SigScanner);
        services.AddSingleton(this);
        services.AddSingleton(UiBuilder);
        services.AddSingleton(DragDropManager);
    }

    // TODO remove static
    // @formatter:off
    [PluginService][RequiredVersion("1.0")] public DalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public CommandManager         Commands        { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public DataManager            GameData        { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public ClientState            ClientState     { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public ChatGui                Chat            { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public Framework              Framework       { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public Condition              Conditions      { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public TargetManager          Targets         { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public ObjectTable            Objects         { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public TitleScreenMenu        TitleScreenMenu { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public GameGui                GameGui         { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public KeyState               KeyState        { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public SigScanner             SigScanner      { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public IDragDropManager       DragDropManager { get; private set; } = null!;
    // @formatter:on

    public UiBuilder UiBuilder
        => PluginInterface.UiBuilder;

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
