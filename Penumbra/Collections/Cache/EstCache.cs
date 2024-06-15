using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class EstCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<EstIdentifier, EstEntry>(manager, collection)
{
    private EstFile? _estFaceFile;
    private EstFile? _estHairFile;
    private EstFile? _estBodyFile;
    private EstFile? _estHeadFile;

    public override void SetFiles()
    {
        Manager.SetFile(_estFaceFile, MetaIndex.FaceEst);
        Manager.SetFile(_estHairFile, MetaIndex.HairEst);
        Manager.SetFile(_estBodyFile, MetaIndex.BodyEst);
        Manager.SetFile(_estHeadFile, MetaIndex.HeadEst);
    }

    public void SetFile(MetaIndex index)
    {
        switch (index)
        {
            case MetaIndex.FaceEst:
                Manager.SetFile(_estFaceFile, MetaIndex.FaceEst);
                break;
            case MetaIndex.HairEst:
                Manager.SetFile(_estHairFile, MetaIndex.HairEst);
                break;
            case MetaIndex.BodyEst:
                Manager.SetFile(_estBodyFile, MetaIndex.BodyEst);
                break;
            case MetaIndex.HeadEst:
                Manager.SetFile(_estHeadFile, MetaIndex.HeadEst);
                break;
        }
    }

    public MetaList.MetaReverter TemporarilySetFiles(EstType type)
    {
        var (file, idx) = type switch
        {
            EstType.Face => (_estFaceFile, MetaIndex.FaceEst),
            EstType.Hair => (_estHairFile, MetaIndex.HairEst),
            EstType.Body => (_estBodyFile, MetaIndex.BodyEst),
            EstType.Head => (_estHeadFile, MetaIndex.HeadEst),
            _            => (null, 0),
        };

        return Manager.TemporarilySetFile(file, idx);
    }

    public override void ResetFiles()
    {
        Manager.SetFile(null, MetaIndex.FaceEst);
        Manager.SetFile(null, MetaIndex.HairEst);
        Manager.SetFile(null, MetaIndex.BodyEst);
        Manager.SetFile(null, MetaIndex.HeadEst);
    }

    protected override void IncorporateChangesInternal()
    {
        if (!Manager.CharacterUtility.Ready)
            return;

        foreach (var (identifier, (_, entry)) in this)
            Apply(GetFile(identifier)!, identifier, entry);
        Penumbra.Log.Verbose($"{Collection.AnonymizedName}: Loaded {Count} delayed EST manipulations.");
    }

    public EstEntry GetEstEntry(EstIdentifier identifier)
    {
        var file = GetFile(identifier);
        return file != null
            ? file[identifier.GenderRace, identifier.SetId]
            : EstFile.GetDefault(Manager, identifier);
    }

    public override void Reset()
    {
        _estFaceFile?.Reset();
        _estHairFile?.Reset();
        _estBodyFile?.Reset();
        _estHeadFile?.Reset();
        Clear();
    }

    protected override void ApplyModInternal(EstIdentifier identifier, EstEntry entry)
    {
        if (GetFile(identifier) is { } file)
            Apply(file, identifier, entry);
    }

    protected override void RevertModInternal(EstIdentifier identifier)
    {
        if (GetFile(identifier) is { } file)
            Apply(file, identifier, EstFile.GetDefault(Manager, identifier.Slot, identifier.GenderRace, identifier.SetId));
    }

    public static bool Apply(EstFile file, EstIdentifier identifier, EstEntry entry)
        => file.SetEntry(identifier.GenderRace, identifier.SetId, entry) switch
        {
            EstFile.EstEntryChange.Unchanged => false,
            EstFile.EstEntryChange.Changed   => true,
            EstFile.EstEntryChange.Added     => true,
            EstFile.EstEntryChange.Removed   => true,
            _                                => false,
        };

    protected override void Dispose(bool _)
    {
        _estFaceFile?.Dispose();
        _estHairFile?.Dispose();
        _estBodyFile?.Dispose();
        _estHeadFile?.Dispose();
        _estFaceFile = null;
        _estHairFile = null;
        _estBodyFile = null;
        _estHeadFile = null;
        Clear();
    }

    private EstFile? GetFile(EstIdentifier identifier)
    {
        if (Manager.CharacterUtility.Ready)
            return null;

        return identifier.Slot switch
        {
            EstType.Hair => _estHairFile ??= new EstFile(Manager, EstType.Hair),
            EstType.Face => _estFaceFile ??= new EstFile(Manager, EstType.Face),
            EstType.Body => _estBodyFile ??= new EstFile(Manager, EstType.Body),
            EstType.Head => _estHeadFile ??= new EstFile(Manager, EstType.Head),
            _            => null,
        };
    }
}
