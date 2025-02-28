using Dalamud.Interface.ImGuiNotification;
using OtterGui;
using OtterGui.Classes;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Communication;
using Penumbra.Mods.Editor;
using Penumbra.String.Classes;
using Penumbra.Util;
using Penumbra.GameData.Data;

namespace Penumbra.Collections.Cache;

public record struct ModPath(IMod Mod, FullPath Path);
public record ModConflicts(IMod Mod2, List<object> Conflicts, bool HasPriority, bool Solved);

/// <summary>
/// The Cache contains all required temporary data to use a collection.
/// It will only be setup if a collection gets activated in any way.
/// </summary>
public sealed class CollectionCache : IDisposable
{
    private readonly CollectionCacheManager                                          _manager;
    private readonly ModCollection                                                   _collection;
    public readonly  CollectionModData                                               ModData       = new();
    private readonly SortedList<string, (SingleArray<IMod>, IIdentifiedObjectData)> _changedItems = [];
    public readonly  ConcurrentDictionary<Utf8GamePath, ModPath>                     ResolvedFiles = new();
    public readonly  CustomResourceCache                                             CustomResources;
    public readonly  MetaCache                                                       Meta;
    public readonly  Dictionary<IMod, SingleArray<ModConflicts>>                     ConflictDict = [];

    public int Calculating = -1;

    public string AnonymizedName
        => _collection.Identity.AnonymizedName;

    public IEnumerable<SingleArray<ModConflicts>> AllConflicts
        => ConflictDict.Values;

    public SingleArray<ModConflicts> Conflicts(IMod mod)
        => ConflictDict.TryGetValue(mod, out var c) ? c : new SingleArray<ModConflicts>();

    private int _changedItemsSaveCounter = -1;

    // Obtain currently changed items. Computes them if they haven't been computed before.
    public IReadOnlyDictionary<string, (SingleArray<IMod>, IIdentifiedObjectData)> ChangedItems
    {
        get
        {
            SetChangedItems();
            return _changedItems;
        }
    }

    // The cache reacts through events on its collection changing.
    public CollectionCache(CollectionCacheManager manager, ModCollection collection)
    {
        _manager        = manager;
        _collection     = collection;
        Meta            = new MetaCache(manager.MetaFileManager, _collection);
        CustomResources = new CustomResourceCache(manager.ResourceLoader);
    }

    public void Dispose()
    {
        Meta.Dispose();
        CustomResources.Dispose();
        GC.SuppressFinalize(this);
    }

    ~CollectionCache()
        => Dispose();

    // Resolve a given game path according to this collection.
    public FullPath? ResolvePath(Utf8GamePath gameResourcePath)
    {
        if (!ResolvedFiles.TryGetValue(gameResourcePath, out var candidate))
            return null;

        if (candidate.Path.InternalName.Length > Utf8GamePath.MaxGamePathLength
         || candidate.Path is { IsRooted: true, Exists: false })
            return null;

        return candidate.Path;
    }

    // For a given full path, find all game paths that currently use this file.
    public IEnumerable<Utf8GamePath> ReverseResolvePath(FullPath localFilePath)
    {
        var needle = localFilePath.FullName.ToLower();
        if (localFilePath.IsRooted)
            needle = needle.Replace('/', '\\');

        var iterator = ResolvedFiles
            .Where(f => string.Equals(f.Value.Path.FullName, needle, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key);

        // For files that are not rooted, try to add themselves.
        if (!localFilePath.IsRooted && Utf8GamePath.FromString(localFilePath.FullName, out var utf8))
            iterator = iterator.Prepend(utf8);

        return iterator;
    }

    // Reverse resolve multiple paths at once for efficiency.
    public HashSet<Utf8GamePath>[] ReverseResolvePaths(IReadOnlyCollection<string> fullPaths)
    {
        if (fullPaths.Count == 0)
            return [];

        var ret  = new HashSet<Utf8GamePath>[fullPaths.Count];
        var dict = new Dictionary<FullPath, int>(fullPaths.Count);
        foreach (var (path, idx) in fullPaths.WithIndex())
        {
            dict[new FullPath(path)] = idx;
            ret[idx] = !Path.IsPathRooted(path) && Utf8GamePath.FromString(path, out var utf8)
                ? [utf8]
                : [];
        }

        foreach (var (game, full) in ResolvedFiles)
        {
            if (dict.TryGetValue(full.Path, out var idx))
                ret[idx].Add(game);
        }

        return ret;
    }

    public void ReloadMod(IMod mod, bool addMetaChanges)
        => _manager.AddChange(ChangeData.ModReload(this, mod, addMetaChanges));

    public void AddMod(IMod mod, bool addMetaChanges)
        => _manager.AddChange(ChangeData.ModAddition(this, mod, addMetaChanges));

    public void RemoveMod(IMod mod, bool addMetaChanges)
        => _manager.AddChange(ChangeData.ModRemoval(this, mod, addMetaChanges));

    /// <summary> Force a file to be resolved to a specific path regardless of conflicts. </summary>
    internal void ForceFileSync(Utf8GamePath path, FullPath fullPath)
    {
        if (!CheckFullPath(path, fullPath))
            return;

        if (ResolvedFiles.Remove(path, out var modPath))
        {
            ModData.RemovePath(modPath.Mod, path);
            if (fullPath.FullName.Length > 0)
            {
                ResolvedFiles.TryAdd(path, new ModPath(Mod.ForcedFiles, fullPath));
                CustomResources.Invalidate(path);
                InvokeResolvedFileChange(_collection, ResolvedFileChanged.Type.Replaced, path, fullPath, modPath.Path,
                    Mod.ForcedFiles);
            }
            else
            {
                CustomResources.Invalidate(path);
                InvokeResolvedFileChange(_collection, ResolvedFileChanged.Type.Removed, path, FullPath.Empty, modPath.Path, null);
            }
        }
        else if (fullPath.FullName.Length > 0)
        {
            ResolvedFiles.TryAdd(path, new ModPath(Mod.ForcedFiles, fullPath));
            CustomResources.Invalidate(path);
            InvokeResolvedFileChange(_collection, ResolvedFileChanged.Type.Added, path, fullPath, FullPath.Empty, Mod.ForcedFiles);
        }
    }

    private void ReloadModSync(IMod mod, bool addMetaChanges)
    {
        RemoveModSync(mod, addMetaChanges);
        AddModSync(mod, addMetaChanges);
    }

    internal void RemoveModSync(IMod mod, bool addMetaChanges)
    {
        var conflicts = Conflicts(mod);
        var (paths, manipulations) = ModData.RemoveMod(mod);

        if (addMetaChanges)
            _collection.Counters.IncrementChange();

        foreach (var path in paths)
        {
            if (ResolvedFiles.Remove(path, out var mp))
            {
                CustomResources.Invalidate(path);
                if (mp.Mod != mod)
                    Penumbra.Log.Warning(
                        $"Invalid mod state, removing {mod.Name} and associated file {path} returned current mod {mp.Mod.Name}.");
                else
                    _manager.ResolvedFileChanged.Invoke(_collection, ResolvedFileChanged.Type.Removed, path, FullPath.Empty, mp.Path, mp.Mod);
            }
        }

        foreach (var manipulation in manipulations)
        {
            if (Meta.RevertMod(manipulation, out var mp) && mp != mod)
                Penumbra.Log.Warning(
                    $"Invalid mod state, removing {mod.Name} and associated manipulation {manipulation} returned current mod {mp.Name}.");
        }

        ConflictDict.Remove(mod);
        foreach (var conflict in conflicts)
        {
            if (conflict.HasPriority)
            {
                ReloadModSync(conflict.Mod2, false);
            }
            else
            {
                var newConflicts = Conflicts(conflict.Mod2).Remove(c => c.Mod2 == mod);
                if (newConflicts.Count > 0)
                    ConflictDict[conflict.Mod2] = newConflicts;
                else
                    ConflictDict.Remove(conflict.Mod2);
            }
        }

        if (addMetaChanges)
            _manager.MetaFileManager.ApplyDefaultFiles(_collection);
    }


    /// <summary> Add all files and possibly manipulations of a given mod according to its settings in this collection. </summary>
    internal void AddModSync(IMod mod, bool addMetaChanges)
    {
        var files = GetFiles(mod);
        foreach (var (path, file) in files.FileRedirections)
            AddFile(path, file, mod);

        if (files.Manipulations.Count > 0)
        {
            foreach (var (identifier, entry) in files.Manipulations.Eqp)
                AddManipulation(mod, identifier, entry);
            foreach (var (identifier, entry) in files.Manipulations.Eqdp)
                AddManipulation(mod, identifier, entry);
            foreach (var (identifier, entry) in files.Manipulations.Est)
                AddManipulation(mod, identifier, entry);
            foreach (var (identifier, entry) in files.Manipulations.Gmp)
                AddManipulation(mod, identifier, entry);
            foreach (var (identifier, entry) in files.Manipulations.Rsp)
                AddManipulation(mod, identifier, entry);
            foreach (var (identifier, entry) in files.Manipulations.Imc)
                AddManipulation(mod, identifier, entry);
            foreach (var (identifier, entry) in files.Manipulations.Atch)
                AddManipulation(mod, identifier, entry);
            foreach (var identifier in files.Manipulations.GlobalEqp)
                AddManipulation(mod, identifier, null!);
        }

        if (addMetaChanges)
        {
            _collection.Counters.IncrementChange();
            _manager.MetaFileManager.ApplyDefaultFiles(_collection);
        }
    }

    private AppliedModData GetFiles(IMod mod)
    {
        if (mod.Index < 0)
            return mod.GetData();

        var settings = _collection.GetActualSettings(mod.Index).Settings;
        return settings is not { Enabled: true }
            ? AppliedModData.Empty
            : mod.GetData(settings);
    }

    /// <summary> Invoke only if not in a full recalculation. </summary>
    private void InvokeResolvedFileChange(ModCollection collection, ResolvedFileChanged.Type type, Utf8GamePath key, FullPath value,
        FullPath old, IMod? mod)
    {
        if (Calculating == -1)
            _manager.ResolvedFileChanged.Invoke(collection, type, key, value, old, mod);
    }

    private static bool IsRedirectionSupported(Utf8GamePath path, IMod mod)
    {
        var ext = path.Extension().AsciiToLower().ToString();
        switch (ext)
        {
            case ".atch" or ".eqp" or ".eqdp" or ".est" or ".gmp" or ".cmp" or ".imc":
                Penumbra.Messager.NotificationMessage(
                    $"Redirection of {ext} files for {mod.Name} is unsupported. This probably means that the mod is outdated and may not work correctly.\n\nPlease tell the mod creator to use the corresponding meta manipulations instead.",
                    NotificationType.Warning);
                return false;
            case ".lvb" or ".lgb" or ".sgb":
                Penumbra.Messager.NotificationMessage($"Redirection of {ext} files for {mod.Name} is unsupported as this breaks the game.\n\nThis mod will probably not work correctly.",
                    NotificationType.Warning);
                return false;
            default: return true;
        }
    }

    // Add a specific file redirection, handling potential conflicts.
    // For different mods, higher mod priority takes precedence before option group priority,
    // which takes precedence before option priority, which takes precedence before ordering.
    // Inside the same mod, conflicts are not recorded.
    private void AddFile(Utf8GamePath path, FullPath file, IMod mod)
    {
        if (!CheckFullPath(path, file))
            return;

        if (!IsRedirectionSupported(path, mod))
            return;

        try
        {
            if (ResolvedFiles.TryAdd(path, new ModPath(mod, file)))
            {
                ModData.AddPath(mod, path);
                CustomResources.Invalidate(path);
                InvokeResolvedFileChange(_collection, ResolvedFileChanged.Type.Added, path, file, FullPath.Empty, mod);
                return;
            }

            var modPath = ResolvedFiles[path];
            // Lower prioritized option in the same mod.
            if (mod == modPath.Mod)
                return;

            if (AddConflict(path, mod, modPath.Mod))
            {
                ModData.RemovePath(modPath.Mod, path);
                ResolvedFiles[path] = new ModPath(mod, file);
                ModData.AddPath(mod, path);
                CustomResources.Invalidate(path);
                InvokeResolvedFileChange(_collection, ResolvedFileChanged.Type.Replaced, path, file, modPath.Path, mod);
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error(
                $"[{Environment.CurrentManagedThreadId}] Error adding redirection {file} -> {path} for mod {mod.Name} to collection cache {AnonymizedName}:\n{ex}");
        }
    }


    // Remove all empty conflict sets for a given mod with the given conflicts.
    // If transitive is true, also removes the corresponding version of the other mod.
    private void RemoveEmptyConflicts(IMod mod, SingleArray<ModConflicts> oldConflicts, bool transitive)
    {
        var changedConflicts = oldConflicts.Remove(c =>
        {
            if (c.Conflicts.Count == 0)
            {
                if (transitive)
                    RemoveEmptyConflicts(c.Mod2, Conflicts(c.Mod2), false);

                return true;
            }

            return false;
        });
        if (changedConflicts.Count == 0)
            ConflictDict.Remove(mod);
        else
            ConflictDict[mod] = changedConflicts;
    }

    // Add a new conflict between the added mod and the existing mod.
    // Update all other existing conflicts between the existing mod and other mods if necessary.
    // Returns if the added mod takes priority before the existing mod.
    private bool AddConflict(object data, IMod addedMod, IMod existingMod)
    {
        var addedPriority = addedMod.Index >= 0 ? _collection.GetActualSettings(addedMod.Index).Settings!.Priority : addedMod.Priority;
        var existingPriority =
            existingMod.Index >= 0 ? _collection.GetActualSettings(existingMod.Index).Settings!.Priority : existingMod.Priority;

        if (existingPriority < addedPriority)
        {
            var tmpConflicts = Conflicts(existingMod);
            foreach (var conflict in tmpConflicts)
            {
                if (data is Utf8GamePath path && conflict.Conflicts.RemoveAll(p => p is Utf8GamePath x && x.Equals(path)) > 0
                 || data is IMetaIdentifier meta && conflict.Conflicts.RemoveAll(m => m.Equals(meta)) > 0)
                    AddConflict(data, addedMod, conflict.Mod2);
            }

            RemoveEmptyConflicts(existingMod, tmpConflicts, true);
        }

        var addedConflicts    = Conflicts(addedMod);
        var existingConflicts = Conflicts(existingMod);
        if (addedConflicts.FindFirst(c => c.Mod2 == existingMod, out var oldConflicts))
        {
            // Only need to change one list since both conflict lists refer to the same list.
            oldConflicts.Conflicts.Add(data);
        }
        else
        {
            // Add the same conflict list to both conflict directions.
            var conflictList = new List<object> { data };
            ConflictDict[addedMod] = addedConflicts.Append(new ModConflicts(existingMod, conflictList, existingPriority < addedPriority,
                existingPriority != addedPriority));
            ConflictDict[existingMod] = existingConflicts.Append(new ModConflicts(addedMod, conflictList,
                existingPriority >= addedPriority,
                existingPriority != addedPriority));
        }

        return existingPriority < addedPriority;
    }

    // Add a specific manipulation, handling potential conflicts.
    // For different mods, higher mod priority takes precedence before option group priority,
    // which takes precedence before option priority, which takes precedence before ordering.
    // Inside the same mod, conflicts are not recorded.
    private void AddManipulation(IMod mod, IMetaIdentifier identifier, object entry)
    {
        if (!Meta.TryGetMod(identifier, out var existingMod))
        {
            Meta.ApplyMod(mod, identifier, entry);
            ModData.AddManip(mod, identifier);
            return;
        }

        // Lower prioritized option in the same mod.
        if (mod == existingMod)
            return;

        if (AddConflict(identifier, mod, existingMod))
        {
            ModData.RemoveManip(existingMod, identifier);
            Meta.ApplyMod(mod, identifier, entry);
            ModData.AddManip(mod, identifier);
        }
    }


    // Identify and record all manipulated objects for this entire collection.
    private void SetChangedItems()
    {
        if (_changedItemsSaveCounter == _collection.Counters.Change)
            return;

        try
        {
            _changedItemsSaveCounter = _collection.Counters.Change;
            _changedItems.Clear();
            // Skip IMCs because they would result in far too many false-positive items,
            // since they are per set instead of per item-slot/item/variant.
            var identifier = _manager.MetaFileManager.Identifier;
            var items      = new SortedList<string, IIdentifiedObjectData>(512);

            void AddItems(IMod mod)
            {
                foreach (var (name, obj) in items)
                {
                    if (!_changedItems.TryGetValue(name, out var data))
                        _changedItems.Add(name, (new SingleArray<IMod>(mod), obj));
                    else if (!data.Item1.Contains(mod))
                        _changedItems[name] = (data.Item1.Append(mod),
                            obj is IdentifiedCounter x && data.Item2 is IdentifiedCounter y ? x + y : obj);
                    else if (obj is IdentifiedCounter x && data.Item2 is IdentifiedCounter y)
                        _changedItems[name] = (data.Item1, x + y);
                }

                items.Clear();
            }

            foreach (var (resolved, modPath) in ResolvedFiles.Where(file => !file.Key.Path.EndsWith("imc"u8)))
            {
                identifier.Identify(items, resolved.ToString());
                AddItems(modPath.Mod);
            }

            foreach (var (manip, mod) in Meta.IdentifierSources)
            {
                manip.AddChangedItems(identifier, items);
                AddItems(mod);
            }

            if (_manager.Config.HideMachinistOffhandFromChangedItems)
                _changedItems.RemoveMachinistOffhands();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Unknown Error:\n{e}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CheckFullPath(Utf8GamePath path, FullPath fullPath)
    {
        if (fullPath.InternalName.Length < Utf8GamePath.MaxGamePathLength)
            return true;

        Penumbra.Log.Error($"The redirected path is too long to add the redirection\n\t{path}\n\t--> {fullPath}");
        return false;
    }

    public readonly record struct ChangeData
    {
        public readonly CollectionCache Cache;
        public readonly Utf8GamePath    Path;
        public readonly FullPath        FullPath;
        public readonly IMod            Mod;
        public readonly byte            Type;
        public readonly bool            AddMetaChanges;

        private ChangeData(CollectionCache cache, Utf8GamePath p, FullPath fp, IMod m, byte t, bool a)
        {
            Cache          = cache;
            Path           = p;
            FullPath       = fp;
            Mod            = m;
            Type           = t;
            AddMetaChanges = a;
        }

        public static ChangeData ModRemoval(CollectionCache cache, IMod mod, bool addMetaChanges)
            => new(cache, Utf8GamePath.Empty, FullPath.Empty, mod, 0, addMetaChanges);

        public static ChangeData ModAddition(CollectionCache cache, IMod mod, bool addMetaChanges)
            => new(cache, Utf8GamePath.Empty, FullPath.Empty, mod, 1, addMetaChanges);

        public static ChangeData ModReload(CollectionCache cache, IMod mod, bool addMetaChanges)
            => new(cache, Utf8GamePath.Empty, FullPath.Empty, mod, 2, addMetaChanges);

        public static ChangeData ForcedFile(CollectionCache cache, Utf8GamePath p, FullPath fp)
            => new(cache, p, fp, Mods.Mod.ForcedFiles, 3, false);

        public void Apply()
        {
            switch (Type)
            {
                case 0:
                    Cache.RemoveModSync(Mod, AddMetaChanges);
                    break;
                case 1:
                    Cache.AddModSync(Mod, AddMetaChanges);
                    break;
                case 2:
                    Cache.ReloadModSync(Mod, AddMetaChanges);
                    break;
                case 3:
                    Cache.ForceFileSync(Path, FullPath);
                    break;
            }
        }
    }
}
