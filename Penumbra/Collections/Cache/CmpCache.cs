using OtterGui.Filesystem;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public struct CmpCache : IDisposable
{
    private          CmpFile?              _cmpFile          = null;
    private readonly List<RspManipulation> _cmpManipulations = new();

    public CmpCache()
    { }

    public void SetFiles(MetaFileManager manager)
        => manager.SetFile(_cmpFile, MetaIndex.HumanCmp);

    public MetaList.MetaReverter TemporarilySetFiles(MetaFileManager manager)
        => manager.TemporarilySetFile(_cmpFile, MetaIndex.HumanCmp);

    public void Reset()
    {
        if (_cmpFile == null)
            return;

        _cmpFile.Reset(_cmpManipulations.Select(m => (m.SubRace, m.Attribute)));
        _cmpManipulations.Clear();
    }

    public bool ApplyMod(MetaFileManager manager, RspManipulation manip)
    {
        _cmpManipulations.AddOrReplace(manip);
        _cmpFile ??= new CmpFile(manager);
        return manip.Apply(_cmpFile);
    }

    public bool RevertMod(MetaFileManager manager, RspManipulation manip)
    {
        if (!_cmpManipulations.Remove(manip))
            return false;

        var def = CmpFile.GetDefault(manager, manip.SubRace, manip.Attribute);
        manip = new RspManipulation(manip.SubRace, manip.Attribute, def);
        return manip.Apply(_cmpFile!);
    }

    public void Dispose()
    {
        _cmpFile?.Dispose();
        _cmpFile = null;
        _cmpManipulations.Clear();
    }
}
