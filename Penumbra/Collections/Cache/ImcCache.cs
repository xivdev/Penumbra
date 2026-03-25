using Luna;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.String;

namespace Penumbra.Collections.Cache;

public sealed class ImcCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<ImcIdentifier, ImcEntry>(manager, collection)
{
    private readonly Dictionary<CiByteString, (ImcFile, HashSet<ImcIdentifier>)> _imcFiles = [];

    public bool HasFile(CiByteString path)
        => _imcFiles.ContainsKey(path);

    public bool GetFile(CiByteString path, [NotNullWhen(true)] out ImcFile? file)
    {
        if (!_imcFiles.TryGetValue(path, out var p))
        {
            file = null;
            return false;
        }

        file = p.Item1;
        return true;
    }

    public void Reset()
    {
        foreach (var (_, (file, set)) in _imcFiles)
        {
            file.Reset();
            set.Clear();
        }

        _imcFiles.Clear();
        Clear();
    }

    protected override void ApplyModInternal(IMod source, ImcIdentifier identifier, ImcEntry entry)
    {
        Collection.Counters.IncrementImc();
        ApplyFile(source, identifier, entry);
    }

    private void ApplyFile(IMod source, ImcIdentifier identifier, ImcEntry entry)
    {
        var path = identifier.GamePath().Path;
        try
        {
            if (!_imcFiles.TryGetValue(path, out var pair))
                pair = (new ImcFile(Manager, identifier), []);

            if (!Apply(pair.Item1, identifier, entry))
                return;

            pair.Item2.Add(identifier);
            _imcFiles[path] = pair;
        }
        catch (ImcException e)
        {
            Penumbra.Messager.AddTaggedMessage(identifier.ToString(), new Notification(e, $"Unable to obtain a default IMC file.\n\nIMC: {identifier.ToStringWithoutImc()}.\nMod:\t{source.Name}\n\nEither your game files are corrupted or this mod manipulates an object that does not exist in your game version.", $"IMC Exception in mod \"{source.Name}\":", TimeSpan.FromSeconds(30)));
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not apply IMC Manipulation {identifier.ToStringWithoutImc()} for mod \"{source.Name}\":\n{e}");
        }
    }

    protected override void RevertModInternal(ImcIdentifier identifier)
    {
        Collection.Counters.IncrementImc();
        var path = identifier.GamePath().Path;
        if (!_imcFiles.TryGetValue(path, out var pair))
            return;

        if (!pair.Item2.Remove(identifier))
            return;

        if (pair.Item2.Count is 0)
        {
            _imcFiles.Remove(path);
            pair.Item1.Dispose();
            return;
        }

        var def = ImcFile.GetDefault(Manager, pair.Item1.Path, identifier.EquipSlot, identifier.Variant, out _);
        Apply(pair.Item1, identifier, def);
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
