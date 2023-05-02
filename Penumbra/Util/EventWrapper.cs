using System;
using System.Collections.Generic;
using System.Linq;

namespace Penumbra.Util;

public abstract class EventWrapper<T, TPriority> : IDisposable
    where T : Delegate
    where TPriority : struct, Enum
{
    private readonly string                                        _name;
    private readonly List<(object Subscriber, TPriority Priority)> _event = new();

    public bool HasSubscribers
        => _event.Count > 0;

    protected EventWrapper(string name)
        => _name = name;

    public void Dispose()
    {
        lock (_event)
        {
            _event.Clear();
        }
    }

    public void Subscribe(T subscriber, TPriority priority)
    {
        lock (_event)
        {
            var existingIdx = _event.FindIndex(p => (T)p.Subscriber == subscriber);
            var idx         = _event.FindIndex(p => p.Priority.CompareTo(priority) > 0);
            if (idx == existingIdx)
            {
                if (idx < 0)
                    _event.Add((subscriber, priority));
                else
                    _event[idx] = (subscriber, priority);
            }
            else
            {
                if (idx < 0)
                    _event.Add((subscriber, priority));
                else
                    _event.Insert(idx, (subscriber, priority));

                if (existingIdx >= 0)
                    _event.RemoveAt(existingIdx < idx ? existingIdx : existingIdx + 1);
            }
        }
    }

    public void Unsubscribe(T subscriber)
    {
        lock (_event)
        {
            var idx = _event.FindIndex(p => (T)p.Subscriber == subscriber);
            if (idx >= 0)
                _event.RemoveAt(idx);
        }
    }


    protected static void Invoke(EventWrapper<T, TPriority> wrapper)
    {
        lock (wrapper._event)
        {
            foreach (var (action, _) in wrapper._event.AsEnumerable().Reverse())
            {
                try
                {
                    ((Action)action).Invoke();
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{wrapper._name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    protected static void Invoke<T1>(EventWrapper<T, TPriority> wrapper, T1 a)
    {
        lock (wrapper._event)
        {
            foreach (var (action, _) in wrapper._event.AsEnumerable().Reverse())
            {
                try
                {
                    ((Action<T1>)action).Invoke(a);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{wrapper._name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    protected static void Invoke<T1, T2>(EventWrapper<T, TPriority> wrapper, T1 a, T2 b)
    {
        lock (wrapper._event)
        {
            foreach (var (action, _) in wrapper._event.AsEnumerable().Reverse())
            {
                try
                {
                    ((Action<T1, T2>)action).Invoke(a, b);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{wrapper._name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    protected static void Invoke<T1, T2, T3>(EventWrapper<T, TPriority> wrapper, T1 a, T2 b, T3 c)
    {
        lock (wrapper._event)
        {
            foreach (var (action, _) in wrapper._event.AsEnumerable().Reverse())
            {
                try
                {
                    ((Action<T1, T2, T3>)action).Invoke(a, b, c);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{wrapper._name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    protected static void Invoke<T1, T2, T3, T4>(EventWrapper<T, TPriority> wrapper, T1 a, T2 b, T3 c, T4 d)
    {
        lock (wrapper._event)
        {
            foreach (var (action, _) in wrapper._event.AsEnumerable().Reverse())
            {
                try
                {
                    ((Action<T1, T2, T3, T4>)action).Invoke(a, b, c, d);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{wrapper._name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    protected static void Invoke<T1, T2, T3, T4, T5>(EventWrapper<T, TPriority> wrapper, T1 a, T2 b, T3 c, T4 d, T5 e)
    {
        lock (wrapper._event)
        {
            foreach (var (action, _) in wrapper._event.AsEnumerable().Reverse())
            {
                try
                {
                    ((Action<T1, T2, T3, T4, T5>)action).Invoke(a, b, c, d, e);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{wrapper._name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }

    protected static void Invoke<T1, T2, T3, T4, T5, T6>(EventWrapper<T, TPriority> wrapper, T1 a, T2 b, T3 c, T4 d, T5 e, T6 f)
    {
        lock (wrapper._event)
        {
            foreach (var (action, _) in wrapper._event.AsEnumerable().Reverse())
            {
                try
                {
                    ((Action<T1, T2, T3, T4, T5, T6>)action).Invoke(a, b, c, d, e, f);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"[{wrapper._name}] Exception thrown during invocation:\n{ex}");
                }
            }
        }
    }
}
