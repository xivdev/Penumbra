using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Plugin;

namespace Penumbra.GameData.Data;

/// <summary>
/// A list sorting objects based on a key which then allows efficiently finding all objects between a pair of keys via binary search.
/// </summary>
public abstract class KeyList<T>
{
    private readonly List<(ulong Key, T Data)> _list;

    public IReadOnlyList<(ulong Key, T Data)> List
        => _list;

    /// <summary>
    /// Iterate over all objects between the given minimal and maximal keys (inclusive).
    /// </summary>
    protected IEnumerable<T> Between(ulong minKey, ulong maxKey)
    {
        var (minIdx, maxIdx) = GetMinMax(minKey, maxKey);
        if (minIdx < 0)
            yield break;

        for (var i = minIdx; i <= maxIdx; ++i)
            yield return _list[i].Data;
    }

    private (int MinIdx, int MaxIdx) GetMinMax(ulong minKey, ulong maxKey)
    {
        var idx    = _list.BinarySearch((minKey, default!), ListComparer);
        var minIdx = idx;
        if (minIdx < 0)
        {
            minIdx = ~minIdx;
            if (minIdx == _list.Count || _list[minIdx].Key > maxKey)
                return (-1, -1);

            idx = minIdx;
        }
        else
        {
            while (minIdx > 0 && _list[minIdx - 1].Key >= minKey)
                --minIdx;
        }

        if (_list[minIdx].Key < minKey || _list[minIdx].Key > maxKey)
            return (-1, -1);


        var maxIdx = _list.BinarySearch(idx, _list.Count - idx, (maxKey, default!), ListComparer);
        if (maxIdx < 0)
        {
            maxIdx = ~maxIdx;
            return maxIdx > minIdx ? (minIdx, maxIdx - 1) : (-1, -1);
        }

        while (maxIdx < _list.Count - 1 && _list[maxIdx + 1].Key <= maxKey)
            ++maxIdx;

        if (_list[maxIdx].Key < minKey || _list[maxIdx].Key > maxKey)
            return (-1, -1);

        return (minIdx, maxIdx);
    }

    /// <summary>
    /// The function turning an object to (potentially multiple) keys. Only used during construction.
    /// </summary>
    protected abstract IEnumerable<ulong> ToKeys(T data);

    /// <summary>
    /// Whether a returned key is valid. Only used during construction.
    /// </summary>
    protected abstract bool ValidKey(ulong key);

    /// <summary>
    /// How multiple items with the same key should be sorted.
    /// </summary>
    protected abstract int ValueKeySelector(T data);

    protected KeyList(DalamudPluginInterface pi, string tag, ClientLanguage language, int version, IEnumerable<T> data)
    {
        _list = DataSharer.TryCatchData(pi, tag, language, version,
            () => data.SelectMany(d => ToKeys(d).Select(k => (k, d)))
                .Where(p => ValidKey(p.k))
                .OrderBy(p => p.k)
                .ThenBy(p => ValueKeySelector(p.d))
                .ToList());
    }

    private class Comparer : IComparer<(ulong, T)>
    {
        public int Compare((ulong, T) x, (ulong, T) y)
            => x.Item1.CompareTo(y.Item1);
    }

    private static readonly Comparer ListComparer = new();
}
