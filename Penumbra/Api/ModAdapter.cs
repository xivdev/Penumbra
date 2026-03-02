using ImSharp;
using Penumbra.Api.Enums;
using Penumbra.Mods;

namespace Penumbra.Api;

public sealed class ModAdapter(Mod mod) : IReadOnlyList<object?>, IDisposable
{
    private readonly WeakReference<Mod> _mod = new(mod);

    object? IReadOnlyList<object?>.this[int index]
        => index switch
        {
            (int)ModProperty.ModPath          => Mod.ModPath,
            (int)ModProperty.Index            => Mod.Index,
            (int)ModProperty.Name             => Mod.Name,
            (int)ModProperty.Identifier       => Mod.Identifier,
            (int)ModProperty.Author           => Mod.Author,
            (int)ModProperty.Description      => Mod.Description,
            (int)ModProperty.Version          => Mod.Version,
            (int)ModProperty.Website          => Mod.Website,
            (int)ModProperty.Image            => Mod.Image,
            (int)ModProperty.ModTags          => Mod.ModTags,
            (int)ModProperty.RequiredFeatures => (ulong)Mod.RequiredFeatures,
            (int)ModProperty.SortName         => Mod.Path.SortName,
            (int)ModProperty.Folder           => Mod.Path.Folder,
            (int)ModProperty.FullPath         => Mod.Path.CurrentPath,
            (int)ModProperty.ImportDate       => DateTimeOffset.FromUnixTimeMilliseconds(Mod.ImportDate),
            (int)ModProperty.LastConfigEdit   => DateTimeOffset.FromUnixTimeMilliseconds(Mod.LastConfigEdit),
            (int)ModProperty.LocalTags        => Mod.LocalTags,
            (int)ModProperty.Favorite         => Mod.Favorite,
            _                                 => throw new ArgumentOutOfRangeException($"Invalid ModProperty {index}."),
        };


    IEnumerator<object?> IEnumerable<object?>.GetEnumerator()
        => ModProperty.Values.Select(i => ((IReadOnlyList<object?>)this)[(int)i]).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable<object?>)this).GetEnumerator();

    int IReadOnlyCollection<object?>.Count
        => ModProperty.Values.Count;

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
