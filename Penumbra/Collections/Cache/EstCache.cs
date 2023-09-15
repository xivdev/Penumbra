using OtterGui.Filesystem;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public struct EstCache : IDisposable
{
    private EstFile? _estFaceFile = null;
    private EstFile? _estHairFile = null;
    private EstFile? _estBodyFile = null;
    private EstFile? _estHeadFile = null;

    private readonly List<EstManipulation> _estManipulations = new();

    public EstCache()
    { }

    public void SetFiles(MetaFileManager manager)
    {
        manager.SetFile(_estFaceFile, MetaIndex.FaceEst);
        manager.SetFile(_estHairFile, MetaIndex.HairEst);
        manager.SetFile(_estBodyFile, MetaIndex.BodyEst);
        manager.SetFile(_estHeadFile, MetaIndex.HeadEst);
    }

    public void SetFile(MetaFileManager manager, MetaIndex index)
    {
        switch (index)
        {
            case MetaIndex.FaceEst:
                manager.SetFile(_estFaceFile, MetaIndex.FaceEst);
                break;
            case MetaIndex.HairEst:
                manager.SetFile(_estHairFile, MetaIndex.HairEst);
                break;
            case MetaIndex.BodyEst:
                manager.SetFile(_estBodyFile, MetaIndex.BodyEst);
                break;
            case MetaIndex.HeadEst:
                manager.SetFile(_estHeadFile, MetaIndex.HeadEst);
                break;
        }
    }

    public MetaList.MetaReverter TemporarilySetFiles(MetaFileManager manager, EstManipulation.EstType type)
    {
        var (file, idx) = type switch
        {
            EstManipulation.EstType.Face => (_estFaceFile, MetaIndex.FaceEst),
            EstManipulation.EstType.Hair => (_estHairFile, MetaIndex.HairEst),
            EstManipulation.EstType.Body => (_estBodyFile, MetaIndex.BodyEst),
            EstManipulation.EstType.Head => (_estHeadFile, MetaIndex.HeadEst),
            _                            => (null, 0),
        };

        return manager.TemporarilySetFile(file, idx);
    }

    public void Reset()
    {
        _estFaceFile?.Reset();
        _estHairFile?.Reset();
        _estBodyFile?.Reset();
        _estHeadFile?.Reset();
        _estManipulations.Clear();
    }

    public bool ApplyMod(MetaFileManager manager, EstManipulation m)
    {
        _estManipulations.AddOrReplace(m);
        var file = m.Slot switch
        {
            EstManipulation.EstType.Hair => _estHairFile ??= new EstFile(manager, EstManipulation.EstType.Hair),
            EstManipulation.EstType.Face => _estFaceFile ??= new EstFile(manager, EstManipulation.EstType.Face),
            EstManipulation.EstType.Body => _estBodyFile ??= new EstFile(manager, EstManipulation.EstType.Body),
            EstManipulation.EstType.Head => _estHeadFile ??= new EstFile(manager, EstManipulation.EstType.Head),
            _                            => throw new ArgumentOutOfRangeException(),
        };
        return m.Apply(file);
    }

    public bool RevertMod(MetaFileManager manager, EstManipulation m)
    {
        if (!_estManipulations.Remove(m))
            return false;

        var def   = EstFile.GetDefault(manager, m.Slot, Names.CombinedRace(m.Gender, m.Race), m.SetId);
        var manip = new EstManipulation(m.Gender, m.Race, m.Slot, m.SetId, def);
        var file = m.Slot switch
        {
            EstManipulation.EstType.Hair => _estHairFile!,
            EstManipulation.EstType.Face => _estFaceFile!,
            EstManipulation.EstType.Body => _estBodyFile!,
            EstManipulation.EstType.Head => _estHeadFile!,
            _                            => throw new ArgumentOutOfRangeException(),
        };
        return manip.Apply(file);
    }

    public void Dispose()
    {
        _estFaceFile?.Dispose();
        _estHairFile?.Dispose();
        _estBodyFile?.Dispose();
        _estHeadFile?.Dispose();
        _estFaceFile = null;
        _estHairFile = null;
        _estBodyFile = null;
        _estHeadFile = null;
        _estManipulations.Clear();
    }
}
