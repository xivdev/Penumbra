using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.String.Classes;

namespace Penumbra.Collections.Cache;

/// <summary>
///     Contains associations between a mod and the paths and meta manipulations affected by that mod.
/// </summary>
public class CollectionModData
{
    private readonly Dictionary<IMod, (HashSet<Utf8GamePath>, HashSet<MetaManipulation>)> _data = new();

    public IEnumerable<(IMod, IReadOnlySet<Utf8GamePath>, IReadOnlySet<MetaManipulation>)> Data
        => _data.Select(kvp => (kvp.Key, (IReadOnlySet<Utf8GamePath>)kvp.Value.Item1, (IReadOnlySet<MetaManipulation>)kvp.Value.Item2));

    public (IReadOnlyCollection<Utf8GamePath> Paths, IReadOnlyCollection<MetaManipulation> Manipulations) RemoveMod(IMod mod)
    {
        if (_data.Remove(mod, out var data))
            return data;

        return ([], []);
    }

    public void AddPath(IMod mod, Utf8GamePath path)
    {
        if (_data.TryGetValue(mod, out var data))
        {
            data.Item1.Add(path);
        }
        else
        {
            data = ([path], []);
            _data.Add(mod, data);
        }
    }

    public void AddManip(IMod mod, MetaManipulation manipulation)
    {
        if (_data.TryGetValue(mod, out var data))
        {
            data.Item2.Add(manipulation);
        }
        else
        {
            data = ([], [manipulation]);
            _data.Add(mod, data);
        }
    }

    public void RemovePath(IMod mod, Utf8GamePath path)
    {
        if (_data.TryGetValue(mod, out var data) && data.Item1.Remove(path) && data.Item1.Count == 0 && data.Item2.Count == 0)
            _data.Remove(mod);
    }

    public void RemoveManip(IMod mod, MetaManipulation manip)
    {
        if (_data.TryGetValue(mod, out var data) && data.Item2.Remove(manip) && data.Item1.Count == 0 && data.Item2.Count == 0)
            _data.Remove(mod);
    }
}
