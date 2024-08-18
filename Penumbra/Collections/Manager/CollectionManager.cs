using OtterGui.Services;
using Penumbra.Collections.Cache;

namespace Penumbra.Collections.Manager;

public class CollectionManager(
    CollectionStorage storage,
    ActiveCollections active,
    InheritanceManager inheritances,
    CollectionCacheManager caches,
    TempCollectionManager temp,
    CollectionEditor editor) : IService
{
    public readonly CollectionStorage      Storage      = storage;
    public readonly ActiveCollections      Active       = active;
    public readonly InheritanceManager     Inheritances = inheritances;
    public readonly CollectionCacheManager Caches       = caches;
    public readonly TempCollectionManager  Temp         = temp;
    public readonly CollectionEditor       Editor       = editor;
}
