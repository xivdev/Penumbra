using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class EstCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<EstIdentifier, EstEntry>(manager, collection)
{
    public override void SetFiles()
    { }

    protected override void IncorporateChangesInternal()
    { }

    public EstEntry GetEstEntry(EstIdentifier identifier)
        => TryGetValue(identifier, out var entry)
            ? entry.Entry
            : EstFile.GetDefault(Manager, identifier);

    public void Reset()
        => Clear();

    protected override void ApplyModInternal(EstIdentifier identifier, EstEntry entry)
    { }

    protected override void RevertModInternal(EstIdentifier identifier)
    { }

    protected override void Dispose(bool _)
        => Clear();
}
