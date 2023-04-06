namespace Penumbra.Collections.Manager;

public class CollectionManager
{
    public readonly CollectionStorage      Storage;
    public readonly ActiveCollections      Active;
    public readonly InheritanceManager     Inheritances;
    public readonly CollectionCacheManager Caches;
    public readonly TempCollectionManager  Temp;

    public CollectionManager(CollectionStorage storage, ActiveCollections active, InheritanceManager inheritances,
        CollectionCacheManager caches, TempCollectionManager temp)
    {
        Storage      = storage;
        Active       = active;
        Inheritances = inheritances;
        Caches       = caches;
        Temp         = temp;
    }
}
