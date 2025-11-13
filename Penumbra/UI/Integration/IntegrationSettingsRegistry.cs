using Dalamud.Plugin;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Api.Enums;

namespace Penumbra.UI.Integration;

public sealed class IntegrationSettingsRegistry : IService, IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    
    private readonly List<(string InternalName, string Name, Action Draw)> _sections = [];

    private bool _disposed = false;

    public IntegrationSettingsRegistry(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        
        _pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
    }

    public void Dispose()
    {
        _disposed = true;
        
        _pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
        
        _sections.Clear();
    }

    public void Draw()
    {
        foreach (var (internalName, name, draw) in _sections)
        {
            if (!ImUtf8.CollapsingHeader($"Integration with {name}###IntegrationSettingsHeader.{internalName}"))
                continue;

            using var id = ImUtf8.PushId($"IntegrationSettings.{internalName}");
            try
            {
                draw();
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Error while drawing {internalName} integration settings: {e}");
            }
        }
    }

    public PenumbraApiEc RegisterSection(Action draw)
    {
        if (_disposed)
            return PenumbraApiEc.SystemDisposed;
        
        var plugin = GetPlugin(draw);
        if (plugin is null)
            return PenumbraApiEc.InvalidArgument;

        var section = (plugin.InternalName, plugin.Name, draw);
        
        var index = FindSectionIndex(plugin.InternalName);
        if (index >= 0)
        {
            if (_sections[index] == section)
                return PenumbraApiEc.NothingChanged;
            _sections[index] = section;
        }
        else
            _sections.Add(section);
        _sections.Sort((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, StringComparison.CurrentCultureIgnoreCase));

        return PenumbraApiEc.Success;
    }

    public bool UnregisterSection(Action draw)
    {
        var index = FindSectionIndex(draw);
        if (index < 0)
            return false;
        
        _sections.RemoveAt(index);
        return true;
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args)
    {
        if (args.Kind is PluginListInvalidationKind.Loaded)
            return;

        foreach (var internalName in args.AffectedInternalNames)
        {
            var index = FindSectionIndex(internalName);
            if (index >= 0 && GetPlugin(_sections[index].Draw) is null)
            {
                _sections.RemoveAt(index);
                Penumbra.Log.Warning($"Removed stale integration setting section of {internalName} (reason: {args.Kind})");
            }
        }
    }

    private IExposedPlugin? GetPlugin(Delegate @delegate)
        => @delegate.Method.DeclaringType
            switch
            {
                null     => null,
                var type => _pluginInterface.GetPlugin(type.Assembly),
            };

    private int FindSectionIndex(string internalName)
        => _sections.FindIndex(section => section.InternalName.Equals(internalName, StringComparison.Ordinal));

    private int FindSectionIndex(Action draw)
        => _sections.FindIndex(section => section.Draw == draw);
}
