using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class RspCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<RspIdentifier, RspEntry>(manager, collection)
{
    public override void SetFiles()
    { }

    protected override void IncorporateChangesInternal()
    { }

    public void Reset()
        => Clear();

    protected override void ApplyModInternal(RspIdentifier identifier, RspEntry entry)
    { }

    protected override void RevertModInternal(RspIdentifier identifier)
    { }


    protected override void Dispose(bool _)
        => Clear();
}
