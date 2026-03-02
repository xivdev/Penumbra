using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.Api;

public sealed class ModAdapter(Mod mod) : IReadOnlyDictionary<string, object?>, IDisposable
{
    private readonly WeakReference<Mod> _mod = new(mod);

    bool IReadOnlyDictionary<string, object?>.TryGetValue(string key, out object? value)
    {
        (var ret, value) = key switch
        {
            nameof(Mod.ModPath)          => (true, Mod.ModPath),
            nameof(Mod.Index)            => (true, Mod.Index),
            nameof(Mod.Name)             => (true, Mod.Name),
            nameof(Mod.Identifier)       => (true, Mod.Identifier),
            nameof(Mod.Author)           => (true, Mod.Author),
            nameof(Mod.Description)      => (true, Mod.Description),
            nameof(Mod.Version)          => (true, Mod.Version),
            nameof(Mod.Website)          => (true, Mod.Website),
            nameof(Mod.Image)            => (true, Mod.Image),
            nameof(Mod.ModTags)          => (true, Mod.ModTags),
            nameof(Mod.RequiredFeatures) => (true, (ulong)Mod.RequiredFeatures),
            nameof(Mod.Path.SortName)    => (true, Mod.Path.SortName),
            nameof(Mod.Path.Folder)      => (true, Mod.Path.Folder),
            "FullPath"                   => (true, Mod.Path.CurrentPath),
            nameof(Mod.ImportDate)       => (true, DateTimeOffset.FromUnixTimeMilliseconds(Mod.ImportDate)),
            nameof(Mod.LastConfigEdit)   => (true, DateTimeOffset.FromUnixTimeMilliseconds(Mod.LastConfigEdit)),
            nameof(Mod.LocalTags)        => (true, Mod.LocalTags),
            nameof(Mod.Favorite)         => (true, Mod.Favorite),
            _                            => (false, (object?)null),
        };
        return ret;
    }

    object? IReadOnlyDictionary<string, object?>.this[string key]
        => ((IReadOnlyDictionary<string, object?>)this).TryGetValue(key, out var v)
            ? v
            : throw new ArgumentOutOfRangeException($"The key {key} is not a valid property of a Mod.");

    bool IReadOnlyDictionary<string, object?>.ContainsKey(string key)
        => throw new NotImplementedException(
            "Mods only implement IReadOnlyDictionary for IPC and only TryGetValue and the []-accessor are supported.");

    IEnumerable<string> IReadOnlyDictionary<string, object?>.Keys
        => throw new NotImplementedException(
            "Mods only implement IReadOnlyDictionary for IPC and only TryGetValue and the []-accessor are supported.");

    IEnumerable<object?> IReadOnlyDictionary<string, object?>.Values
        => throw new NotImplementedException(
            "Mods only implement IReadOnlyDictionary for IPC and only TryGetValue and the []-accessor are supported.");

    IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
        => throw new NotImplementedException(
            "Mods only implement IReadOnlyDictionary for IPC and only TryGetValue and the []-accessor are supported.");

    IEnumerator IEnumerable.GetEnumerator()
        => throw new NotImplementedException(
            "Mods only implement IReadOnlyDictionary for IPC and only TryGetValue and the []-accessor are supported.");

    int IReadOnlyCollection<KeyValuePair<string, object?>>.Count
        => throw new NotImplementedException(
            "Mods only implement IReadOnlyDictionary for IPC and only TryGetValue and the []-accessor are supported.");

    private Mod Mod
    {
        get
        {
            if (_mod.TryGetTarget(out var mod))
                return mod;

            _mod.SetTarget(null!);
            throw new ObjectDisposedException("The reference to the Mod is invalid.");
        }
    }

    public void Dispose()
    {
        _mod.SetTarget(null!);
        GC.SuppressFinalize(this);
    }

    ~ModAdapter()
        => Dispose();
}

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
