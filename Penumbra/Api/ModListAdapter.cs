using Penumbra.Mods.Manager;

namespace Penumbra.Api;

public sealed class ModListAdapter(ModStorage storage) : IReadOnlyList<IDisposable>, IDisposable
{
    private readonly WeakReference<ModStorage> _storage = new(storage);

    public int Count
        => Storage.Count;

    public IDisposable this[int idx]
        => new ModAdapter(Storage[idx]);

    public IEnumerator<IDisposable> GetEnumerator()
        => Storage.Select(m => new ModAdapter(m)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private ModStorage Storage
    {
        get
        {
            if (_storage.TryGetTarget(out var storage))
                return storage;

            _storage.SetTarget(null!);
            throw new ObjectDisposedException("The reference to the ModStorage is invalid.");
        }
    }

    public void Dispose()
    {
        _storage.SetTarget(null!);
        GC.SuppressFinalize(this);
    }

    ~ModListAdapter()
        => Dispose();
}
