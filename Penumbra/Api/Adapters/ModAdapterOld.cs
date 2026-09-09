using ImSharp;
using Penumbra.Api.Wrappers;
using Penumbra.Mods;

namespace Penumbra.Api; 

public sealed class ModAdapterOld(Mod mod) : IReadOnlyList<object?>, IDisposable
{
    private readonly WeakReference<Mod> _mod = new(mod);

    object? IReadOnlyList<object?>.this[int index]
        => index switch
        {
            (int)ModWrapper.Method.ModPath          => Mod.ModPath,
            (int)ModWrapper.Method.Index            => Mod.Index,
            (int)ModWrapper.Method.Name             => Mod.Name,
            (int)ModWrapper.Method.Identifier       => Mod.Identifier,
            (int)ModWrapper.Method.Author           => Mod.Author,
            (int)ModWrapper.Method.Description      => Mod.Description,
            (int)ModWrapper.Method.ModVersion          => Mod.Version,
            (int)ModWrapper.Method.Website          => Mod.Website,
            (int)ModWrapper.Method.Image            => Mod.Image,
            (int)ModWrapper.Method.ModTags          => Mod.ModTags,
            (int)ModWrapper.Method.RequiredFeatures => (ulong)Mod.RequiredFeatures,
            (int)ModWrapper.Method.SortName         => Mod.Path.SortName,
            (int)ModWrapper.Method.Folder           => Mod.Path.Folder,
            (int)ModWrapper.Method.FullPath         => Mod.Path.CurrentPath,
            (int)ModWrapper.Method.ImportDate       => DateTimeOffset.FromUnixTimeMilliseconds(Mod.ImportDate),
            (int)ModWrapper.Method.LastConfigEdit   => DateTimeOffset.FromUnixTimeMilliseconds(Mod.LastConfigEdit),
            (int)ModWrapper.Method.LocalTags        => Mod.LocalTags,
            (int)ModWrapper.Method.Favorite         => Mod.Favorite,
            _                                 => throw new ArgumentOutOfRangeException($"Invalid ModProperty {index}."),
        };


    IEnumerator<object?> IEnumerable<object?>.GetEnumerator()
        => ModWrapper.Method.Values.Select(i => ((IReadOnlyList<object?>)this)[(int)i]).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable<object?>)this).GetEnumerator();

    int IReadOnlyCollection<object?>.Count
        => ModWrapper.Method.Values.Count;

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
        => _mod.SetTarget(null!);
}
