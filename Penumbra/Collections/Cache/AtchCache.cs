using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.AtchStructs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class AtchCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<AtchIdentifier, AtchEntry>(manager, collection)
{
    private readonly Dictionary<GenderRace, (AtchFile, HashSet<AtchIdentifier>)> _atchFiles = [];

    public bool HasFile(GenderRace gr)
        => _atchFiles.ContainsKey(gr);

    public bool GetFile(GenderRace gr, [NotNullWhen(true)] out AtchFile? file)
    {
        if (!_atchFiles.TryGetValue(gr, out var p))
        {
            file = null;
            return false;
        }

        file = p.Item1;
        return true;
    }

    public void Reset()
    {
        foreach (var (_, (_, set)) in _atchFiles)
            set.Clear();

        _atchFiles.Clear();
        Clear();
    }

    protected override void ApplyModInternal(AtchIdentifier identifier, AtchEntry entry)
    {
        Collection.Counters.IncrementAtch();
        ApplyFile(identifier, entry);
    }

    private void ApplyFile(AtchIdentifier identifier, AtchEntry entry)
    {
        try
        {
            if (!_atchFiles.TryGetValue(identifier.GenderRace, out var pair))
            {
                if (!Manager.AtchManager.AtchFileBase.TryGetValue(identifier.GenderRace, out var baseFile))
                    throw new Exception($"Invalid Atch File for {identifier.GenderRace.ToName()} requested.");

                pair = (baseFile.Clone(), []);
            }


            if (!Apply(pair.Item1, identifier, entry))
                return;

            pair.Item2.Add(identifier);
            _atchFiles[identifier.GenderRace] = pair;
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not apply ATCH Manipulation {identifier}:\n{e}");
        }
    }

    protected override void RevertModInternal(AtchIdentifier identifier)
    {
        Collection.Counters.IncrementAtch();
        if (!_atchFiles.TryGetValue(identifier.GenderRace, out var pair))
            return;

        if (!pair.Item2.Remove(identifier))
            return;

        if (pair.Item2.Count == 0)
        {
            _atchFiles.Remove(identifier.GenderRace);
            return;
        }

        var def = GetDefault(Manager, identifier);
        if (def == null)
            throw new Exception($"Reverting an .atch mod had no default value for the identifier to revert to.");

        Apply(pair.Item1, identifier, def.Value);
    }

    public static AtchEntry? GetDefault(MetaFileManager manager, AtchIdentifier identifier)
    {
        if (!manager.AtchManager.AtchFileBase.TryGetValue(identifier.GenderRace, out var baseFile))
            return null;

        if (baseFile.Points.FirstOrDefault(p => p.Type == identifier.Type) is not { } point)
            return null;

        if (point.Entries.Length <= identifier.EntryIndex)
            return null;

        return point.Entries[identifier.EntryIndex];
    }

    public static bool Apply(AtchFile file, AtchIdentifier identifier, in AtchEntry entry)
    {
        if (file.Points.FirstOrDefault(p => p.Type == identifier.Type) is not { } point)
            return false;

        if (point.Entries.Length <= identifier.EntryIndex)
            return false;

        point.Entries[identifier.EntryIndex] = entry;
        return true;
    }

    protected override void Dispose(bool _)
    {
        Clear();
        _atchFiles.Clear();
    }
}
