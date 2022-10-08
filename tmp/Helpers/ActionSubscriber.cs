using System;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Penumbra.Api.Helpers;

public readonly struct ActionSubscriber< T1 >
{
    private readonly ICallGateSubscriber< T1, object? >? _subscriber;

    public bool Valid
        => _subscriber != null;

    public ActionSubscriber( DalamudPluginInterface pi, string label )
    {
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, object? >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public void Invoke( T1 a )
        => _subscriber?.InvokeAction( a );
}

public readonly struct ActionSubscriber< T1, T2 >
{
    private readonly ICallGateSubscriber< T1, T2, object? >? _subscriber;

    public bool Valid
        => _subscriber != null;

    public ActionSubscriber( DalamudPluginInterface pi, string label )
    {
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, T2, object? >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public void Invoke( T1 a, T2 b )
        => _subscriber?.InvokeAction( a, b );
}