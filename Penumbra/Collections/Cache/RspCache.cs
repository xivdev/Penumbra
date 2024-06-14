using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class RspCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<RspIdentifier, RspEntry>(manager, collection)
{
    private CmpFile? _cmpFile;

    public override void SetFiles()
        => Manager.SetFile(_cmpFile, MetaIndex.HumanCmp);

    public override void ResetFiles()
        => Manager.SetFile(null, MetaIndex.HumanCmp);

    protected override void IncorporateChangesInternal()
    {
        if (GetFile() is not { } file)
            return;

        foreach (var (identifier, (_, entry)) in this)
            Apply(file, identifier, entry);

        Penumbra.Log.Verbose($"{Collection.AnonymizedName}: Loaded {Count} delayed RSP manipulations.");
    }

    public MetaList.MetaReverter TemporarilySetFile()
        => Manager.TemporarilySetFile(_cmpFile, MetaIndex.HumanCmp);

    public override void Reset()
    {
        if (_cmpFile == null)
            return;

        _cmpFile.Reset(Keys.Select(identifier => (identifier.SubRace, identifier.Attribute)));
        Clear();
    }

    protected override void ApplyModInternal(RspIdentifier identifier, RspEntry entry)
    {
        if (GetFile() is { } file)
            Apply(file, identifier, entry);
    }

    protected override void RevertModInternal(RspIdentifier identifier)
    {
        if (GetFile() is { } file)
            Apply(file, identifier, CmpFile.GetDefault(Manager, identifier.SubRace, identifier.Attribute));
    }

    public static bool Apply(CmpFile file, RspIdentifier identifier, RspEntry entry)
    {
        var value = file[identifier.SubRace, identifier.Attribute];
        if (value == entry)
            return false;

        file[identifier.SubRace, identifier.Attribute] = entry;
        return true;
    }

    protected override void Dispose(bool _)
    {
        _cmpFile?.Dispose();
        _cmpFile = null;
        Clear();
    }

    private CmpFile? GetFile()
    {
        if (_cmpFile != null)
            return _cmpFile;

        if (!Manager.CharacterUtility.Ready)
            return null;

        return _cmpFile = new CmpFile(Manager);
    }
}
