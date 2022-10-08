using System;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Penumbra.Api.Helpers;

public sealed class EventProvider : IDisposable
{
    private ICallGateProvider< object? >? _provider;
    private Delegate?                     _unsubscriber;

    public EventProvider( DalamudPluginInterface pi, string label, (Action< Action > Add, Action< Action > Del)? subscribe = null )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< object? >( label );
            subscribe?.Add( Invoke );
            _unsubscriber = subscribe?.Del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public EventProvider( DalamudPluginInterface pi, string label, Action add, Action del )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< object? >( label );
            add();
            _unsubscriber = del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public void Invoke()
        => _provider?.SendMessage();

    public void Dispose()
    {
        switch( _unsubscriber )
        {
            case Action< Action > a:
                a( Invoke );
                break;
            case Action b:
                b();
                break;
        }

        _unsubscriber = null;
        _provider     = null;
        GC.SuppressFinalize( this );
    }

    ~EventProvider()
        => Dispose();
}

public sealed class EventProvider< T1 > : IDisposable
{
    private ICallGateProvider< T1, object? >? _provider;
    private Delegate?                         _unsubscriber;

    public EventProvider( DalamudPluginInterface pi, string label, (Action< Action< T1 > > Add, Action< Action< T1 > > Del)? subscribe = null )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< T1, object? >( label );
            subscribe?.Add( Invoke );
            _unsubscriber = subscribe?.Del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public EventProvider( DalamudPluginInterface pi, string label, Action add, Action del )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< T1, object? >( label );
            add();
            _unsubscriber = del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public void Invoke( T1 a )
        => _provider?.SendMessage( a );

    public void Dispose()
    {
        switch( _unsubscriber )
        {
            case Action< Action< T1 > > a:
                a( Invoke );
                break;
            case Action b:
                b();
                break;
        }

        _unsubscriber = null;
        _provider     = null;
        GC.SuppressFinalize( this );
    }

    ~EventProvider()
        => Dispose();
}

public sealed class EventProvider< T1, T2 > : IDisposable
{
    private ICallGateProvider< T1, T2, object? >? _provider;
    private Delegate?                             _unsubscriber;

    public EventProvider( DalamudPluginInterface pi, string label,
        (Action< Action< T1, T2 > > Add, Action< Action< T1, T2 > > Del)? subscribe = null )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, object? >( label );
            subscribe?.Add( Invoke );
            _unsubscriber = subscribe?.Del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public EventProvider( DalamudPluginInterface pi, string label, Action add, Action del )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, object? >( label );
            add();
            _unsubscriber = del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public void Invoke( T1 a, T2 b )
        => _provider?.SendMessage( a, b );

    public void Dispose()
    {
        switch( _unsubscriber )
        {
            case Action< Action< T1, T2 > > a:
                a( Invoke );
                break;
            case Action b:
                b();
                break;
        }

        _unsubscriber = null;
        _provider     = null;
        GC.SuppressFinalize( this );
    }

    ~EventProvider()
        => Dispose();
}

public sealed class EventProvider< T1, T2, T3 > : IDisposable
{
    private ICallGateProvider< T1, T2, T3, object? >? _provider;
    private Delegate?                                 _unsubscriber;

    public EventProvider( DalamudPluginInterface pi, string label,
        (Action< Action< T1, T2, T3 > > Add, Action< Action< T1, T2, T3 > > Del)? subscribe = null )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, T3, object? >( label );
            subscribe?.Add( Invoke );
            _unsubscriber = subscribe?.Del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public EventProvider( DalamudPluginInterface pi, string label, Action add, Action del )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, T3, object? >( label );
            add();
            _unsubscriber = del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public void Invoke( T1 a, T2 b, T3 c )
        => _provider?.SendMessage( a, b, c );

    public void Dispose()
    {
        switch( _unsubscriber )
        {
            case Action< Action< T1, T2, T3 > > a:
                a( Invoke );
                break;
            case Action b:
                b();
                break;
        }

        _unsubscriber = null;
        _provider     = null;
        GC.SuppressFinalize( this );
    }

    ~EventProvider()
        => Dispose();
}

public sealed class EventProvider< T1, T2, T3, T4 > : IDisposable
{
    private ICallGateProvider< T1, T2, T3, T4, object? >? _provider;
    private Delegate?                                     _unsubscriber;

    public EventProvider( DalamudPluginInterface pi, string label,
        (Action< Action< T1, T2, T3, T4 > > Add, Action< Action< T1, T2, T3, T4 > > Del)? subscribe = null )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, T3, T4, object? >( label );
            subscribe?.Add( Invoke );
            _unsubscriber = subscribe?.Del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public EventProvider( DalamudPluginInterface pi, string label, Action add, Action del )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, T3, T4, object? >( label );
            add();
            _unsubscriber = del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public void Invoke( T1 a, T2 b, T3 c, T4 d )
        => _provider?.SendMessage( a, b, c, d );

    public void Dispose()
    {
        switch( _unsubscriber )
        {
            case Action< Action< T1, T2, T3, T4 > > a:
                a( Invoke );
                break;
            case Action b:
                b();
                break;
        }

        _unsubscriber = null;
        _provider     = null;
        GC.SuppressFinalize( this );
    }

    ~EventProvider()
        => Dispose();
}

public sealed class EventProvider< T1, T2, T3, T4, T5 > : IDisposable
{
    private ICallGateProvider< T1, T2, T3, T4, T5, object? >? _provider;
    private Delegate?                                         _unsubscriber;

    public EventProvider( DalamudPluginInterface pi, string label,
        (Action< Action< T1, T2, T3, T4, T5 > > Add, Action< Action< T1, T2, T3, T4, T5 > > Del)? subscribe = null )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, T3, T4, T5, object? >( label );
            subscribe?.Add( Invoke );
            _unsubscriber = subscribe?.Del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public EventProvider( DalamudPluginInterface pi, string label, Action add, Action del )
    {
        _unsubscriber = null;
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, T3, T4, T5, object? >( label );
            add();
            _unsubscriber = del;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }
    }

    public void Invoke( T1 a, T2 b, T3 c, T4 d, T5 e )
        => _provider?.SendMessage( a, b, c, d, e );

    public void Dispose()
    {
        switch( _unsubscriber )
        {
            case Action< Action< T1, T2, T3, T4, T5 > > a:
                a( Invoke );
                break;
            case Action b:
                b();
                break;
        }

        _unsubscriber = null;
        _provider     = null;
        GC.SuppressFinalize( this );
    }

    ~EventProvider()
        => Dispose();
}