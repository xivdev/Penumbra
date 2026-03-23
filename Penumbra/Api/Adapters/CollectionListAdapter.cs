using Dalamud.Plugin.Ipc;
using Penumbra.Api.IpcSubscribers;
using Penumbra.Collections.Manager;

namespace Penumbra.Api;

public sealed class CollectionListAdapter(CollectionStorage collections, ActiveCollections active)
    : BasicAdapter<CollectionStorage, ActiveCollections>(collections, active)
{
    public bool TryInvoke<TRet>(int method, out TRet? output) where TRet : allows ref struct
    {
        switch (method)
        {
            case (int)CollectionListWrapper.Method.GetEnumerator:
            {
                var enumerator = Manager1.GetEnumerator();
                if (enumerator is not TRet e)
                {
                    enumerator.Dispose();
                    throw new AdapterTypeMismatchException(method, 0, true, -1, typeof(TRet));
                }

                output = e;
                return true;
            }
            case (int)CollectionListWrapper.Method.Count:
            {
                if (typeof(TRet) != typeof(int))
                    throw new AdapterTypeMismatchException(method, 0, true, -1, typeof(TRet));

                var count = Manager1.Count;
                output = Unsafe.As<int, TRet>(ref count);
                return true;
            }
            case (int)CollectionListWrapper.Method.GetCurrent:
            {
                if (typeof(TRet) != typeof(IIdDataShareAdapter))
                    throw new AdapterTypeMismatchException(method, 0, true, -1, typeof(TRet));

                if (Manager2.Current is TRet ret)
                    output = ret;
                else
                    output = default;
                return true;
            }
            case (int)CollectionListWrapper.Method.GetDefault:
            {
                if (typeof(TRet) != typeof(IIdDataShareAdapter))
                    throw new AdapterTypeMismatchException(method, 0, true, -1, typeof(TRet));

                if (Manager2.Default is TRet ret)
                    output = ret;
                else
                    output = default;
                return true;
            }
            case (int)CollectionListWrapper.Method.GetInterface:
            {
                if (typeof(TRet) != typeof(IIdDataShareAdapter))
                    throw new AdapterTypeMismatchException(method, 0, true, -1, typeof(TRet));

                if (Manager2.Interface is TRet ret)
                    output = ret;
                else
                    output = default;
                return true;
            }
        }

        throw new AdapterMethodMissingException(method, 0, true);
    }

    public bool TryInvoke<T1, TRet>(int method, T1 arg1, out TRet? output)
        where T1 : allows ref struct
        where TRet : allows ref struct
    {
        switch (method)
        {
            case (int)ModListWrapper.Method.GetModByIndex:
            {
                if (typeof(T1) != typeof(int))
                    throw new AdapterTypeMismatchException(method, 1, true, 0, typeof(T1));
                if (typeof(TRet) != typeof(IIdDataShareAdapter))
                    throw new AdapterTypeMismatchException(method, 1, true, -1, typeof(TRet));

                var index = Unsafe.As<T1, int>(ref arg1);
                if (index < 0 || index >= Manager.Count || Manager[index] is not TRet r)
                {
                    output = default;
                    return true;
                }

                output = r;
                return true;
            }
        }

        throw new AdapterMethodMissingException(method, 1, true);
    }

    public bool TryInvoke<T1, T2, TRet>(int method, T1 arg1, T2 arg2, out TRet? output)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where TRet : allows ref struct
    {
        switch (method)
        {
            case (int)ModListWrapper.Method.GetModByName:
            {
                if (typeof(T1) != typeof(ReadOnlySpan<char>))
                    throw new AdapterTypeMismatchException(method, 2, true, 0, typeof(T1));
                if (typeof(T2) != typeof(ReadOnlySpan<char>))
                    throw new AdapterTypeMismatchException(method, 2, true, 1, typeof(T2));
                if (typeof(TRet) != typeof(IIdDataShareAdapter))
                    throw new AdapterTypeMismatchException(method, 2, true, -1, typeof(TRet));

                var identifier = Unsafe.As<T1, ReadOnlySpan<char>>(ref arg1);
                var name       = Unsafe.As<T2, ReadOnlySpan<char>>(ref arg2);

                if (!Manager.TryGetMod(identifier, name, out var mod) || mod is not TRet m)
                {
                    output = default;
                    return false;
                }

                output = m;
                return true;
            }
        }

        throw new AdapterMethodMissingException(method, 2, true);
    }
}
