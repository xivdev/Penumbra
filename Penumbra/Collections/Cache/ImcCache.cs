using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Collections.Cache;

public sealed class ImcCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<ImcIdentifier, ImcEntry>(manager, collection)
{
    private readonly Dictionary<Utf8GamePath, (ImcFile, HashSet<ImcIdentifier>)> _imcFiles = [];

    public override void SetFiles()
        => SetFiles(false);

    public bool GetFile(Utf8GamePath path, [NotNullWhen(true)] out ImcFile? file)
    {
        if (!_imcFiles.TryGetValue(path, out var p))
        {
            file = null;
            return false;
        }

        file = p.Item1;
        return true;
    }

    public void SetFiles(bool fromFullCompute)
    {
        if (fromFullCompute)
            foreach (var (path, _) in _imcFiles)
                Collection._cache!.ForceFileSync(path, PathDataHandler.CreateImc(path.Path, Collection));
        else
            foreach (var (path, _) in _imcFiles)
                Collection._cache!.ForceFile(path, PathDataHandler.CreateImc(path.Path, Collection));
    }

    public void ResetFiles()
    {
        foreach (var (path, _) in _imcFiles)
            Collection._cache!.ForceFile(path, FullPath.Empty);
    }

    protected override void IncorporateChangesInternal()
    {
        if (!Manager.CharacterUtility.Ready)
            return;

        foreach (var (identifier, (_, entry)) in this)
            ApplyFile(identifier, entry);

        Penumbra.Log.Verbose($"{Collection.AnonymizedName}: Loaded {Count} delayed IMC manipulations.");
    }


    public void Reset()
    {
        foreach (var (path, (file, set)) in _imcFiles)
        {
            Collection._cache!.RemovePath(path);
            file.Reset();
            set.Clear();
        }

        Clear();
    }

    protected override void ApplyModInternal(ImcIdentifier identifier, ImcEntry entry)
    {
        ++Collection.ImcChangeCounter;
        if (Manager.CharacterUtility.Ready)
            ApplyFile(identifier, entry);
    }

    private void ApplyFile(ImcIdentifier identifier, ImcEntry entry)
    {
        var path = identifier.GamePath();
        try
        {
            if (!_imcFiles.TryGetValue(path, out var pair))
                pair = (new ImcFile(Manager, identifier), []);


            if (!Apply(pair.Item1, identifier, entry))
                return;

            pair.Item2.Add(identifier);
            _imcFiles[path] = pair;
            var fullPath = PathDataHandler.CreateImc(pair.Item1.Path.Path, Collection);
            Collection._cache!.ForceFile(path, fullPath);
        }
        catch (ImcException e)
        {
            Manager.ValidityChecker.ImcExceptions.Add(e);
            Penumbra.Log.Error(e.ToString());
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not apply IMC Manipulation {identifier}:\n{e}");
        }
    }

    protected override void RevertModInternal(ImcIdentifier identifier)
    {
        ++Collection.ImcChangeCounter;
        var path = identifier.GamePath();
        if (!_imcFiles.TryGetValue(path, out var pair))
            return;

        if (!pair.Item2.Remove(identifier))
            return;

        if (pair.Item2.Count == 0)
        {
            _imcFiles.Remove(path);
            Collection._cache!.ForceFile(pair.Item1.Path, FullPath.Empty);
            pair.Item1.Dispose();
            return;
        }

        var def = ImcFile.GetDefault(Manager, pair.Item1.Path, identifier.EquipSlot, identifier.Variant, out _);
        if (!Apply(pair.Item1, identifier, def))
            return;

        var fullPath = PathDataHandler.CreateImc(pair.Item1.Path.Path, Collection);
        Collection._cache!.ForceFile(pair.Item1.Path, fullPath);
    }

    public static bool Apply(ImcFile file, ImcIdentifier identifier, ImcEntry entry)
        => file.SetEntry(ImcFile.PartIndex(identifier.EquipSlot), identifier.Variant.Id, entry);

    protected override void Dispose(bool _)
    {
        foreach (var (_, (file, _)) in _imcFiles)
            file.Dispose();
        Clear();
        _imcFiles.Clear();
    }
}
