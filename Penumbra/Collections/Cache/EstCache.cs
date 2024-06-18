using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class EstCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<EstIdentifier, EstEntry>(manager, collection)
{
    public EstEntry GetEstEntry(EstIdentifier identifier)
        => TryGetValue(identifier, out var entry)
            ? entry.Entry
            : EstFile.GetDefault(Manager, identifier);

    public void Reset()
        => Clear();

    protected override void Dispose(bool _)
        => Clear();
}
