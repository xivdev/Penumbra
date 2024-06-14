using Penumbra.GameData.Structs;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class GmpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<GmpIdentifier, GmpEntry>(manager, collection)
{
    private ExpandedGmpFile? _gmpFile;

    public override void SetFiles()
        => Manager.SetFile(_gmpFile, MetaIndex.Gmp);

    public override void ResetFiles()
        => Manager.SetFile(null, MetaIndex.Gmp);

    protected override void IncorporateChangesInternal()
    {
        if (GetFile() is not { } file)
            return;

        foreach (var (identifier, (_, entry)) in this)
            Apply(file, identifier, entry);

        Penumbra.Log.Verbose($"{Collection.AnonymizedName}: Loaded {Count} delayed GMP manipulations.");
    }

    public MetaList.MetaReverter TemporarilySetFile()
        => Manager.TemporarilySetFile(_gmpFile, MetaIndex.Gmp);

    public override void Reset()
    {
        if (_gmpFile == null)
            return;

        _gmpFile.Reset(Keys.Select(identifier => identifier.SetId));
        Clear();
    }

    protected override void ApplyModInternal(GmpIdentifier identifier, GmpEntry entry)
    {
        if (GetFile() is { } file)
            Apply(file, identifier, entry);
    }

    protected override void RevertModInternal(GmpIdentifier identifier)
    {
        if (GetFile() is { } file)
            Apply(file, identifier, ExpandedGmpFile.GetDefault(Manager, identifier.SetId));
    }

    public static bool Apply(ExpandedGmpFile file, GmpIdentifier identifier, GmpEntry entry)
    {
        var origEntry = file[identifier.SetId];
        if (entry == origEntry)
            return false;

        file[identifier.SetId] = entry;
        return true;
    }

    protected override void Dispose(bool _)
    {
        _gmpFile?.Dispose();
        _gmpFile = null;
        Clear();
    }

    private ExpandedGmpFile? GetFile()
    {
        if (_gmpFile != null)
            return _gmpFile;

        if (!Manager.CharacterUtility.Ready)
            return null;

        return _gmpFile = new ExpandedGmpFile(Manager);
    }
}
