using OtterGui;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class EqdpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<EqdpIdentifier, EqdpEntry>(manager, collection)
{
    private readonly ExpandedEqdpFile?[] _eqdpFiles = new ExpandedEqdpFile[CharacterUtilityData.EqdpIndices.Length]; // TODO: female Hrothgar

    public override void SetFiles()
    {
        for (var i = 0; i < CharacterUtilityData.EqdpIndices.Length; ++i)
            Manager.SetFile(_eqdpFiles[i], CharacterUtilityData.EqdpIndices[i]);
    }

    public void SetFile(MetaIndex index)
    {
        var i = CharacterUtilityData.EqdpIndices.IndexOf(index);
        if (i != -1)
            Manager.SetFile(_eqdpFiles[i], index);
    }

    public override void ResetFiles()
    {
        foreach (var t in CharacterUtilityData.EqdpIndices)
            Manager.SetFile(null, t);
    }

    protected override void IncorporateChangesInternal()
    {
        foreach (var (identifier, (_, entry)) in this)
            Apply(GetFile(identifier)!, identifier, entry);

        Penumbra.Log.Verbose($"{Collection.AnonymizedName}: Loaded {Count} delayed EQDP manipulations.");
    }

    public ExpandedEqdpFile? EqdpFile(GenderRace race, bool accessory)
        => _eqdpFiles[Array.IndexOf(CharacterUtilityData.EqdpIndices, CharacterUtilityData.EqdpIdx(race, accessory))]; // TODO: female Hrothgar

    public MetaList.MetaReverter? TemporarilySetFile(GenderRace genderRace, bool accessory)
    {
        var idx = CharacterUtilityData.EqdpIdx(genderRace, accessory);
        if (idx < 0)
        {
            Penumbra.Log.Warning($"Invalid Gender, Race or Accessory for EQDP file {genderRace}, {accessory}.");
            return null;
        }

        var i = CharacterUtilityData.EqdpIndices.IndexOf(idx);
        if (i < 0)
        {
            Penumbra.Log.Warning($"Invalid Gender, Race or Accessory for EQDP file {genderRace}, {accessory}.");
            return null;
        }

        return Manager.TemporarilySetFile(_eqdpFiles[i], idx);
    }

    public override void Reset()
    {
        foreach (var file in _eqdpFiles.OfType<ExpandedEqdpFile>())
        {
            var relevant = CharacterUtility.RelevantIndices[file.Index.Value];
            file.Reset(Keys.Where(m => m.FileIndex() == relevant).Select(m => m.SetId));
        }

        Clear();
    }

    protected override void ApplyModInternal(EqdpIdentifier identifier, EqdpEntry entry)
    {
        if (GetFile(identifier) is { } file)
            Apply(file, identifier, entry);
    }

    protected override void RevertModInternal(EqdpIdentifier identifier)
    {
        if (GetFile(identifier) is { } file)
            Apply(file, identifier, ExpandedEqdpFile.GetDefault(Manager, identifier));
    }

    public static bool Apply(ExpandedEqdpFile file, EqdpIdentifier identifier, EqdpEntry entry)
    {
        var origEntry = file[identifier.SetId];
        var mask      = Eqdp.Mask(identifier.Slot);
        if ((origEntry & mask) == entry)
            return false;
        
        file[identifier.SetId] = (origEntry & ~mask) | entry;
        return true;
    }

    protected override void Dispose(bool _)
    {
        for (var i = 0; i < _eqdpFiles.Length; ++i)
        {
            _eqdpFiles[i]?.Dispose();
            _eqdpFiles[i] = null;
        }

        Clear();
    }

    private ExpandedEqdpFile? GetFile(EqdpIdentifier identifier)
    {
        if (!Manager.CharacterUtility.Ready)
            return null;

        var index = Array.IndexOf(CharacterUtilityData.EqdpIndices, identifier.FileIndex());
        return _eqdpFiles[index] ??= new ExpandedEqdpFile(Manager, identifier.GenderRace, identifier.Slot.IsAccessory());
    }
}
