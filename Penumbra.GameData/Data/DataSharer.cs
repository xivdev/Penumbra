using System;
using Dalamud;
using Dalamud.Logging;
using Dalamud.Plugin;

namespace Penumbra.GameData.Data;

public abstract class DataSharer : IDisposable
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly int _version;
    protected readonly ClientLanguage Language;
    private bool _disposed;

    protected DataSharer(DalamudPluginInterface pluginInterface, ClientLanguage language, int version)
    {
        _pluginInterface = pluginInterface;
        Language = language;
        _version = version;
    }

    protected virtual void DisposeInternal()
    { }

    public void Dispose()
    {
        if (_disposed)
            return;

        DisposeInternal();
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    ~DataSharer()
        => Dispose();

    protected void DisposeTag(string tag)
        => _pluginInterface.RelinquishData(GetVersionedTag(tag));

    private string GetVersionedTag(string tag)
        => $"Penumbra.GameData.{tag}.{Language}.V{_version}";

    protected T TryCatchData<T>(string tag, Func<T> func) where T : class
    {
        try
        {
            return _pluginInterface.GetOrCreateData(GetVersionedTag(tag), func);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error creating shared actor data for {tag}:\n{ex}");
            return func();
        }
    }
}
