using OtterGui.Classes;
using Penumbra.GameData.Enums;
using Penumbra.Mods;
using System.Diagnostics.CodeAnalysis;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;
using Penumbra.Collections.Cache;
using Penumbra.Interop.Services;

namespace Penumbra.Collections;

public partial class ModCollection
{
    // Only active collections need to have a cache.
    internal CollectionCache? _cache;

    public bool HasCache
        => _cache != null;


    // Handle temporary mods for this collection.
    public void Apply(TemporaryMod tempMod, bool created)
    {
        if (created)
            _cache?.AddMod(tempMod, tempMod.TotalManipulations > 0);
        else
            _cache?.ReloadMod(tempMod, tempMod.TotalManipulations > 0);
    }

    public void Remove(TemporaryMod tempMod)
    {
        _cache?.RemoveMod(tempMod, tempMod.TotalManipulations > 0);
    }

    public IEnumerable<Utf8GamePath> ReverseResolvePath(FullPath path)
        => _cache?.ReverseResolvePath(path) ?? Array.Empty<Utf8GamePath>();

    public HashSet<Utf8GamePath>[] ReverseResolvePaths(string[] paths)
        => _cache?.ReverseResolvePaths(paths) ?? paths.Select(_ => new HashSet<Utf8GamePath>()).ToArray();

    public FullPath? ResolvePath(Utf8GamePath path)
        => _cache?.ResolvePath(path);

    // Obtain data from the cache.
    internal MetaCache? MetaCache
        => _cache?.Meta;

    public bool GetImcFile(Utf8GamePath path, [NotNullWhen(true)] out ImcFile? file)
    {
        if (_cache != null)
            return _cache.Meta.GetImcFile(path, out file);

        file = null;
        return false;
    }

    internal IReadOnlyDictionary<Utf8GamePath, ModPath> ResolvedFiles
        => _cache?.ResolvedFiles ?? new Dictionary<Utf8GamePath, ModPath>();

    internal IReadOnlyDictionary<string, (SingleArray<IMod>, object?)> ChangedItems
        => _cache?.ChangedItems ?? new Dictionary<string, (SingleArray<IMod>, object?)>();

    internal IEnumerable<SingleArray<ModConflicts>> AllConflicts
        => _cache?.AllConflicts ?? Array.Empty<SingleArray<ModConflicts>>();

    internal SingleArray<ModConflicts> Conflicts(Mod mod)
        => _cache?.Conflicts(mod) ?? new SingleArray<ModConflicts>();

    public void SetFiles(CharacterUtility utility)
    {
        if (_cache == null)
        {
            utility.ResetAll();
        }
        else
        {
            _cache.Meta.SetFiles();
            Penumbra.Log.Debug($"Set CharacterUtility resources for collection {Name}.");
        }
    }

    public void SetMetaFile(CharacterUtility utility, MetaIndex idx)
    {
        if (_cache == null)
            utility.ResetResource(idx);
        else
            _cache.Meta.SetFile(idx);
    }

    // Used for short periods of changed files.
    public MetaList.MetaReverter TemporarilySetEqdpFile(CharacterUtility utility, GenderRace genderRace, bool accessory)
        => _cache?.Meta.TemporarilySetEqdpFile(genderRace, accessory)
         ?? utility.TemporarilyResetResource(Interop.Structs.CharacterUtilityData.EqdpIdx(genderRace, accessory));

    public MetaList.MetaReverter TemporarilySetEqpFile(CharacterUtility utility)
        => _cache?.Meta.TemporarilySetEqpFile()
         ?? utility.TemporarilyResetResource(MetaIndex.Eqp);

    public MetaList.MetaReverter TemporarilySetGmpFile(CharacterUtility utility)
        => _cache?.Meta.TemporarilySetGmpFile()
         ?? utility.TemporarilyResetResource(MetaIndex.Gmp);

    public MetaList.MetaReverter TemporarilySetCmpFile(CharacterUtility utility)
        => _cache?.Meta.TemporarilySetCmpFile()
         ?? utility.TemporarilyResetResource(MetaIndex.HumanCmp);

    public MetaList.MetaReverter TemporarilySetEstFile(CharacterUtility utility, EstManipulation.EstType type)
        => _cache?.Meta.TemporarilySetEstFile(type)
         ?? utility.TemporarilyResetResource((MetaIndex)type);
}