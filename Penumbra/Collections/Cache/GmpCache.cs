using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class GmpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<GmpIdentifier, GmpEntry>(manager, collection)
{
    public override void SetFiles()
    { }

    protected override void IncorporateChangesInternal()
    { }

    public void Reset()
        => Clear();

    protected override void ApplyModInternal(GmpIdentifier identifier, GmpEntry entry)
    { }

    protected override void RevertModInternal(GmpIdentifier identifier)
    { }

    public static bool Apply(ExpandedGmpFile file, GmpIdentifier identifier, GmpEntry entry)
    {
        var origEntry = file[identifier.SetId];
        if (entry == origEntry)
            return false;

        file[identifier.SetId] = entry;
        return true;
    }

    protected override void Dispose(bool _)
        => Clear();
}
