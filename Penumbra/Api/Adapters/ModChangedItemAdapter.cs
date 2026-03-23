using Penumbra.GameData.Data;
using Penumbra.Mods.Manager;

namespace Penumbra.Api;

public sealed class ModChangedItemAdapter(WeakReference<ModStorage> storage)
    : IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>,
        IReadOnlyList<(string ModDirectory, IReadOnlyDictionary<string, object?> ChangedItems)>
{
    IEnumerator<(string ModDirectory, IReadOnlyDictionary<string, object?> ChangedItems)>
        IEnumerable<(string ModDirectory, IReadOnlyDictionary<string, object?> ChangedItems)>.GetEnumerator()
        => Storage.Select(m => (m.Identifier, (IReadOnlyDictionary<string, object?>)new ChangedItemDictionaryAdapter(m.ChangedItems)))
            .GetEnumerator();

    public IEnumerator<KeyValuePair<string, IReadOnlyDictionary<string, object?>>> GetEnumerator()
        => Storage.Select(m => new KeyValuePair<string, IReadOnlyDictionary<string, object?>>(m.Identifier,
                new ChangedItemDictionaryAdapter(m.ChangedItems)))
            .GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => Storage.Count;

    public bool ContainsKey(string key)
        => Storage.TryGetMod(key, string.Empty, out _);

    public bool TryGetValue(string key, [NotNullWhen(true)] out IReadOnlyDictionary<string, object?>? value)
    {
        if (Storage.TryGetMod(key, string.Empty, out var mod))
        {
            value = new ChangedItemDictionaryAdapter(mod.ChangedItems);
            return true;
        }

        value = null;
        return false;
    }

    public IReadOnlyDictionary<string, object?> this[string key]
        => TryGetValue(key, out var v) ? v : throw new KeyNotFoundException();

    (string ModDirectory, IReadOnlyDictionary<string, object?> ChangedItems)
        IReadOnlyList<(string ModDirectory, IReadOnlyDictionary<string, object?> ChangedItems)>.this[int index]
    {
        get
        {
            var m = Storage[index];
            return (m.Identifier, new ChangedItemDictionaryAdapter(m.ChangedItems));
        }
    }

    public IEnumerable<string> Keys
        => Storage.Select(m => m.Identifier);

    public IEnumerable<IReadOnlyDictionary<string, object?>> Values
        => Storage.Select(m => new ChangedItemDictionaryAdapter(m.ChangedItems));

    private ModStorage Storage
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => storage.TryGetTarget(out var t)
            ? t
            : throw new ObjectDisposedException("The underlying mod storage of this IPC container was disposed.");
    }

    private sealed class ChangedItemDictionaryAdapter(SortedList<string, IIdentifiedObjectData> data) : IReadOnlyDictionary<string, object?>
    {
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
            => data.Select(d => new KeyValuePair<string, object?>(d.Key, d.Value?.ToInternalObject())).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public int Count
            => data.Count;

        public bool ContainsKey(string key)
            => data.ContainsKey(key);

        public bool TryGetValue(string key, out object? value)
        {
            if (data.TryGetValue(key, out var v))
            {
                value = v?.ToInternalObject();
                return true;
            }

            value = null;
            return false;
        }

        public object? this[string key]
            => data[key]?.ToInternalObject();

        public IEnumerable<string> Keys
            => data.Keys;

        public IEnumerable<object?> Values
            => data.Values.Select(v => v?.ToInternalObject());
    }
}
