using Luna;
using Penumbra.Mods;
using Penumbra.String.Classes;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Data;
using Penumbra.Mods.Editor;

namespace Penumbra.Collections;

public partial class ModCollection
{
    // Only active collections need to have a cache.
    public CollectionCache? Cache;

    public bool HasCache
        => Cache != null;


    // Handle temporary mods for this collection.
    public void Apply(TemporaryMod tempMod, bool created)
    {
        if (created)
            Cache?.AddMod(tempMod, tempMod.TotalManipulations > 0);
        else
            Cache?.ReloadMod(tempMod, tempMod.TotalManipulations > 0);
    }

    public void Remove(TemporaryMod tempMod)
    {
        Cache?.RemoveMod(tempMod, tempMod.TotalManipulations > 0);
    }

    public IEnumerable<Utf8GamePath> ReverseResolvePath(FullPath path)
        => Cache?.ReverseResolvePath(path) ?? [];

    public HashSet<Utf8GamePath>[] ReverseResolvePaths(IReadOnlyCollection<string> paths)
        => Cache?.ReverseResolvePaths(paths) ?? paths.Select(_ => new HashSet<Utf8GamePath>()).ToArray();

    public FullPath? ResolvePath(Utf8GamePath path)
        => Cache?.ResolvePath(path);

    // Obtain data from the cache.
    internal MetaCache? MetaCache
        => Cache?.Meta;

    internal IReadOnlyDictionary<Utf8GamePath, ModPath> ResolvedFiles
        => Cache?.ResolvedFiles ?? new ConcurrentDictionary<Utf8GamePath, ModPath>();

    internal IReadOnlyDictionary<string, (SingleArray<IMod>, IIdentifiedObjectData)> ChangedItems
        => Cache?.ChangedItems ?? new Dictionary<string, (SingleArray<IMod>, IIdentifiedObjectData)>();

    internal IEnumerable<SingleArray<ModConflicts>> AllConflicts
        => Cache?.AllConflicts ?? [];

    internal SingleArray<ModConflicts> Conflicts(Mod mod)
        => Cache?.Conflicts(mod) ?? new SingleArray<ModConflicts>();
}
