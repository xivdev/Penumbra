using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OtterGui.Classes;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Collections.Cache;

public class CollectionCacheManager : IDisposable
{
    private readonly FrameworkManager    _framework;
    private readonly CommunicatorService _communicator;
    private readonly TempModManager      _tempMods;
    private readonly ModStorage          _modStorage;
    private readonly ActiveCollections   _active;

    internal readonly MetaFileManager MetaFileManager;

    private readonly Dictionary<ModCollection, CollectionCache> _caches = new();

    public int Count
        => _caches.Count;

    public IEnumerable<(ModCollection Collection, CollectionCache Cache)> Active
        => _caches.Where(c => c.Key.Index > ModCollection.Empty.Index).Select(p => (p.Key, p.Value));

    public CollectionCacheManager(FrameworkManager framework, CommunicatorService communicator,
        TempModManager tempMods, ModStorage modStorage, MetaFileManager metaFileManager, ActiveCollections active)
    {
        _framework      = framework;
        _communicator   = communicator;
        _tempMods       = tempMods;
        _modStorage     = modStorage;
        MetaFileManager = metaFileManager;
        _active         = active;

        _communicator.CollectionChange.Subscribe(OnCollectionChange);
        _communicator.ModPathChanged.Subscribe(OnModChangeAddition, -100);
        _communicator.ModPathChanged.Subscribe(OnModChangeRemoval,  100);
        _communicator.TemporaryGlobalModChange.Subscribe(OnGlobalModChange);
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, -100);
        _communicator.ModSettingChanged.Subscribe(OnModSettingChange);
        _communicator.CollectionInheritanceChanged.Subscribe(OnCollectionInheritanceChange);
        CreateNecessaryCaches();

        if (!MetaFileManager.CharacterUtility.Ready)
            MetaFileManager.CharacterUtility.LoadingFinished += IncrementCounters;
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        _communicator.ModPathChanged.Unsubscribe(OnModChangeAddition);
        _communicator.ModPathChanged.Unsubscribe(OnModChangeRemoval);
        _communicator.TemporaryGlobalModChange.Unsubscribe(OnGlobalModChange);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
        _communicator.ModSettingChanged.Unsubscribe(OnModSettingChange);
        _communicator.CollectionInheritanceChanged.Unsubscribe(OnCollectionInheritanceChange);
        MetaFileManager.CharacterUtility.LoadingFinished -= IncrementCounters;
    }

    /// <summary> Only creates a new cache, does not update an existing one. </summary>
    public bool CreateCache(ModCollection collection)
    {
        if (_caches.ContainsKey(collection) || collection.Index == ModCollection.Empty.Index)
            return false;

        var cache = new CollectionCache(this, collection);
        _caches.Add(collection, cache);
        collection._cache = cache;
        Penumbra.Log.Verbose($"Created new cache for collection {collection.AnonymizedName}.");
        return true;
    }

    /// <summary>
    /// Update the effective file list for the given cache.
    /// Does not create caches.
    /// </summary>
    public void CalculateEffectiveFileList(ModCollection collection)
        => _framework.RegisterImportant(nameof(CalculateEffectiveFileList) + collection.Name,
            () => CalculateEffectiveFileListInternal(collection));

    private void CalculateEffectiveFileListInternal(ModCollection collection)
    {
        // Skip the empty collection.
        if (collection.Index == 0)
            return;

        Penumbra.Log.Debug($"[{Thread.CurrentThread.ManagedThreadId}] Recalculating effective file list for {collection.AnonymizedName}");
        if (!_caches.TryGetValue(collection, out var cache))
        {
            Penumbra.Log.Error(
                $"[{Thread.CurrentThread.ManagedThreadId}] Recalculating effective file list for {collection.AnonymizedName} failed, no cache exists.");
            return;
        }

        FullRecalculation(collection, cache);

        Penumbra.Log.Debug(
            $"[{Thread.CurrentThread.ManagedThreadId}] Recalculation of effective file list for {collection.AnonymizedName} finished.");
    }

    private void FullRecalculation(ModCollection collection, CollectionCache cache)
    {
        cache.ResolvedFiles.Clear();
        cache.Meta.Reset();
        cache._conflicts.Clear();

        // Add all forced redirects.
        foreach (var tempMod in _tempMods.ModsForAllCollections.Concat(
                     _tempMods.Mods.TryGetValue(collection, out var list) ? list : Array.Empty<TemporaryMod>()))
            cache.AddMod(tempMod, false);

        foreach (var mod in _modStorage)
            cache.AddMod(mod, false);

        cache.AddMetaFiles();

        ++collection.ChangeCounter;

        MetaFileManager.ApplyDefaultFiles(collection);
    }

    private void OnCollectionChange(CollectionType type, ModCollection? old, ModCollection? newCollection, string displayName)
    {
        if (type is CollectionType.Temporary)
        {
            if (newCollection != null && CreateCache(newCollection))
                CalculateEffectiveFileList(newCollection);

            if (old != null)
                ClearCache(old);
        }
        else
        {
            RemoveCache(old);

            if (type is not CollectionType.Inactive && newCollection != null && newCollection.Index != 0 && CreateCache(newCollection))
                CalculateEffectiveFileList(newCollection);
        }
    }


    private void OnModChangeRemoval(ModPathChangeType type, Mod mod, DirectoryInfo? oldModPath, DirectoryInfo? newModPath)
    {
        switch (type)
        {
            case ModPathChangeType.Deleted:
            case ModPathChangeType.StartingReload:
                foreach (var collection in _caches.Keys.Where(c => c[mod.Index].Settings?.Enabled == true))
                    collection._cache!.RemoveMod(mod, true);
                break;
            case ModPathChangeType.Moved:
                foreach (var collection in _caches.Keys.Where(c => c.HasCache && c[mod.Index].Settings?.Enabled == true))
                    collection._cache!.ReloadMod(mod, true);
                break;
        }
    }

    private void OnModChangeAddition(ModPathChangeType type, Mod mod, DirectoryInfo? oldModPath, DirectoryInfo? newModPath)
    {
        if (type is not (ModPathChangeType.Added or ModPathChangeType.Reloaded))
            return;

        foreach (var collection in _caches.Keys.Where(c => c[mod.Index].Settings?.Enabled == true))
            collection._cache!.AddMod(mod, true);
    }

    /// <summary> Apply a mod change to all collections with a cache. </summary>
    private void OnGlobalModChange(TemporaryMod mod, bool created, bool removed)
        => TempModManager.OnGlobalModChange(_caches.Keys, mod, created, removed);

    /// <summary> Remove a cache from a collection if it is active. </summary>
    private void RemoveCache(ModCollection? collection)
    {
        if (collection != null
         && collection.Index > ModCollection.Empty.Index
         && collection.Index != _active.Default.Index
         && collection.Index != _active.Interface.Index
         && collection.Index != _active.Current.Index
         && _active.SpecialAssignments.All(c => c.Value.Index != collection.Index)
         && _active.Individuals.All(c => c.Collection.Index != collection.Index))
            ClearCache(collection);
    }

    /// <summary> Prepare Changes by removing mods from caches with collections or add or reload mods. </summary>
    private void OnModOptionChange(ModOptionChangeType type, Mod mod, int groupIdx, int optionIdx, int movedToIdx)
    {
        if (type is ModOptionChangeType.PrepareChange)
        {
            foreach (var collection in _caches.Keys.Where(collection => collection[mod.Index].Settings is { Enabled: true }))
                collection._cache!.RemoveMod(mod, false);

            return;
        }

        type.HandlingInfo(out _, out var recomputeList, out var reload);

        if (!recomputeList)
            return;

        foreach (var collection in _caches.Keys.Where(collection => collection[mod.Index].Settings is { Enabled: true }))
        {
            if (reload)
                collection._cache!.ReloadMod(mod, true);
            else
                collection._cache!.AddMod(mod, true);
        }
    }

    /// <summary> Increment the counter to ensure new files are loaded after applying meta changes. </summary>
    private void IncrementCounters()
    {
        foreach (var (collection, _) in _caches)
            ++collection.ChangeCounter;
        MetaFileManager.CharacterUtility.LoadingFinished -= IncrementCounters;
    }

    private void OnModSettingChange(ModCollection collection, ModSettingChange type, Mod? mod, int oldValue, int groupIdx, bool _)
    {
        if (!_caches.TryGetValue(collection, out var cache))
            return;

        switch (type)
        {
            case ModSettingChange.Inheritance:
                cache.ReloadMod(mod!, true);
                break;
            case ModSettingChange.EnableState:
                if (oldValue == 0)
                    cache.AddMod(mod!, true);
                else if (oldValue == 1)
                    cache.RemoveMod(mod!, true);
                else if (collection[mod!.Index].Settings?.Enabled == true)
                    cache.ReloadMod(mod!, true);
                else
                    cache.RemoveMod(mod!, true);

                break;
            case ModSettingChange.Priority:
                if (cache.Conflicts(mod!).Count > 0)
                    cache.ReloadMod(mod!, true);

                break;
            case ModSettingChange.Setting:
                if (collection[mod!.Index].Settings?.Enabled == true)
                    cache.ReloadMod(mod!, true);

                break;
            case ModSettingChange.MultiInheritance:
            case ModSettingChange.MultiEnableState:
                FullRecalculation(collection, cache);
                break;
        }
    }

    /// <summary>
    /// Inheritance changes are too big to check for relevance,
    /// just recompute everything.
    /// </summary>
    private void OnCollectionInheritanceChange(ModCollection collection, bool _)
    {
        if (_caches.TryGetValue(collection, out var cache))
            FullRecalculation(collection, cache);
    }

    /// <summary> Clear the current cache of a collection. </summary>
    private void ClearCache(ModCollection collection)
    {
        if (!_caches.Remove(collection, out var cache))
            return;

        cache.Dispose();
        collection._cache = null;
        Penumbra.Log.Verbose($"Cleared cache of collection {collection.AnonymizedName}.");
    }

    /// <summary>
    /// Cache handling. Usually recreate caches on the next framework tick,
    /// but at launch create all of them at once.
    /// </summary>
    private void CreateNecessaryCaches()
    {
        var tasks = _active.SpecialAssignments.Select(p => p.Value)
            .Concat(_active.Individuals.Select(p => p.Collection))
            .Prepend(_active.Current)
            .Prepend(_active.Default)
            .Prepend(_active.Interface)
            .Distinct()
            .Select(c => CreateCache(c) ? Task.Run(() => CalculateEffectiveFileListInternal(c)) : Task.CompletedTask)
            .ToArray();

        Task.WaitAll(tasks);
    }
}
