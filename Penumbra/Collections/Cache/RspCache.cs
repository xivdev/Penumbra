using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class RspCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<RspIdentifier, RspEntry>(manager, collection)
{
    public void Reset()
        => Clear();

    protected override void Dispose(bool _)
        => Clear();
}
