using System;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Penumbra.Api.Helpers;

public sealed class ActionProvider<T1> : IDisposable
{
    private ICallGateProvider<T1, object?>? _provider;

    public ActionProvider( DalamudPluginInterface pi, string label, Action<T1> action )
    {
        try
        {
            _provider = pi.GetIpcProvider<T1, object?>( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }

        _provider?.RegisterAction( action );
    }

    public void Dispose()
    {
        _provider?.UnregisterAction();
        _provider = null;
        GC.SuppressFinalize( this );
    }

    ~ActionProvider()
        => Dispose();
}

public sealed class ActionProvider< T1, T2 > : IDisposable
{
    private          ICallGateProvider< T1, T2, object? >? _provider;

    public ActionProvider( DalamudPluginInterface pi, string label, Action< T1, T2 > action )
    {
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, object? >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }

        _provider?.RegisterAction( action );
    }

    public void Dispose()
    {
        _provider?.UnregisterAction();
        _provider = null;
        GC.SuppressFinalize( this );
    }

    ~ActionProvider()
        => Dispose();
}