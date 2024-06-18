using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class GmpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<GmpIdentifier, GmpEntry>(manager, collection)
{
    public void Reset()
        => Clear();

    protected override void Dispose(bool _)
        => Clear();
}
