using System;
using System.Collections.Generic;
using System.Linq;

namespace Penumbra.Util;

public readonly struct EventWrapper : IDisposable
{
    private readonly string       _name;
    private readonly List<Action> _event = new();

    public EventWrapper(string name)
        => _name = name;

    public void Invoke()
    {
        lock (_event)
        {
            foreach (var action in _event)
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{_name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_event)
        {
            _event.Clear();
        }
    }

    public event Action Event
    {
        add
        {
            lock (_event)
            {
                if (_event.All(a => a != value))
                    _event.Add(value);
            }
        }
        remove
        {
            lock (_event)
            {
                _event.Remove(value);
            }
        }
    }
}

public readonly struct EventWrapper<T1, T2> : IDisposable
{
    private readonly string               _name;
    private readonly List<Action<T1, T2>> _event = new();

    public EventWrapper(string name)
        => _name = name;

    public void Invoke(T1 arg1, T2 arg2)
    {
        lock (_event)
        {
            foreach (var action in _event)
            {
                try
                {
                    action.Invoke(arg1, arg2);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{_name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_event)
        {
            _event.Clear();
        }
    }

    public event Action<T1, T2> Event
    {
        add
        {
            lock (_event)
            {
                if (_event.All(a => a != value))
                    _event.Add(value);
            }
        }
        remove
        {
            lock (_event)
            {
                _event.Remove(value);
            }
        }
    }
}

public readonly struct EventWrapper<T1, T2, T3> : IDisposable
{
    private readonly string                   _name;
    private readonly List<Action<T1, T2, T3>> _event = new();

    public EventWrapper(string name)
        => _name = name;

    public void Invoke(T1 arg1, T2 arg2, T3 arg3)
    {
        lock (_event)
        {
            foreach (var action in _event)
            {
                try
                {
                    action.Invoke(arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{_name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_event)
        {
            _event.Clear();
        }
    }

    public event Action<T1, T2, T3> Event
    {
        add
        {
            lock (_event)
            {
                if (_event.All(a => a != value))
                    _event.Add(value);
            }
        }
        remove
        {
            lock (_event)
            {
                _event.Remove(value);
            }
        }
    }
}

public readonly struct EventWrapper<T1, T2, T3, T4> : IDisposable
{
    private readonly string                       _name;
    private readonly List<Action<T1, T2, T3, T4>> _event = new();

    public EventWrapper(string name)
        => _name = name;

    public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (_event)
        {
            foreach (var action in _event)
            {
                try
                {
                    action.Invoke(arg1, arg2, arg3, arg4);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{_name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_event)
        {
            _event.Clear();
        }
    }

    public event Action<T1, T2, T3, T4> Event
    {
        add
        {
            lock (_event)
            {
                if (_event.All(a => a != value))
                    _event.Add(value);
            }
        }
        remove
        {
            lock (_event)
            {
                _event.Remove(value);
            }
        }
    }
}

public readonly struct EventWrapper<T1, T2, T3, T4, T5> : IDisposable
{
    private readonly string                           _name;
    private readonly List<Action<T1, T2, T3, T4, T5>> _event = new();

    public EventWrapper(string name)
        => _name = name;

    public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (_event)
        {
            foreach (var action in _event)
            {
                try
                {
                    action.Invoke(arg1, arg2, arg3, arg4, arg5);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{_name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_event)
        {
            _event.Clear();
        }
    }

    public event Action<T1, T2, T3, T4, T5> Event
    {
        add
        {
            lock (_event)
            {
                if (_event.All(a => a != value))
                    _event.Add(value);
            }
        }
        remove
        {
            lock (_event)
            {
                _event.Remove(value);
            }
        }
    }
}

public readonly struct EventWrapper<T1, T2, T3, T4, T5, T6> : IDisposable
{
    private readonly string                               _name;
    private readonly List<Action<T1, T2, T3, T4, T5, T6>> _event = new();

    public EventWrapper(string name)
        => _name = name;

    public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        lock (_event)
        {
            foreach (var action in _event)
            {
                try
                {
                    action.Invoke(arg1, arg2, arg3, arg4, arg5, arg6);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{_name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_event)
        {
            _event.Clear();
        }
    }

    public event Action<T1, T2, T3, T4, T5, T6> Event
    {
        add
        {
            lock (_event)
            {
                if (_event.All(a => a != value))
                    _event.Add(value);
            }
        }
        remove
        {
            lock (_event)
            {
                _event.Remove(value);
            }
        }
    }
}
