using OtterGui.Tasks;
using Penumbra.Util;

namespace Penumbra.Services;

public abstract class SyncServiceWrapper<T> : IDisposable
{
    public  string Name    { get; }
    public  T      Service { get; }
    private bool   _isDisposed;

    public bool Valid
        => !_isDisposed;

    protected SyncServiceWrapper(string name, StartTracker tracker, StartTimeType type, Func<T> factory)
    {
        Name = name;
        using var timer = tracker.Measure(type);
        Service = factory();
        Penumbra.Log.Verbose($"[{Name}] Created.");
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        if (Service is IDisposable d)
            d.Dispose();
        Penumbra.Log.Verbose($"[{Name}] Disposed.");
    }
}

public abstract class AsyncServiceWrapper<T> : IDisposable
{
    public string Name    { get; }
    public T?     Service { get; private set; }

    public T AwaitedService
    {
        get
        {
            _task?.Wait();
            return Service!;
        }
    }

    public bool Valid
        => Service != null && !_isDisposed;

    public event Action? FinishedCreation;
    private Task?        _task;

    private bool _isDisposed;

    protected AsyncServiceWrapper(string name, StartTracker tracker, StartTimeType type, Func<T> factory)
    {
        Name = name;
        _task = TrackedTask.Run(() =>
        {
            using var timer   = tracker.Measure(type);
            var       service = factory();
            if (_isDisposed)
            {
                if (service is IDisposable d)
                    d.Dispose();
            }
            else
            {
                Service = service;
                Penumbra.Log.Verbose($"[{Name}] Created.");
                _task = null;
            }
        });
        _task.ContinueWith((t, x) =>
        {
            if (!_isDisposed)
                FinishedCreation?.Invoke();
        }, null);
    }

    protected AsyncServiceWrapper(string name, Func<T> factory)
    {
        Name = name;
        _task = TrackedTask.Run(() =>
        {
            var service = factory();
            if (_isDisposed)
            {
                if (service is IDisposable d)
                    d.Dispose();
            }
            else
            {
                Service = service;
                Penumbra.Log.Verbose($"[{Name}] Created.");
                _task = null;
            }
        });
        _task.ContinueWith((t, x) =>
        {
            if (!_isDisposed)
                FinishedCreation?.Invoke();
        }, null);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _task       = null;
        if (Service is IDisposable d)
            d.Dispose();
        Penumbra.Log.Verbose($"[{Name}] Disposed.");
    }
}
