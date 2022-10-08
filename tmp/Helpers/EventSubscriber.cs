using System;
using System.Collections.Generic;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Penumbra.Api.Helpers;

public sealed class EventSubscriber : IDisposable
{
    private readonly string                          _label;
    private readonly Dictionary< Action, Action >    _delegates = new();
    private          ICallGateSubscriber< object? >? _subscriber;
    private          bool                            _disabled;

    public EventSubscriber( DalamudPluginInterface pi, string label, params Action[] actions )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< object? >( label );
            foreach( var action in actions )
            {
                Event += action;
            }

            _disabled = false;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public void Enable()
    {
        if( _disabled && _subscriber != null )
        {
            foreach( var action in _delegates.Keys )
            {
                _subscriber.Subscribe( action );
            }

            _disabled = false;
        }
    }

    public void Disable()
    {
        if( !_disabled )
        {
            if( _subscriber != null )
            {
                foreach( var action in _delegates.Keys )
                {
                    _subscriber.Unsubscribe( action );
                }
            }

            _disabled = true;
        }
    }

    public event Action Event
    {
        add
        {
            if( _subscriber != null && !_delegates.ContainsKey( value ) )
            {
                void Action()
                {
                    try
                    {
                        value();
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Exception invoking IPC event {_label}:\n{e}" );
                    }
                }

                if( _delegates.TryAdd( value, Action ) && !_disabled )
                {
                    _subscriber.Subscribe( Action );
                }
            }
        }
        remove
        {
            if( _subscriber != null && _delegates.Remove( value, out var action ) )
            {
                _subscriber.Unsubscribe( action );
            }
        }
    }

    public void Dispose()
    {
        Disable();
        _subscriber = null;
        _delegates.Clear();
    }

    ~EventSubscriber()
        => Dispose();
}

public sealed class EventSubscriber< T1 > : IDisposable
{
    private readonly string                                   _label;
    private readonly Dictionary< Action< T1 >, Action< T1 > > _delegates = new();
    private          ICallGateSubscriber< T1, object? >?      _subscriber;
    private          bool                                     _disabled;

    public EventSubscriber( DalamudPluginInterface pi, string label, params Action< T1 >[] actions )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, object? >( label );
            foreach( var action in actions )
            {
                Event += action;
            }

            _disabled = false;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public void Enable()
    {
        if( _disabled && _subscriber != null )
        {
            foreach( var action in _delegates.Keys )
            {
                _subscriber.Subscribe( action );
            }

            _disabled = false;
        }
    }

    public void Disable()
    {
        if( !_disabled )
        {
            if( _subscriber != null )
            {
                foreach( var action in _delegates.Keys )
                {
                    _subscriber.Unsubscribe( action );
                }
            }

            _disabled = true;
        }
    }

    public event Action< T1 > Event
    {
        add
        {
            if( _subscriber != null && !_delegates.ContainsKey( value ) )
            {
                void Action( T1 a )
                {
                    try
                    {
                        value( a );
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Exception invoking IPC event {_label}:\n{e}" );
                    }
                }

                if( _delegates.TryAdd( value, Action ) && !_disabled )
                {
                    _subscriber.Subscribe( Action );
                }
            }
        }
        remove
        {
            if( _subscriber != null && _delegates.Remove( value, out var action ) )
            {
                _subscriber.Unsubscribe( action );
            }
        }
    }

    public void Dispose()
    {
        Disable();
        _subscriber = null;
        _delegates.Clear();
    }

    ~EventSubscriber()
        => Dispose();
}

public sealed class EventSubscriber< T1, T2 > : IDisposable
{
    private readonly string                                           _label;
    private readonly Dictionary< Action< T1, T2 >, Action< T1, T2 > > _delegates = new();
    private          ICallGateSubscriber< T1, T2, object? >?          _subscriber;
    private          bool                                             _disabled;

    public EventSubscriber( DalamudPluginInterface pi, string label, params Action< T1, T2 >[] actions )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, T2, object? >( label );
            foreach( var action in actions )
            {
                Event += action;
            }

            _disabled = false;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public void Enable()
    {
        if( _disabled && _subscriber != null )
        {
            foreach( var action in _delegates.Keys )
            {
                _subscriber.Subscribe( action );
            }

            _disabled = false;
        }
    }

    public void Disable()
    {
        if( !_disabled )
        {
            if( _subscriber != null )
            {
                foreach( var action in _delegates.Keys )
                {
                    _subscriber.Unsubscribe( action );
                }
            }

            _disabled = true;
        }
    }

    public event Action< T1, T2 > Event
    {
        add
        {
            if( _subscriber != null && !_delegates.ContainsKey( value ) )
            {
                void Action( T1 a, T2 b )
                {
                    try
                    {
                        value( a, b );
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Exception invoking IPC event {_label}:\n{e}" );
                    }
                }

                if( _delegates.TryAdd( value, Action ) && !_disabled )
                {
                    _subscriber.Subscribe( Action );
                }
            }
        }
        remove
        {
            if( _subscriber != null && _delegates.Remove( value, out var action ) )
            {
                _subscriber.Unsubscribe( action );
            }
        }
    }

    public void Dispose()
    {
        Disable();
        _subscriber = null;
        _delegates.Clear();
    }

    ~EventSubscriber()
        => Dispose();
}

public sealed class EventSubscriber< T1, T2, T3 > : IDisposable
{
    private readonly string                                                   _label;
    private readonly Dictionary< Action< T1, T2, T3 >, Action< T1, T2, T3 > > _delegates = new();
    private          ICallGateSubscriber< T1, T2, T3, object? >?              _subscriber;
    private          bool                                                     _disabled;

    public EventSubscriber( DalamudPluginInterface pi, string label, params Action< T1, T2, T3 >[] actions )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, T2, T3, object? >( label );
            foreach( var action in actions )
            {
                Event += action;
            }

            _disabled = false;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public void Enable()
    {
        if( _disabled && _subscriber != null )
        {
            foreach( var action in _delegates.Keys )
            {
                _subscriber.Subscribe( action );
            }

            _disabled = false;
        }
    }

    public void Disable()
    {
        if( !_disabled )
        {
            if( _subscriber != null )
            {
                foreach( var action in _delegates.Keys )
                {
                    _subscriber.Unsubscribe( action );
                }
            }

            _disabled = true;
        }
    }

    public event Action< T1, T2, T3 > Event
    {
        add
        {
            if( _subscriber != null && !_delegates.ContainsKey( value ) )
            {
                void Action( T1 a, T2 b, T3 c )
                {
                    try
                    {
                        value( a, b, c );
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Exception invoking IPC event {_label}:\n{e}" );
                    }
                }

                if( _delegates.TryAdd( value, Action ) && !_disabled )
                {
                    _subscriber.Subscribe( Action );
                }
            }
        }
        remove
        {
            if( _subscriber != null && _delegates.Remove( value, out var action ) )
            {
                _subscriber.Unsubscribe( action );
            }
        }
    }

    public void Dispose()
    {
        Disable();
        _subscriber = null;
        _delegates.Clear();
    }

    ~EventSubscriber()
        => Dispose();
}

public sealed class EventSubscriber< T1, T2, T3, T4 > : IDisposable
{
    private readonly string                                                           _label;
    private readonly Dictionary< Action< T1, T2, T3, T4 >, Action< T1, T2, T3, T4 > > _delegates = new();
    private          ICallGateSubscriber< T1, T2, T3, T4, object? >?                  _subscriber;
    private          bool                                                             _disabled;

    public EventSubscriber( DalamudPluginInterface pi, string label, params Action< T1, T2, T3, T4 >[] actions )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, T2, T3, T4, object? >( label );
            foreach( var action in actions )
            {
                Event += action;
            }

            _disabled = false;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public void Enable()
    {
        if( _disabled && _subscriber != null )
        {
            foreach( var action in _delegates.Keys )
            {
                _subscriber.Subscribe( action );
            }

            _disabled = false;
        }
    }

    public void Disable()
    {
        if( !_disabled )
        {
            if( _subscriber != null )
            {
                foreach( var action in _delegates.Keys )
                {
                    _subscriber.Unsubscribe( action );
                }
            }

            _disabled = true;
        }
    }

    public event Action< T1, T2, T3, T4 > Event
    {
        add
        {
            if( _subscriber != null && !_delegates.ContainsKey( value ) )
            {
                void Action( T1 a, T2 b, T3 c, T4 d )
                {
                    try
                    {
                        value( a, b, c, d );
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Exception invoking IPC event {_label}:\n{e}" );
                    }
                }

                if( _delegates.TryAdd( value, Action ) && !_disabled )
                {
                    _subscriber.Subscribe( Action );
                }
            }
        }
        remove
        {
            if( _subscriber != null && _delegates.Remove( value, out var action ) )
            {
                _subscriber.Unsubscribe( action );
            }
        }
    }

    public void Dispose()
    {
        Disable();
        _subscriber = null;
        _delegates.Clear();
    }

    ~EventSubscriber()
        => Dispose();
}

public sealed class EventSubscriber< T1, T2, T3, T4, T5 > : IDisposable
{
    private readonly string                                                                   _label;
    private readonly Dictionary< Action< T1, T2, T3, T4, T5 >, Action< T1, T2, T3, T4, T5 > > _delegates = new();
    private          ICallGateSubscriber< T1, T2, T3, T4, T5, object? >?                      _subscriber;
    private          bool                                                                     _disabled;

    public EventSubscriber( DalamudPluginInterface pi, string label, params Action< T1, T2, T3, T4, T5 >[] actions )
    {
        _label = label;
        try
        {
            _subscriber = pi.GetIpcSubscriber< T1, T2, T3, T4, T5, object? >( label );
            foreach( var action in actions )
            {
                Event += action;
            }

            _disabled = false;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Subscriber for {label}\n{e}" );
            _subscriber = null;
        }
    }

    public void Enable()
    {
        if( _disabled && _subscriber != null )
        {
            foreach( var action in _delegates.Keys )
            {
                _subscriber.Subscribe( action );
            }

            _disabled = false;
        }
    }

    public void Disable()
    {
        if( !_disabled )
        {
            if( _subscriber != null )
            {
                foreach( var action in _delegates.Keys )
                {
                    _subscriber.Unsubscribe( action );
                }
            }

            _disabled = true;
        }
    }

    public event Action< T1, T2, T3, T4, T5 > Event
    {
        add
        {
            if( _subscriber != null && !_delegates.ContainsKey( value ) )
            {
                void Action( T1 a, T2 b, T3 c, T4 d, T5 e )
                {
                    try
                    {
                        value( a, b, c, d, e );
                    }
                    catch( Exception ex )
                    {
                        PluginLog.Error( $"Exception invoking IPC event {_label}:\n{ex}" );
                    }
                }

                if( _delegates.TryAdd( value, Action ) && !_disabled )
                {
                    _subscriber.Subscribe( Action );
                }
            }
        }
        remove
        {
            if( _subscriber != null && _delegates.Remove( value, out var action ) )
            {
                _subscriber.Unsubscribe( action );
            }
        }
    }

    public void Dispose()
    {
        Disable();
        _subscriber = null;
        _delegates.Clear();
    }

    ~EventSubscriber()
        => Dispose();
}