using Dalamud.Plugin.Ipc;

namespace Penumbra.Api;

public class BasicAdapter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static TOut CheckValue<TArg, TOut>(int method, int numArguments, bool func, int argumentIndex, ref TArg arg)
        where TArg : allows ref struct
        where TOut : allows ref struct
    {
        if (typeof(TArg) != typeof(TOut))
            throw new AdapterTypeMismatchException(method, numArguments, func, argumentIndex, typeof(TArg));

        return Unsafe.As<TArg, TOut>(ref arg);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void CheckRet<TRet, TOut>(int method, int numArguments)
        where TRet : allows ref struct
        where TOut : allows ref struct
    {
        if (typeof(TRet) != typeof(TOut))
            throw new AdapterTypeMismatchException(method, numArguments, true, -1, typeof(TRet));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static TRet? CheckRet<TRet, TOut>(int method, int numArguments, TOut? value)
        where TRet : allows ref struct
        where TOut : allows ref struct
    {
        if (typeof(TRet) != typeof(TOut))
            throw new AdapterTypeMismatchException(method, numArguments, true, -1, typeof(TRet));

        if (value is null)
            return default;

        return Unsafe.As<TOut, TRet>(ref value);
    }
}

public class BasicAdapter<T>(T manager) : BasicAdapter, IIdDataShareAdapter
    where T : class
{
    protected readonly WeakReference<T> WeakManager = new(manager);

    protected T Manager
    {
        get
        {
            if (WeakManager.TryGetTarget(out var storage))
                return storage;

            Dispose();
            throw new ObjectDisposedException("The reference to the Manager is invalid.");
        }
    }

    public void Dispose()
    {
        WeakManager.SetTarget(null!);
        GC.SuppressFinalize(this);
    }

    ~BasicAdapter()
        => WeakManager.SetTarget(null!);
}

public class BasicAdapter<T1, T2>(T1 manager1, T2 manager2) : BasicAdapter, IIdDataShareAdapter
    where T1 : class
    where T2 : class
{
    protected readonly WeakReference<T1> WeakManager1 = new(manager1);
    protected readonly WeakReference<T2> WeakManager2 = new(manager2);

    protected T1 Manager1
    {
        get
        {
            if (WeakManager1.TryGetTarget(out var storage))
                return storage;

            Dispose();
            throw new ObjectDisposedException("The reference to the Manager1 is invalid.");
        }
    }

    protected T2 Manager2
    {
        get
        {
            if (WeakManager2.TryGetTarget(out var storage))
                return storage;

            Dispose();
            throw new ObjectDisposedException("The reference to the Manager2 is invalid.");
        }
    }

    public void Dispose()
    {
        WeakManager1.SetTarget(null!);
        WeakManager2.SetTarget(null!);
        GC.SuppressFinalize(this);
    }

    ~BasicAdapter()
    {
        WeakManager1.SetTarget(null!);
        WeakManager2.SetTarget(null!);
    }
}
