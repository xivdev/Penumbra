using Dalamud.Plugin;
using OtterGui.Services;

namespace Penumbra.Services;

public class DalamudConfigService : IService
{
    public DalamudConfigService()
    {
        try
        {
            var serviceType =
                typeof(IDalamudPluginInterface).Assembly.DefinedTypes.FirstOrDefault(t => t.Name == "Service`1" && t.IsGenericType);
            var configType    = typeof(IDalamudPluginInterface).Assembly.DefinedTypes.FirstOrDefault(t => t.Name == "DalamudConfiguration");
            var interfaceType = typeof(IDalamudPluginInterface).Assembly.DefinedTypes.FirstOrDefault(t => t.Name == "DalamudInterface");
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
