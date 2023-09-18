using OtterGui;
using OtterGui.Filesystem;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public readonly struct EqdpCache : IDisposable
{
    private readonly ExpandedEqdpFile?[]    _eqdpFiles = new ExpandedEqdpFile[CharacterUtilityData.EqdpIndices.Length]; // TODO: female Hrothgar
    private readonly List<EqdpManipulation> _eqdpManipulations = new();

    public EqdpCache()
    { }

    public void SetFiles(MetaFileManager manager)
    {
        for (var i = 0; i < CharacterUtilityData.EqdpIndices.Length; ++i)
            manager.SetFile(_eqdpFiles[i], CharacterUtilityData.EqdpIndices[i]);
    }

    public void SetFile(MetaFileManager manager, MetaIndex index)
    {
        var i = CharacterUtilityData.EqdpIndices.IndexOf(index);
        if (i != -1)
            manager.SetFile(_eqdpFiles[i], index);
    }

    public MetaList.MetaReverter TemporarilySetFiles(MetaFileManager manager, GenderRace genderRace, bool accessory)
    {
        var idx = CharacterUtilityData.EqdpIdx(genderRace, accessory);
        Debug.Assert(idx >= 0, $"Invalid Gender, Race or Accessory for EQDP file {genderRace}, {accessory}.");
        var i = CharacterUtilityData.EqdpIndices.IndexOf(idx);
        Debug.Assert(i >= 0, $"Invalid Gender, Race or Accessory for EQDP file {genderRace}, {accessory}.");
        return manager.TemporarilySetFile(_eqdpFiles[i], idx);
    }

    public void Reset()
    {
        foreach (var file in _eqdpFiles.OfType<ExpandedEqdpFile>())
        {
            var relevant = CharacterUtility.RelevantIndices[file.Index.Value];
            file.Reset(_eqdpManipulations.Where(m => m.FileIndex() == relevant).Select(m => (SetId)m.SetId));
        }

        _eqdpManipulations.Clear();
    }

    public bool ApplyMod(MetaFileManager manager, EqdpManipulation manip)
    {
        _eqdpManipulations.AddOrReplace(manip);
        var file = _eqdpFiles[Array.IndexOf(CharacterUtilityData.EqdpIndices, manip.FileIndex())] ??=
            new ExpandedEqdpFile(manager, Names.CombinedRace(manip.Gender, manip.Race), manip.Slot.IsAccessory()); // TODO: female Hrothgar
        return manip.Apply(file);
    }

    public bool RevertMod(MetaFileManager manager, EqdpManipulation manip)
    {
        if (!_eqdpManipulations.Remove(manip))
            return false;

        var def  = ExpandedEqdpFile.GetDefault(manager, Names.CombinedRace(manip.Gender, manip.Race), manip.Slot.IsAccessory(), manip.SetId);
        var file = _eqdpFiles[Array.IndexOf(CharacterUtilityData.EqdpIndices, manip.FileIndex())]!;
        manip = new EqdpManipulation(def, manip.Slot, manip.Gender, manip.Race, manip.SetId);
        return manip.Apply(file);
    }

    public ExpandedEqdpFile? EqdpFile(GenderRace race, bool accessory)
        => _eqdpFiles
            [Array.IndexOf(CharacterUtilityData.EqdpIndices, CharacterUtilityData.EqdpIdx(race, accessory))]; // TODO: female Hrothgar

    public void Dispose()
    {
        for (var i = 0; i < _eqdpFiles.Length; ++i)
        {
            _eqdpFiles[i]?.Dispose();
            _eqdpFiles[i] = null;
        }

        _eqdpManipulations.Clear();
    }
}
