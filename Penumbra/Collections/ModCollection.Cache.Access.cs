using OtterGui.Classes;
using Penumbra.Mods;
using Penumbra.String.Classes;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Data;
using Penumbra.Mods.Editor;

namespace Penumbra.Collections;

public partial class ModCollection
{
    // Only active collections need to have a cache.
    internal CollectionCache? _cache;

    public bool HasCache
        => _cache != null;


    // Handle temporary mods for this collection.
    public void Apply(TemporaryMod tempMod, bool created)
    {
        if (created)
            _cache?.AddMod(tempMod, tempMod.TotalManipulations > 0);
        else
            _cache?.ReloadMod(tempMod, tempMod.TotalManipulations > 0);
    }

    public void Remove(TemporaryMod tempMod)
    {
        _cache?.RemoveMod(tempMod, tempMod.TotalManipulations > 0);
    }

    public IEnumerable<Utf8GamePath> ReverseResolvePath(FullPath path)
        => _cache?.ReverseResolvePath(path) ?? Array.Empty<Utf8GamePath>();

    public HashSet<Utf8GamePath>[] ReverseResolvePaths(IReadOnlyCollection<string> paths)
        => _cache?.ReverseResolvePaths(paths) ?? paths.Select(_ => new HashSet<Utf8GamePath>()).ToArray();

    public FullPath? ResolvePath(Utf8GamePath path)
        => _cache?.ResolvePath(path);

    // Obtain data from the cache.
    internal MetaCache? MetaCache
        => _cache?.Meta;

    internal IReadOnlyDictionary<Utf8GamePath, ModPath> ResolvedFiles
        => _cache?.ResolvedFiles ?? new ConcurrentDictionary<Utf8GamePath, ModPath>();

    internal IReadOnlyDictionary<string, (SingleArray<IMod>, IIdentifiedObjectData)> ChangedItems
        => _cache?.ChangedItems ?? new Dictionary<string, (SingleArray<IMod>, IIdentifiedObjectData)>();

    internal IEnumerable<SingleArray<ModConflicts>> AllConflicts
        => _cache?.AllConflicts ?? Array.Empty<SingleArray<ModConflicts>>();

    internal SingleArray<ModConflicts> Conflicts(Mod mod)
        => _cache?.Conflicts(mod) ?? new SingleArray<ModConflicts>();
}
