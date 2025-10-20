using Dalamud.Interface.Windowing;

namespace Penumbra.UI.Classes;

public abstract class WindowFactory<T> where T : IndexedWindow
{
    protected WindowSystem? WindowSystem;

    private readonly HashSet<T>   _openWindows     = [];
    private readonly HashSet<int> _reusableIndices = [];
    private          int          _nextIndex       = 0;

    protected IEnumerable<T> OpenWindows
        => _openWindows;

    internal void SetWindowSystem(WindowSystem windowSystem)
    {
        if (WindowSystem is not null)
            throw new InvalidOperationException("WindowSystem is already set");
        
        WindowSystem = windowSystem;
        
        OnWindowSystemSet();
    }

    protected virtual void OnWindowSystemSet()
    {
    }

    protected int GetFreeIndex()
    {
        foreach (var index in _reusableIndices) {
            _reusableIndices.Remove(index);
            return index;
        }

        return _nextIndex++;
    }

    protected abstract T? DoCreateWindow();

    protected T? CreateWindow()
    {
        var window = DoCreateWindow();
        if (window is not null) {
            SetupWindow(window);
        }

        return window;
    }

    protected void SetupWindow(T window)
    {
        window.Close += WindowClose;
        WindowSystem?.AddWindow(window);
        window.IsOpen = true;
        _openWindows.Add(window);
        window.BringToFront();
    }

    private void WindowClose(object? sender, EventArgs e)
    {
        if (sender is not T window) {
            return;
        }

        _openWindows.Remove(window);
        _reusableIndices.Add(window.Index);
    }
}
