using System;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace Penumbra.Api.Helpers;

public readonly struct FuncSubscriber< TRet >
{
    private readonly string                       _label;
    private readonly ICallGateSubscriber< TRet >? _subscriber;

    public bool Valid
        => _subscriber != null;

    public FuncSubscriber( DalamudPluginInterface pi, string label )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public TRet Invoke()
        => _subscriber != null ? _subscriber.InvokeFunc() : throw new IpcNotReadyError( _label );
}

public readonly struct FuncSubscriber< T1, TRet >
{
    private readonly string                           _label;
    private readonly ICallGateSubscriber< T1, TRet >? _subscriber;

    public bool Valid
        => _subscriber != null;

    public FuncSubscriber( DalamudPluginInterface pi, string label )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public TRet Invoke( T1 a )
        => _subscriber != null ? _subscriber.InvokeFunc( a ) : throw new IpcNotReadyError( _label );
}

public readonly struct FuncSubscriber< T1, T2, TRet >
{
    private readonly string                               _label;
    private readonly ICallGateSubscriber< T1, T2, TRet >? _subscriber;

    public bool Valid
        => _subscriber != null;

    public FuncSubscriber( DalamudPluginInterface pi, string label )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, T2, TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public TRet Invoke( T1 a, T2 b )
        => _subscriber != null ? _subscriber.InvokeFunc( a, b ) : throw new IpcNotReadyError( _label );
}

public readonly struct FuncSubscriber< T1, T2, T3, TRet >
{
    private readonly string                                   _label;
    private readonly ICallGateSubscriber< T1, T2, T3, TRet >? _subscriber;

    public bool Valid
        => _subscriber != null;

    public FuncSubscriber( DalamudPluginInterface pi, string label )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, T2, T3, TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public TRet Invoke( T1 a, T2 b, T3 c )
        => _subscriber != null ? _subscriber.InvokeFunc( a, b, c ) : throw new IpcNotReadyError( _label );
}

public readonly struct FuncSubscriber< T1, T2, T3, T4, TRet >
{
    private readonly string                                       _label;
    private readonly ICallGateSubscriber< T1, T2, T3, T4, TRet >? _subscriber;

    public bool Valid
        => _subscriber != null;

    public FuncSubscriber( DalamudPluginInterface pi, string label )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, T2, T3, T4, TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public TRet Invoke( T1 a, T2 b, T3 c, T4 d )
        => _subscriber != null ? _subscriber.InvokeFunc( a, b, c, d ) : throw new IpcNotReadyError( _label );
}

public readonly struct FuncSubscriber< T1, T2, T3, T4, T5, TRet >
{
    private readonly string                                           _label;
    private readonly ICallGateSubscriber< T1, T2, T3, T4, T5, TRet >? _subscriber;

    public bool Valid
        => _subscriber != null;

    public FuncSubscriber( DalamudPluginInterface pi, string label )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, T2, T3, T4, T5, TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public TRet Invoke( T1 a, T2 b, T3 c, T4 d, T5 e )
        => _subscriber != null ? _subscriber.InvokeFunc( a, b, c, d, e ) : throw new IpcNotReadyError( _label );
}